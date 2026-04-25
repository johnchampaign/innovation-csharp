namespace Innovation.Core.Handlers;

/// <summary>
/// Banking (age 5, Green/Crown) — demand: "I demand you transfer a top
/// non-green card with a [Factory] from your board to my board! If you
/// do, draw and score a 5."
///
/// The activator performs the draw-and-score 5 when a card actually
/// moves.
/// </summary>
public sealed class BankingDemandHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        var activator = g.Players[ctx.ActivatingPlayerIndex];

        if (ctx.HandlerState is null && ctx.PendingChoice is null)
        {
            var eligible = new List<CardColor>();
            foreach (CardColor c in Enum.GetValues<CardColor>())
            {
                if (c == CardColor.Green) continue;
                var s = target.Stack(c);
                if (s.IsEmpty) continue;
                if (Mechanics.HasIcon(g.Cards[s.Top], Icon.Factory)) eligible.Add(c);
            }
            if (eligible.Count == 0) return false;

            ctx.HandlerState = new object();
            ctx.PendingChoice = new SelectColorRequest
            {
                Prompt = $"Banking: transfer a top non-green [Factory] card to "
                       + $"player {ctx.ActivatingPlayerIndex + 1}'s board.",
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
        // "If you do, draw and score a 5" — "you" is the demand target.
        Mechanics.DrawAndScore(g, target, 5);
        return true;
    }
}
