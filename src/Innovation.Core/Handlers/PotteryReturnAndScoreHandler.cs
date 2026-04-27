namespace Innovation.Core.Handlers;

/// <summary>
/// Pottery first effect (age 1, Blue/Leaf): "You may return up to three cards
/// from your hand. If you returned any cards, draw and score a card of value
/// equal to the number of cards you returned."
///
/// Mirrors VB6 main.frm 4497–4515. The second Pottery effect ("Draw a 1.")
/// is wired separately via <see cref="DrawHandler"/> in
/// <see cref="CardRegistrations"/>.
/// </summary>
public sealed class PotteryReturnAndScoreHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        // Empty hand: VB6 short-circuits (`If size2(hand, player) > 0`).
        if (target.Hand.Count == 0) return false;

        if (ctx.PendingChoice is null)
        {
            ctx.PendingChoice = new SelectHandCardSubsetRequest
            {
                Prompt = "Pottery: return up to three cards from your hand. "
                       + "Draw and score a card of age equal to the count.",
                PlayerIndex = target.Index,
                EligibleCardIds = target.Hand.ToArray(),
                MinCount = 0,
                MaxCount = Math.Min(3, target.Hand.Count),
            };
            ctx.Paused = true;
            return false;
        }

        if (ctx.PendingChoice is SelectHandCardSubsetRequest subset)
        {
            ctx.PendingChoice = null;
            var picks = subset.ChosenCardIds.ToArray();
            if (picks.Length == 0) return false;
            if (picks.Length == 1)
            {
                ApplyReturnsAndScore(g, target, picks);
                return true;
            }
            ctx.HandlerState = picks;
            ctx.PendingChoice = new SelectCardOrderRequest
            {
                Prompt = "Pottery: choose the return order (last-returned sits on top of its age deck).",
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
        // ids is the player's chosen order top-first; reverse so the last
        // returned ends up on top of the deck (= first in the chosen order).
        for (int i = ids.Count - 1; i >= 0; i--)
            Mechanics.Return(g, target, ids[i]);
        int drawn = Mechanics.DrawFromAge(g, target, ids.Count);
        if (drawn < 0 || g.IsGameOver) return;
        Mechanics.Score(g, target, drawn);
    }
}
