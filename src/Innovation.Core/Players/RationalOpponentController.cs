namespace Innovation.Core.Players;

/// <summary>
/// Rollout controller used by <see cref="GreedyController"/> to model
/// OPPONENTS during its trial. The principle: opponents make locally-
/// rational choices for each prompt, sharing when sharing is broadly
/// beneficial but not over-committing the hand for share-baiting cards.
///
/// Concretely:
///   • Yes/No prompts default to true. Most "you may X" effects offer
///     benefits with small or no costs (free splays, free melds, etc.).
///   • Hand-card subset prompts return MinCount (lowest-age) when forced
///     (Min &gt; 0); when optional (Min = 0), return 1 — enough to
///     trigger the share's reward without dumping the hand. This avoids
///     both extremes: pessimistic (decline = miss free shares) and
///     greedy-max (dump-hand = wasteful share-baiting).
///   • Single-hand-card prompts pick the lowest-age card even when
///     optional. The opponent benefits from sharing a typical
///     return-and-draw, and a lone low-age card is cheap to give up.
///   • Demand-style forced prompts give up the lowest-age card (when
///     the player is the demand defender).
///
/// Replaces the older PessimisticOpponentController, which declined
/// every voluntary share and made the AI under-value rational
/// opponents' contributions (caused #1: Mathematics shared by opponent
/// for asymmetric gain). Tradeoff: the AI may now slightly over-value
/// share-bait cards where opponents would actually decline; that's the
/// less-bad failure mode and is bounded by rational subset sizes.
/// </summary>
internal sealed class RationalOpponentController : IPlayerController
{
    private readonly RandomController _fallback;

    public RationalOpponentController(Random rng) => _fallback = new RandomController(rng);

    // Most yes/no's gate a benefit. Accept by default.
    public bool ChooseYesNo(GameState g, PlayerState self, YesNoChoiceRequest req) => true;

    public IReadOnlyList<int> ChooseHandCardSubset(GameState g, PlayerState self, SelectHandCardSubsetRequest req)
    {
        int max = Math.Min(req.MaxCount, req.EligibleCardIds.Count);
        if (max == 0) return Array.Empty<int>();
        // For optional shares (Min = 0), commit just one card — enough
        // to trigger the share's reward without dumping the whole hand.
        // For forced subsets (Min > 0, demands), give up exactly Min.
        int target = req.MinCount == 0 ? Math.Min(1, max) : Math.Min(req.MinCount, max);
        return req.EligibleCardIds
            .OrderBy(id => g.Cards[id].Age)
            .Take(target)
            .ToList();
    }

    public IReadOnlyList<int> ChooseScoreCardSubset(GameState g, PlayerState self, SelectScoreCardSubsetRequest req)
    {
        // Same logic for score-pile subsets (Combustion, Databases — both
        // demands with Min = Max, so this just gives up the lowest).
        int max = Math.Min(req.MaxCount, req.EligibleCardIds.Count);
        if (max == 0) return Array.Empty<int>();
        int target = req.MinCount == 0 ? Math.Min(1, max) : Math.Min(req.MinCount, max);
        return req.EligibleCardIds
            .OrderBy(id => g.Cards[id].Age)
            .Take(target)
            .ToList();
    }

    public int? ChooseHandCard(GameState g, PlayerState self, SelectHandCardRequest req)
    {
        if (req.EligibleCardIds.Count == 0) return null;
        // Pick the lowest-age card. For optional shares (Mathematics: "you
        // may return a card from your hand. Draw and meld a card of value
        // one higher"), this commits a cheap card and reaps the free meld.
        // For demands, gives up the cheapest.
        return req.EligibleCardIds.OrderBy(id => g.Cards[id].Age).First();
    }

    public int? ChooseScoreCard(GameState g, PlayerState self, SelectScoreCardRequest req)
    {
        if (req.EligibleCardIds.Count == 0) return null;
        // Defending (giving up a score card) → low-age. Note that some
        // SelectScoreCardRequest sites are board picks (Globalization /
        // Self Service / Services / Fission / Bioengineering reuse this
        // request type for board-card selection) — there low-age may not
        // be ideal, but those handlers raise this prompt at the activator,
        // not opponents, so we rarely hit them in opponent rollouts.
        return req.EligibleCardIds.OrderBy(id => g.Cards[id].Age).First();
    }

    public CardColor? ChooseColor(GameState g, PlayerState self, SelectColorRequest req)
    {
        if (req.EligibleColors.Count == 0) return null;
        // Forced color pick: give up the lowest-age top card. Optional
        // pick: still pick the smallest cost; declining (null) when
        // AllowNone wastes the opportunity for free icon swaps and
        // splays the opponent benefits from.
        CardColor best = req.EligibleColors[0];
        int bestAge = self.Stack(best).IsEmpty ? int.MaxValue : g.Cards[self.Stack(best).Top].Age;
        foreach (var c in req.EligibleColors)
        {
            var s = self.Stack(c);
            int age = s.IsEmpty ? int.MaxValue : g.Cards[s.Top].Age;
            if (age < bestAge) { bestAge = age; best = c; }
        }
        return best;
    }

    public int? ChooseValue(GameState g, PlayerState self, SelectValueRequest req)
    {
        if (req.AllowNone) return null;
        return req.EligibleValues.Count > 0 ? req.EligibleValues[0] : null;
    }

    // Plumbing forwards. ChooseAction and ChooseInitialMeld aren't called
    // for opponents during AI trials (the trial only resolves in-dogma
    // prompts), but defensive fallbacks keep the controller well-formed
    // if used elsewhere.
    public int ChooseInitialMeld(GameState g, PlayerState self) => _fallback.ChooseInitialMeld(g, self);
    public PlayerAction ChooseAction(GameState g, PlayerState self, IReadOnlyList<PlayerAction> legal)
        => _fallback.ChooseAction(g, self, legal);
    public IReadOnlyList<int> ChooseStackOrder(GameState g, PlayerState self, SelectStackOrderRequest req)
        => req.CurrentOrder.ToList();
    public IReadOnlyList<int> ChooseCardOrder(GameState g, PlayerState self, SelectCardOrderRequest req)
        => req.CardIds.ToList();
}
