namespace Innovation.Core.Handlers;

/// <summary>
/// Engineering (age 3, Red/Castle) — demand: "I demand you transfer all
/// top cards with a [Castle] from your board to my score pile!"
///
/// Every color whose top card displays a Castle icon is transferred;
/// target has no choice. No Monument bump (transfer, not score).
/// </summary>
public sealed class EngineeringDemandHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        var activator = g.Players[ctx.ActivatingPlayerIndex];
        bool any = false;
        foreach (CardColor c in Enum.GetValues<CardColor>())
        {
            var s = target.Stack(c);
            if (s.IsEmpty) continue;
            if (!FeudalismDemandHandler.HasIcon(g.Cards[s.Top], Icon.Castle)) continue;
            Mechanics.TransferBoardToScore(g, target, activator, c);
            any = true;
            if (g.IsGameOver) return true;
        }
        if (any) ctx.DemandSuccessful = true;
        return any;
    }
}
