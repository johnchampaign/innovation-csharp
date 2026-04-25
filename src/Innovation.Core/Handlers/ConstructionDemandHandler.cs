namespace Innovation.Core.Handlers;

/// <summary>
/// Construction first effect (age 2, Red/Castle, <b>demand</b>): "I demand
/// you transfer two cards from your hand to my hand, then draw a 2!"
///
/// Target picks which cards (defender-friendly — VB6 auto-picked lowest,
/// we raise a choice). Transfers *up to* two: if target has only 0 or 1
/// cards, transfer what they have. After transfer, the <em>target</em>
/// draws a 2 (user-confirmed; VB6 skipped this entirely — faithfulness
/// bug, corrected here).
/// </summary>
public sealed class ConstructionDemandHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (ctx.HandlerState is null && ctx.PendingChoice is null)
        {
            if (target.Hand.Count == 0) return false;
            int max = Math.Min(2, target.Hand.Count);

            ctx.HandlerState = new object();
            ctx.PendingChoice = new SelectHandCardSubsetRequest
            {
                Prompt = $"Construction: transfer up to {max} cards from your "
                       + $"hand to player {ctx.ActivatingPlayerIndex + 1}'s hand, "
                       + $"then draw a 2.",
                PlayerIndex = target.Index,
                EligibleCardIds = target.Hand.ToArray(),
                MinCount = max,
                MaxCount = max,
            };
            ctx.Paused = true;
            return false;
        }

        var req = (SelectHandCardSubsetRequest)ctx.PendingChoice!;
        ctx.PendingChoice = null;
        ctx.HandlerState = null;

        var activator = g.Players[ctx.ActivatingPlayerIndex];
        foreach (var id in req.ChosenCardIds)
            Mechanics.TransferHandToHand(g, target, activator, id);

        if (req.ChosenCardIds.Count > 0)
            ctx.DemandSuccessful = true;

        // Consolation draw for the demanded player.
        Mechanics.DrawFromAge(g, target, 2);
        return true;
    }
}
