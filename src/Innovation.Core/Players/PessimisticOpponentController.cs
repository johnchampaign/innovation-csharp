namespace Innovation.Core.Players;

/// <summary>
/// Adversarial rollout controller used by <see cref="GreedyController"/> to
/// model OPPONENTS during its one-ply trial. The principle: when an
/// opponent has any latitude to NOT help the activator, they refuse.
///
/// Concretely, this controller:
///   • Declines every yes/no prompt — defaults to "no" / "skip".
///   • Returns the minimum allowed subset (typically empty) for any
///     subset prompt — modeling "don't share, don't progress the share
///     effect, deny the activator's share-bonus draw".
///   • For demand prompts that force a non-zero pick (Min &gt; 0), gives
///     up the lowest-age cards (least valuable to the demander).
///   • For non-share prompts that aren't binary, falls back to random.
///
/// The result is that <see cref="GreedyController"/>'s simulated activator
/// only sees value from its OWN execution of the dogma — not from the
/// hopeful payoff of an opponent obligingly sharing. This stops the AI
/// from "share-baiting" cards like Currency where its only realistic gain
/// is whatever the activator does with their own hand.
/// </summary>
internal sealed class PessimisticOpponentController : IPlayerController
{
    private readonly RandomController _fallback;

    public PessimisticOpponentController(Random rng) => _fallback = new RandomController(rng);

    // Decline yes/no.
    public bool ChooseYesNo(GameState g, PlayerState self, YesNoChoiceRequest req) => false;

    // Empty (or minimum required) subset. Share prompts have Min=0, so we
    // return []; demands fix Min=Max so we return Min cards (lowest-age).
    public IReadOnlyList<int> ChooseHandCardSubset(GameState g, PlayerState self, SelectHandCardSubsetRequest req)
    {
        int min = Math.Min(req.MinCount, req.EligibleCardIds.Count);
        if (min == 0) return Array.Empty<int>();
        return req.EligibleCardIds
            .OrderBy(id => g.Cards[id].Age)
            .Take(min)
            .ToList();
    }

    public IReadOnlyList<int> ChooseScoreCardSubset(GameState g, PlayerState self, SelectScoreCardSubsetRequest req)
    {
        int min = Math.Min(req.MinCount, req.EligibleCardIds.Count);
        if (min == 0) return Array.Empty<int>();
        return req.EligibleCardIds
            .OrderBy(id => g.Cards[id].Age)
            .Take(min)
            .ToList();
    }

    // Decline optional hand picks. If forced (AllowNone=false), give up the
    // lowest-age card.
    public int? ChooseHandCard(GameState g, PlayerState self, SelectHandCardRequest req)
    {
        if (req.AllowNone) return null;
        if (req.EligibleCardIds.Count == 0) return null;
        return req.EligibleCardIds.OrderBy(id => g.Cards[id].Age).First();
    }

    public int? ChooseScoreCard(GameState g, PlayerState self, SelectScoreCardRequest req)
    {
        if (req.AllowNone) return null;
        if (req.EligibleCardIds.Count == 0) return null;
        return req.EligibleCardIds.OrderBy(id => g.Cards[id].Age).First();
    }

    public CardColor? ChooseColor(GameState g, PlayerState self, SelectColorRequest req)
    {
        if (req.AllowNone) return null;
        if (req.EligibleColors.Count == 0) return null;
        // Forced color pick: give up the lowest-age top card if any color
        // has cards. Otherwise just take the first.
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

    // Plumbing forwards.
    public int ChooseInitialMeld(GameState g, PlayerState self) => _fallback.ChooseInitialMeld(g, self);
    public PlayerAction ChooseAction(GameState g, PlayerState self, IReadOnlyList<PlayerAction> legal)
        => _fallback.ChooseAction(g, self, legal);
    public IReadOnlyList<int> ChooseStackOrder(GameState g, PlayerState self, SelectStackOrderRequest req)
        => req.CurrentOrder.ToList();
    public IReadOnlyList<int> ChooseCardOrder(GameState g, PlayerState self, SelectCardOrderRequest req)
        => req.CardIds.ToList();
}
