namespace Innovation.Core.Handlers;

/// <summary>
/// Currency (age 2, Green/Crown): "You may return any number of cards from
/// your hand. If you do, draw and score a 2 for every different value of
/// card you returned."
///
/// "Different value" means distinct ages — returning two 1s and a 3 yields
/// two draw-and-scores, not three. Cards go to the SCORE pile, not the
/// hand. Pattern follows Pottery: subset prompt → return each → derive
/// distinct-age count → draw-and-score that many 2s.
/// </summary>
public sealed class CurrencyHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (target.Hand.Count == 0) return false;

        if (ctx.PendingChoice is null)
        {
            ctx.PendingChoice = new SelectHandCardSubsetRequest
            {
                Prompt = "Currency: return any number of cards from your hand. "
                       + "Draw and score a 2 for each distinct age among cards returned.",
                PlayerIndex = target.Index,
                EligibleCardIds = target.Hand.ToArray(),
                MinCount = 0,
                MaxCount = target.Hand.Count,
            };
            ctx.Paused = true;
            return false;
        }

        if (ctx.PendingChoice is SelectHandCardSubsetRequest subset)
        {
            ctx.PendingChoice = null;
            var picks = subset.ChosenCardIds.ToArray();
            if (picks.Length == 0) return false;
            // Returns to deck: order only matters when 2+ picks share an age
            // (otherwise each ends up on a different deck and the orderings
            // are equivalent).
            if (picks.Length == 1 || !Mechanics.OrderMatters(picks, id => g.Cards[id].Age))
            {
                ApplyReturnsAndScore(g, target, picks);
                return true;
            }
            ctx.HandlerState = picks;
            ctx.PendingChoice = new SelectCardOrderRequest
            {
                Prompt = "Currency: choose the return order (last-returned card sits on top of its age deck and will be drawn first).",
                PlayerIndex = target.Index,
                Action = "return",
                CardIds = picks,
            };
            ctx.Paused = true;
            return false;
        }

        var orderReq = (SelectCardOrderRequest)ctx.PendingChoice!;
        var input = (int[])ctx.HandlerState!;
        ctx.PendingChoice = null;
        ctx.HandlerState = null;
        var ordered = Mechanics.ValidateOrder(orderReq.ChosenOrder, input);
        ApplyReturnsAndScore(g, target, ordered);
        return ordered.Count > 0;
    }

    private static void ApplyReturnsAndScore(GameState g, PlayerState target, IReadOnlyList<int> ids)
    {
        // ids is the player's chosen order top-first (next-drawn → drawn-last).
        // Decks add to the end and pop from the end, so apply in REVERSE: the
        // last id returned ends up on top of the deck, which should be the
        // FIRST in the chosen order.
        var distinctAges = new HashSet<int>();
        for (int i = ids.Count - 1; i >= 0; i--)
        {
            int id = ids[i];
            distinctAges.Add(g.Cards[id].Age);
            Mechanics.Return(g, target, id);
        }
        for (int i = 0; i < distinctAges.Count; i++)
        {
            if (g.IsGameOver) return;
            Mechanics.DrawAndScore(g, target, 2);
        }
    }
}
