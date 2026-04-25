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

        var req = (SelectHandCardSubsetRequest)ctx.PendingChoice;
        ctx.PendingChoice = null;

        int returned = req.ChosenCardIds.Count;
        if (returned == 0) return false;   // declined — no progress

        foreach (var id in req.ChosenCardIds)
            Mechanics.Return(g, target, id);

        // Draw-and-score at age = count (1–3 since the request capped at 3).
        int drawn = Mechanics.DrawFromAge(g, target, returned);
        if (drawn < 0 || g.IsGameOver) return true;
        Mechanics.Score(g, target, drawn);
        return true;
    }
}
