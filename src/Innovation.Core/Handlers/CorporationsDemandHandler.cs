namespace Innovation.Core.Handlers;

/// <summary>
/// Corporations (age 8, Green/Factory) — demand: "I demand you transfer a
/// top non-green card with a [Factory] from your board to my score pile!
/// If you do, draw and meld an 8."
///
/// The activator's draw-and-meld fires only when a transfer happened.
/// Ties are broken by the target (they pick the color).
/// </summary>
public sealed class CorporationsDemandHandler : IDogmaHandler
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

            if (eligible.Count == 1)
            {
                TransferAndReward(g, target, activator, eligible[0], ctx);
                return true;
            }

            ctx.HandlerState = new object();
            ctx.PendingChoice = new SelectColorRequest
            {
                Prompt = $"Corporations: transfer a top non-green [Factory] card to "
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

        TransferAndReward(g, target, activator, color, ctx);
        return true;
    }

    private static void TransferAndReward(GameState g, PlayerState target, PlayerState activator, CardColor color, DogmaContext ctx)
    {
        int moved = Mechanics.TransferBoardToScore(g, target, activator, color);
        if (moved < 0) return;
        ctx.DemandSuccessful = true;
        if (g.IsGameOver) return;
        // "If you do, draw and meld an 8" — "you" is the demand target.
        Mechanics.DrawAndMeld(g, target, 8);
    }
}
