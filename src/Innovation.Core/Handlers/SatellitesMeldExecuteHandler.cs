namespace Innovation.Core.Handlers;

/// <summary>
/// Satellites (age 9, Green/Clock) — effect 3: "Meld a card from your hand
/// and then execute each of its non-demand dogma effects for yourself only."
/// </summary>
public sealed class SatellitesMeldExecuteHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (target.Hand.Count == 0) return false;

        if (ctx.PendingChoice is null)
        {
            ctx.PendingChoice = new SelectHandCardRequest
            {
                Prompt = "Satellites: meld a card and execute its non-demand effects for yourself.",
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
        Mechanics.Meld(g, target, id);
        if (g.IsGameOver) return true;
        Mechanics.ExecuteSelfOnly(ctx, id, target);
        return true;
    }
}
