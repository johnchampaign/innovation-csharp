namespace Innovation.Core.Players;

/// <summary>
/// One-ply greedy <see cref="IPlayerController"/>. For every top-level
/// action in the legal set, it clones the game state, applies the
/// candidate action, lets any resulting dogma play out via a random
/// roll-out policy inside the clone, and scores the resulting position
/// with <see cref="HeuristicEvaluator.ScoreRelative"/>. The action with
/// the highest score is returned.
///
/// Scope of "greedy" here is top-level only. In-dogma prompts still
/// route through the embedded <see cref="RandomController"/>; mid-dogma
/// look-ahead would require cloning the in-flight <see cref="DogmaContext"/>
/// (which Phase 5 doesn't tackle). That mirrors the VB6 AI's behavior at
/// depth 1 — the original code recurses one ply for the turn-level
/// decision and falls back on simpler logic inside effect resolution.
///
/// Deterministic given its seed: the roll-out policy controls every
/// random decision inside the trial clones, so repeated ChooseAction
/// calls with the same state and seed produce identical picks.
/// </summary>
public sealed class GreedyController : IPlayerController
{
    private readonly RandomController _rollout;
    private readonly RationalOpponentController _opponentRollout;

    /// <summary>
    /// 1 = single-ply (just this action). 2 = look ahead to the AI's
    /// second action of the same turn so combos like "Bicycle then
    /// Achieve" are visible. 2 costs ~|legal|² trials per top-level
    /// decision; still well under a second on modern hardware. Default
    /// stays 1 so existing tests keep their fast behaviour; the WPF and
    /// WinForms shells pass 2.
    /// </summary>
    private readonly int _lookahead;

    public GreedyController(Random rng, int lookahead = 1)
    {
        _rollout = new RandomController(rng);
        // Seed the opponent rollout off the same rng for determinism.
        _opponentRollout = new RationalOpponentController(new Random(rng.Next()));
        _lookahead = Math.Max(1, lookahead);
    }
    public GreedyController(int seed, int lookahead = 1) : this(new Random(seed), lookahead) { }
    public GreedyController() : this(new Random(), 1) { }

    public int ChooseInitialMeld(GameState g, PlayerState self)
    {
        // Try each starting-hand card as the meld; pick the one that
        // maximizes post-meld relative score. At setup nothing else in
        // the game state differs between candidates, so the winner is
        // whichever card grows the per-color / icon bonuses most.
        int best = self.Hand[0];
        long bestScore = long.MinValue;
        foreach (var id in self.Hand)
        {
            GameLog.Pause();
            long s;
            try
            {
                var clone = g.DeepClone();
                Mechanics.Meld(clone, clone.Players[self.Index], id);
                s = HeuristicEvaluator.ScoreRelative(clone, self.Index);
            }
            finally { GameLog.Resume(); }
            if (s > bestScore) { bestScore = s; best = id; }
        }
        return best;
    }

    public PlayerAction ChooseAction(GameState g, PlayerState self, IReadOnlyList<PlayerAction> legal)
    {
        if (legal.Count == 1) return legal[0];

        // Two-ply only kicks in when this is the first of two same-turn
        // actions. With one action remaining, two-ply == one-ply. The
        // setup-time and end-of-turn checks fall through to single-ply.
        bool twoPly = _lookahead >= 2 && g.ActionsRemaining >= 2 && !g.IsGameOver;

        PlayerAction best = legal[0];
        long bestScore = long.MinValue;
        foreach (var action in legal)
        {
            long score = twoPly
                ? TryActionTwoPly(g, self.Index, action)
                : TryAction(g, self.Index, action);
            if (score > bestScore) { bestScore = score; best = action; }
        }
        return best;
    }

    /// <summary>
    /// Score the position reached by applying <paramref name="firstAction"/>
    /// AND THEN the best legal second action of the same turn — so the
    /// search sees "play Bicycle, then Achieve age 7" as a single plan,
    /// not as two independent decisions.
    ///
    /// The second action is found by enumerating legal actions in the
    /// post-first-action clone and recursively scoring each via the
    /// existing one-ply trial. That's |legal|² trials in the worst case.
    ///
    /// If the first action ends the turn (game over, last achievement
    /// claimed, etc.) we fall back to the single-ply score on the
    /// resulting state.
    /// </summary>
    private long TryActionTwoPly(GameState g, int selfIndex, PlayerAction firstAction)
    {
        GameLog.Pause();
        try
        {
            GameState afterFirst;
            try
            {
                afterFirst = g.DeepClone();
                var rollouts = MakeRollouts(afterFirst.Players.Length, selfIndex);
                var runner = new GameRunner(afterFirst, rollouts);
                runner.ApplyActionAndResolveDogma(firstAction);
            }
            catch
            {
                return long.MinValue;
            }

            // Turn ended (game over, or the runner advanced the turn
            // because no actions remained, or a Dogma claim used up the
            // turn) — score the resulting state directly.
            if (afterFirst.IsGameOver
                || afterFirst.ActivePlayer != selfIndex
                || afterFirst.ActionsRemaining == 0)
            {
                return HeuristicEvaluator.ScoreRelative(afterFirst, selfIndex, searchDepth: 1);
            }

            // Search action 2.
            var legal2 = LegalActions.Enumerate(afterFirst, afterFirst.Players[selfIndex]);
            if (legal2.Count == 0)
                return HeuristicEvaluator.ScoreRelative(afterFirst, selfIndex, searchDepth: 1);

            long bestSecond = long.MinValue;
            foreach (var second in legal2)
            {
                long s = TryActionFromClone(afterFirst, selfIndex, second);
                if (s > bestSecond) bestSecond = s;
            }
            return bestSecond == long.MinValue
                ? HeuristicEvaluator.ScoreRelative(afterFirst, selfIndex, searchDepth: 1)
                : bestSecond;
        }
        finally { GameLog.Resume(); }
    }

    /// <summary>Single-ply trial of <paramref name="action"/> from a
    /// pre-cloned base. Used by two-ply search for the second action.</summary>
    private long TryActionFromClone(GameState baseClone, int selfIndex, PlayerAction action)
    {
        GameState clone;
        try { clone = baseClone.DeepClone(); }
        catch { return long.MinValue; }

        var rollouts = MakeRollouts(clone.Players.Length, selfIndex);
        var runner = new GameRunner(clone, rollouts);
        try { runner.ApplyActionAndResolveDogma(action); }
        catch { return long.MinValue; }

        return HeuristicEvaluator.ScoreRelative(clone, selfIndex, searchDepth: 1);
    }

    private IPlayerController[] MakeRollouts(int n, int selfIndex)
    {
        var rollouts = new IPlayerController[n];
        for (int i = 0; i < n; i++)
            rollouts[i] = (i == selfIndex) ? (IPlayerController)_rollout : _opponentRollout;
        return rollouts;
    }

    /// <summary>
    /// Score the position reached by applying <paramref name="action"/>
    /// in a cloned game. If the trial throws (e.g. an inconsistency an
    /// invalid handler exposes under look-ahead), return
    /// <see cref="long.MinValue"/> so the candidate is discarded.
    /// </summary>
    private long TryAction(GameState g, int selfIndex, PlayerAction action)
    {
        GameLog.Pause();
        try { return TryActionInner(g, selfIndex, action); }
        finally { GameLog.Resume(); }
    }

    private long TryActionInner(GameState g, int selfIndex, PlayerAction action)
    {
        GameState clone;
        try
        {
            clone = g.DeepClone();
        }
        catch
        {
            return long.MinValue;
        }

        // Trial seats: self uses the random rollout (keeps in-dogma choices
        // varied so the activator's own picks aren't degenerate). Opponents
        // use a RATIONAL controller that takes share-positive prompts
        // (mostly free benefits) but commits the minimum hand cost — gives
        // the activator a realistic projection of "if my opponent acts
        // sensibly, what will the dogma actually do?" Earlier versions
        // used a strictly pessimistic decline-everything controller, which
        // under-modelled positive-sum shares like Mathematics where the
        // opponent gains a free meld and would obviously share (#1).
        var rollouts = new IPlayerController[clone.Players.Length];
        for (int i = 0; i < rollouts.Length; i++)
            rollouts[i] = (i == selfIndex) ? (IPlayerController)_rollout : _opponentRollout;

        var runner = new GameRunner(clone, rollouts);
        try
        {
            runner.ApplyActionAndResolveDogma(action);
        }
        catch
        {
            return long.MinValue;
        }

        return HeuristicEvaluator.ScoreRelative(clone, selfIndex, searchDepth: 1);
    }

    // In-dogma prompts. A full one-ply search over choice resolutions is
    // Phase 5.4+ work (needs a cloneable DogmaContext + TurnManager). Until
    // then, local heuristics:
    //   • Hand-card picks default to LOWEST-age (give up the least valuable
    //     card). Almost every hand-card prompt is a "lose this card" event
    //     — return to deck, transfer to opponent, tuck under stack — so
    //     preserving high-age hand cards is the right play. (Earlier
    //     versions defaulted to highest-age on the mistaken theory that
    //     "high-age return → high-age score," but Pottery / Currency /
    //     Democracy reward by COUNT, not by age of card returned, so
    //     burning your best cards was strictly worse than giving up your
    //     worst.)
    //   • Hand-card SUBSET picks default to MaxCount of the lowest-age
    //     cards — same logic. The exception is Masonry, which melds the
    //     subset onto the player's board; for that we want HIGHEST-age
    //     castles. Detected by prompt prefix.
    //   • Yes/no defaults to true (handlers raise these for opt-in
    //     benefits); Color picks prefer tallest stack (most splay value);
    //     Score-card picks branch on whether self is the requester (give
    //     up lowest if defending a demand, take highest otherwise).
    public int? ChooseHandCard(GameState g, PlayerState self, SelectHandCardRequest req)
    {
        if (req.EligibleCardIds.Count == 0) return null;
        // Lowest-age: minimise the cost of whatever the handler does with
        // this card.
        int best = req.EligibleCardIds[0];
        int bestAge = g.Cards[best].Age;
        foreach (var id in req.EligibleCardIds)
        {
            int age = g.Cards[id].Age;
            if (age < bestAge) { bestAge = age; best = id; }
        }
        return best;
    }

    public IReadOnlyList<int> ChooseHandCardSubset(GameState g, PlayerState self, SelectHandCardSubsetRequest req)
    {
        int maxTakeable = Math.Min(req.MaxCount, req.EligibleCardIds.Count);
        int minTakeable = Math.Min(req.MinCount, maxTakeable);
        if (maxTakeable == 0) return Array.Empty<int>();

        // Masonry melds the subset onto the board; want the BEST castles,
        // not the worst. Detected by prompt prefix so we don't have to
        // thread a flag through the choice infrastructure.
        bool wantHigh = req.Prompt?.StartsWith("Masonry:") == true;

        // Democracy rewards "more cards returned than any other player" with
        // a single age-8 score. Returning the whole hand to win that reward
        // is almost never worth it — one card beats every opponent who
        // returned zero, which covers the typical case. The full
        // count-the-opposition logic would need handler state we can't see
        // from here, so default to 1; better to play it safe and miss the
        // reward sometimes than to dump the hand for it. (Bug #2.)
        if (req.Prompt?.StartsWith("Democracy:") == true)
        {
            // Take exactly 1 (or 0 if the prompt somehow allows that and
            // there's nothing to return — defensive).
            int target = Math.Min(1, maxTakeable);
            target = Math.Max(target, minTakeable);
            var lowestOne = req.EligibleCardIds.OrderBy(id => g.Cards[id].Age).Take(target).ToList();
            return lowestOne;
        }

        var ordered = wantHigh
            ? req.EligibleCardIds.OrderByDescending(id => g.Cards[id].Age).ToList()
            : req.EligibleCardIds.OrderBy(id => g.Cards[id].Age).ToList();
        int take = Math.Max(minTakeable, maxTakeable);
        return ordered.Take(take).ToList();
    }

    public IReadOnlyList<int> ChooseScoreCardSubset(GameState g, PlayerState self, SelectScoreCardSubsetRequest req)
    {
        // Score-pile subset prompts (Combustion, Databases) give cards away;
        // surrender the LOWEST-age cards to minimise damage.
        int maxTakeable = Math.Min(req.MaxCount, req.EligibleCardIds.Count);
        int minTakeable = Math.Min(req.MinCount, maxTakeable);
        if (maxTakeable == 0) return Array.Empty<int>();
        var ordered = req.EligibleCardIds.OrderBy(id => g.Cards[id].Age).ToList();
        int take = Math.Max(minTakeable, maxTakeable == 0 ? 0 : minTakeable);
        // Default to MinCount when the prompt allows declining beyond it
        // (Databases, Combustion both fix Min=Max anyway).
        return ordered.Take(Math.Max(minTakeable, take)).ToList();
    }

    public bool ChooseYesNo(GameState g, PlayerState self, YesNoChoiceRequest req) => true;

    public CardColor? ChooseColor(GameState g, PlayerState self, SelectColorRequest req)
    {
        if (req.EligibleColors.Count == 0) return null;
        // Prefer the color with the tallest stack — most benefit from a
        // splay, biggest target for transfer-away demands is already
        // filtered out by the handler (eligible list).
        CardColor best = req.EligibleColors[0];
        int bestCount = self.Stack(best).Count;
        foreach (var c in req.EligibleColors)
        {
            int n = self.Stack(c).Count;
            if (n > bestCount) { bestCount = n; best = c; }
        }
        return best;
    }

    public IReadOnlyList<int> ChooseStackOrder(GameState g, PlayerState self, SelectStackOrderRequest req)
    {
        // Greedy default: leave the order unchanged. A real evaluator would
        // need to simulate the downstream effects of each permutation, which
        // is out of scope for depth-1 search.
        return req.CurrentOrder.ToList();
    }

    public IReadOnlyList<int> ChooseCardOrder(GameState g, PlayerState self, SelectCardOrderRequest req)
        => req.CardIds.ToList();   // no preference — input order is fine

    public int? ChooseValue(GameState g, PlayerState self, SelectValueRequest req)
    {
        if (req.EligibleValues.Count == 0) return null;
        return req.EligibleValues[0];
    }

    public int? ChooseScoreCard(GameState g, PlayerState self, SelectScoreCardRequest req)
    {
        if (req.EligibleCardIds.Count == 0) return null;
        // If the prompt targets this player, they're the ones losing the
        // card (demand) — give up the lowest-age one. If the prompt is on
        // the activator (Optics, Pirate Code effect 2), the AI is picking
        // to take/keep and wants the highest. We can't tell from req
        // alone, so default to LOWEST when self matches the request (the
        // defender case) and HIGHEST otherwise.
        bool defending = req.PlayerIndex == self.Index;
        var ordered = defending
            ? req.EligibleCardIds.OrderBy(id => g.Cards[id].Age)
            : req.EligibleCardIds.OrderByDescending(id => g.Cards[id].Age);
        return ordered.First();
    }
}
