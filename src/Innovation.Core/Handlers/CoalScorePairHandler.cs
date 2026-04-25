namespace Innovation.Core.Handlers;

/// <summary>
/// Coal (age 5, Red/Factory) — effect 3: "You may score any one of your
/// top cards. If you do, also score the card beneath it."
///
/// Target picks a color; that color's top card is scored, then (if the
/// stack had ≥2 cards before) the newly-exposed card is also scored.
/// Both scores go through <see cref="Mechanics.ScoreFromBoard"/> so they
/// count toward Monument.
/// </summary>
public sealed class CoalScorePairHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (ctx.PendingChoice is null)
        {
            var eligible = new List<CardColor>();
            foreach (CardColor c in Enum.GetValues<CardColor>())
                if (!target.Stack(c).IsEmpty) eligible.Add(c);
            if (eligible.Count == 0) return false;

            ctx.PendingChoice = new SelectColorRequest
            {
                Prompt = "Coal: score a top card (and the card beneath it)?",
                PlayerIndex = target.Index,
                EligibleColors = eligible,
                AllowNone = true,
            };
            ctx.Paused = true;
            return false;
        }

        var req = (SelectColorRequest)ctx.PendingChoice;
        ctx.PendingChoice = null;
        if (req.ChosenColor is not CardColor color) return false;

        var stack = target.Stack(color);
        if (stack.IsEmpty) return false;

        int top = stack.Top;
        Mechanics.ScoreFromBoard(g, target, color, top);
        if (g.IsGameOver) return true;
        if (!stack.IsEmpty)
        {
            int next = stack.Top;
            Mechanics.ScoreFromBoard(g, target, color, next);
        }
        return true;
    }
}
