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

    public GreedyController(Random rng) => _rollout = new RandomController(rng);
    public GreedyController(int seed) : this(new Random(seed)) { }
    public GreedyController() : this(new Random()) { }

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

        PlayerAction best = legal[0];
        long bestScore = long.MinValue;
        foreach (var action in legal)
        {
            long score = TryAction(g, self.Index, action);
            if (score > bestScore) { bestScore = score; best = action; }
        }
        return best;
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

        // Every seat in the trial uses the rollout policy — the greedy
        // controller does not recurse into deeper search.
        var rollouts = new IPlayerController[clone.Players.Length];
        for (int i = 0; i < rollouts.Length; i++) rollouts[i] = _rollout;

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
    // then, local heuristics: assume any prompt the handler bothered to
    // raise is offering a benefit when accepted, so ACCEPT, and pick the
    // choice that usually maxes value — highest-age hand card (biggest
    // score off Agriculture / Mathematics / Pottery), highest-age score
    // card (biggest gain off Optics-style pulls), every color that splays
    // right, and yes to every optional yes/no.
    public int? ChooseHandCard(GameState g, PlayerState self, SelectHandCardRequest req)
    {
        if (req.EligibleCardIds.Count == 0) return null;
        int best = req.EligibleCardIds[0];
        int bestAge = g.Cards[best].Age;
        foreach (var id in req.EligibleCardIds)
        {
            int age = g.Cards[id].Age;
            if (age > bestAge) { bestAge = age; best = id; }
        }
        return best;
    }

    public IReadOnlyList<int> ChooseHandCardSubset(GameState g, PlayerState self, SelectHandCardSubsetRequest req)
    {
        int maxTakeable = Math.Min(req.MaxCount, req.EligibleCardIds.Count);
        int minTakeable = Math.Min(req.MinCount, maxTakeable);
        if (maxTakeable == 0) return Array.Empty<int>();
        // Greedy default: take MaxCount highest-age cards. Works for
        // Pottery (more returns = higher-age score), Democracy (more
        // returns = the 8-reward), Masonry (castles = Empire progress).
        var ordered = req.EligibleCardIds.OrderByDescending(id => g.Cards[id].Age).ToList();
        int take = Math.Max(minTakeable, maxTakeable);
        return ordered.Take(take).ToList();
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
