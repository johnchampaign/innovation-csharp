namespace Innovation.Core.Handlers;

/// <summary>
/// Metric System (age 6, Green/Crown) — effect 1: "If your green
/// cards are splayed right, you may splay any color of your cards
/// right."
/// </summary>
public sealed class MetricSystemAnyColorHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (target.Stack(CardColor.Green).Splay != Splay.Right) return false;

        if (ctx.PendingChoice is null)
        {
            var eligible = new List<CardColor>();
            foreach (CardColor c in Enum.GetValues<CardColor>())
            {
                var s = target.Stack(c);
                if (s.Count >= 2 && s.Splay != Splay.Right) eligible.Add(c);
            }
            if (eligible.Count == 0) return false;

            ctx.PendingChoice = new SelectColorRequest
            {
                Prompt = "Metric System: splay any one color right?",
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
        return Mechanics.Splay(g, target, color, Splay.Right);
    }
}
