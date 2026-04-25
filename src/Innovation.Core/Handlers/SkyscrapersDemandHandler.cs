namespace Innovation.Core.Handlers;

/// <summary>
/// Skyscrapers (age 8, Yellow/Crown) — demand: "I demand you transfer a top
/// non-yellow card with a [Clock] from your board to my board! If you do,
/// score the card beneath it, and return all other cards from that pile!"
///
/// After the top transfers to the activator's board, the target's former
/// pile is processed: the new top (card directly beneath the transferred
/// one) is scored by the target, and any remaining cards in that pile are
/// returned to their age decks.
/// </summary>
public sealed class SkyscrapersDemandHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        var activator = g.Players[ctx.ActivatingPlayerIndex];

        if (ctx.HandlerState is null && ctx.PendingChoice is null)
        {
            var eligible = new List<CardColor>();
            foreach (CardColor c in Enum.GetValues<CardColor>())
            {
                if (c == CardColor.Yellow) continue;
                var s = target.Stack(c);
                if (s.IsEmpty) continue;
                if (Mechanics.HasIcon(g.Cards[s.Top], Icon.Clock)) eligible.Add(c);
            }
            if (eligible.Count == 0) return false;

            if (eligible.Count == 1)
            {
                ApplyTransfer(g, target, activator, eligible[0]);
                ctx.DemandSuccessful = true;
                return true;
            }

            ctx.HandlerState = new object();
            ctx.PendingChoice = new SelectColorRequest
            {
                Prompt = "Skyscrapers: choose a top non-yellow [Clock] card to transfer.",
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

        ApplyTransfer(g, target, activator, color);
        ctx.DemandSuccessful = true;
        return true;
    }

    private static void ApplyTransfer(GameState g, PlayerState target, PlayerState activator, CardColor color)
    {
        Mechanics.TransferBoardToBoard(g, target, activator, color);
        if (g.IsGameOver) return;

        var stack = target.Stack(color);
        if (stack.IsEmpty) return;

        int beneath = stack.Top;
        Mechanics.ScoreFromBoard(g, target, color, beneath);
        if (g.IsGameOver) return;

        // Return any remaining cards in that pile (from top down).
        while (!stack.IsEmpty)
        {
            int id = stack.PopTop();
            int age = g.Cards[id].Age;
            g.Decks[age].Add(id);
            GameLog.Log($"{GameLog.P(target)} returns {GameLog.C(g, id)} from board (Skyscrapers)");
        }
        SpecialAchievements.CheckAll(g);
    }
}
