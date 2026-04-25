namespace Innovation.Core.Handlers;

/// <summary>
/// Societies (age 5, Purple/Crown) — demand: "I demand you transfer a
/// top non-purple card with a [Lightbulb] from your board to my board!
/// If you do, draw a 5!"
///
/// Activator draws the 5 if a card actually moved.
/// </summary>
public sealed class SocietiesDemandHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        var activator = g.Players[ctx.ActivatingPlayerIndex];

        if (ctx.HandlerState is null && ctx.PendingChoice is null)
        {
            var eligible = new List<CardColor>();
            foreach (CardColor c in Enum.GetValues<CardColor>())
            {
                if (c == CardColor.Purple) continue;
                var s = target.Stack(c);
                if (s.IsEmpty) continue;
                if (Mechanics.HasIcon(g.Cards[s.Top], Icon.Lightbulb)) eligible.Add(c);
            }
            if (eligible.Count == 0) return false;

            ctx.HandlerState = new object();
            ctx.PendingChoice = new SelectColorRequest
            {
                Prompt = $"Societies: transfer a top non-purple [Lightbulb] card "
                       + $"to player {ctx.ActivatingPlayerIndex + 1}'s board.",
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

        if (!Mechanics.TransferBoardToBoard(g, target, activator, color)) return false;
        ctx.DemandSuccessful = true;
        if (g.IsGameOver) return true;
        // "If you do, draw a 5" — "you" is the demand target.
        Mechanics.DrawFromAge(g, target, 5);
        return true;
    }
}
