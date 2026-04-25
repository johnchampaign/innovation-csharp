namespace Innovation.Core.Handlers;

/// <summary>
/// Invention (age 4, Green/Lightbulb) — effect 1: "You may splay right
/// any one color of your cards currently splayed left. If you do, draw
/// and score a 4."
///
/// Eligibility: pile has splay=Left (by implication has ≥2 cards).
/// Optional — player may decline. Score-4 only fires if a splay
/// actually happened.
/// </summary>
public sealed class InventionSplayAndDrawHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (ctx.PendingChoice is null)
        {
            var eligible = new List<CardColor>();
            foreach (CardColor c in Enum.GetValues<CardColor>())
                if (target.Stack(c).Splay == Splay.Left) eligible.Add(c);
            if (eligible.Count == 0) return false;

            ctx.PendingChoice = new SelectColorRequest
            {
                Prompt = "Invention: re-splay a left-splayed color to the right "
                       + "(then draw and score a 4)?",
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
        if (!Mechanics.Splay(g, target, color, Splay.Right)) return false;
        if (g.IsGameOver) return true;
        Mechanics.DrawAndScore(g, target, 4);
        return true;
    }
}
