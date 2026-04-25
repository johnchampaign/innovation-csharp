namespace Innovation.Core.Handlers;

/// <summary>
/// Emancipation (age 6, Purple/Factory) — demand: "I demand you
/// transfer a card from your hand to my score pile! If you do, draw
/// a 6!"
///
/// Target picks any hand card. "If you do" refers back to "I demand
/// you" — the defender is the one who draws the 6 on success.
/// </summary>
public sealed class EmancipationDemandHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        var activator = g.Players[ctx.ActivatingPlayerIndex];
        if (target.Hand.Count == 0) return false;

        if (ctx.PendingChoice is null)
        {
            ctx.PendingChoice = new SelectHandCardRequest
            {
                Prompt = $"Emancipation: transfer a hand card to P{ctx.ActivatingPlayerIndex + 1}'s score pile.",
                PlayerIndex = target.Index,
                EligibleCardIds = target.Hand.ToArray(),
                AllowNone = false,
            };
            ctx.Paused = true;
            return false;
        }

        var req = (SelectHandCardRequest)ctx.PendingChoice;
        ctx.PendingChoice = null;
        if (req.ChosenCardId is not int id) return false;

        Mechanics.TransferHandToScore(g, target, activator, id);
        ctx.DemandSuccessful = true;
        if (g.IsGameOver) return true;
        Mechanics.DrawFromAge(g, target, 6);
        return true;
    }
}
