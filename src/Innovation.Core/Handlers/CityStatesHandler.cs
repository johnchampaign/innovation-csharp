namespace Innovation.Core.Handlers;

/// <summary>
/// City States (age 1, Purple/Crown, <b>demand</b>): "I demand you
/// transfer a top card with a [Castle] from your board to my board if
/// you have at least four [Castle] icons on your board! If you do,
/// draw a 1."
///
/// Mirrors VB6 main.frm 4294–4317. Three gates:
///   1. Target must have ≥ 4 total Castle icons on their board.
///   2. Target must have at least one top card displaying a Castle.
///   3. Target picks which top-castle to give up (rulebook default: the
///      defender chooses). VB6 AI auto-picks the lowest-age top-castle,
///      which is the defender-friendly pick; we always raise the choice
///      so the caller decides.
///
/// After the transfer, the target draws a 1.
/// </summary>
public sealed class CityStatesHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        // Cold entry.
        if (ctx.HandlerState is null && ctx.PendingChoice is null)
        {
            int totalCastles = IconCounter.Count(target, Icon.Castle, g.Cards);
            if (totalCastles < 4) return false;

            // Colors whose top card displays a Castle.
            var eligibleColors = new List<CardColor>();
            foreach (CardColor c in Enum.GetValues<CardColor>())
            {
                var stack = target.Stack(c);
                if (stack.IsEmpty) continue;
                if (HasCastle(g.Cards[stack.Top])) eligibleColors.Add(c);
            }
            if (eligibleColors.Count == 0) return false;

            ctx.HandlerState = new object();   // sentinel
            ctx.PendingChoice = new SelectColorRequest
            {
                Prompt = $"City States: transfer one of your top [Castle] "
                       + $"cards to player {ctx.ActivatingPlayerIndex + 1}'s board.",
                PlayerIndex = target.Index,
                EligibleColors = eligibleColors,
            };
            ctx.Paused = true;
            return false;
        }

        // Resume: apply transfer, then target draws a 1.
        var req = (SelectColorRequest)ctx.PendingChoice!;
        ctx.PendingChoice = null;
        ctx.HandlerState = null;

        if (req.ChosenColor is not CardColor color) return false;

        var activator = g.Players[ctx.ActivatingPlayerIndex];
        bool moved = Mechanics.TransferBoardToBoard(g, target, activator, color);
        if (!moved) return false;   // defensive — shouldn't happen after color filter
        ctx.DemandSuccessful = true;
        if (g.IsGameOver) return true;

        Mechanics.DrawFromAge(g, target, 1);
        return true;
    }

    private static bool HasCastle(Card c) =>
        c.Top == Icon.Castle || c.Left == Icon.Castle ||
        c.Middle == Icon.Castle || c.Right == Icon.Castle;
}
