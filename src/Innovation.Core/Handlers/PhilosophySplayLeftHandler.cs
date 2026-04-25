namespace Innovation.Core.Handlers;

/// <summary>
/// Philosophy (age 2, Purple/Lightbulb) — effect 1: "You may splay left
/// any one color of your cards."
///
/// Eligible colors are those whose pile has ≥2 cards and isn't already
/// left-splayed. If none are eligible the effect is skipped silently;
/// otherwise the target gets a <see cref="SelectColorRequest"/> with
/// <see cref="SelectColorRequest.AllowNone"/>=true (the rule is
/// optional).
/// </summary>
public sealed class PhilosophySplayLeftHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (ctx.PendingChoice is null)
        {
            var eligible = new List<CardColor>();
            foreach (CardColor c in Enum.GetValues<CardColor>())
            {
                var stack = target.Stack(c);
                if (stack.Count >= 2 && stack.Splay != Splay.Left)
                    eligible.Add(c);
            }
            if (eligible.Count == 0) return false;

            ctx.PendingChoice = new SelectColorRequest
            {
                Prompt = "Philosophy: splay a color left?",
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
        return Mechanics.Splay(g, target, color, Splay.Left);
    }
}
