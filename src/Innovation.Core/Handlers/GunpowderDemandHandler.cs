namespace Innovation.Core.Handlers;

/// <summary>
/// Gunpowder (age 4, Red/Factory) — demand: "I demand you transfer a
/// top card with a [Castle] from your board to my score pile!"
///
/// Target picks the color. No Monument bump (transfer, not score).
/// Effect 2 (non-demand) reads <see cref="DogmaContext.DemandSuccessful"/>.
/// </summary>
public sealed class GunpowderDemandHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        var activator = g.Players[ctx.ActivatingPlayerIndex];

        if (ctx.HandlerState is null && ctx.PendingChoice is null)
        {
            var eligible = new List<CardColor>();
            foreach (CardColor c in Enum.GetValues<CardColor>())
            {
                var s = target.Stack(c);
                if (s.IsEmpty) continue;
                if (Mechanics.HasIcon(g.Cards[s.Top], Icon.Castle)) eligible.Add(c);
            }
            if (eligible.Count == 0) return false;

            ctx.HandlerState = new object();
            ctx.PendingChoice = new SelectColorRequest
            {
                Prompt = $"Gunpowder: transfer a top [Castle] card to "
                       + $"player {ctx.ActivatingPlayerIndex + 1}'s score pile.",
                PlayerIndex = target.Index,
                EligibleColors = eligible,
            };
            ctx.Paused = true;
            return false;
        }

        var req = (SelectColorRequest)ctx.PendingChoice!;
        ctx.PendingChoice = null;
        ctx.HandlerState = null;
        if (req.ChosenColor is not CardColor color) return false;

        int moved = Mechanics.TransferBoardToScore(g, target, activator, color);
        if (moved < 0) return false;
        ctx.DemandSuccessful = true;
        return true;
    }
}
