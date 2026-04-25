namespace Innovation.Core.Handlers;

/// <summary>
/// Enterprise (age 4, Purple/Crown) — demand: "I demand you transfer a
/// top non-purple card with a [Crown] from your board to my board! If
/// you do, draw and meld a 4!"
///
/// Target picks the color. The activator (not the target) performs the
/// draw-and-meld 4 if a card actually moved.
/// </summary>
public sealed class EnterpriseDemandHandler : IDogmaHandler
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
                if (Mechanics.HasIcon(g.Cards[s.Top], Icon.Crown)) eligible.Add(c);
            }
            if (eligible.Count == 0) return false;

            ctx.HandlerState = new object();
            ctx.PendingChoice = new SelectColorRequest
            {
                Prompt = $"Enterprise: transfer a top non-purple [Crown] card "
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

        // "If you do, draw and meld a 4" — "you" is the demand target.
        Mechanics.DrawAndMeld(g, target, 4);
        return true;
    }
}
