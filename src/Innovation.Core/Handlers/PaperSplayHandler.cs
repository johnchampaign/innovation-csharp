namespace Innovation.Core.Handlers;

/// <summary>
/// Paper (age 3, Green/Lightbulb) — effect 1: "You may splay your green
/// or blue cards left."
///
/// Eligible = whichever of {Green, Blue} has ≥2 cards and isn't already
/// left-splayed. Optional: the target may decline.
/// </summary>
public sealed class PaperSplayHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (ctx.PendingChoice is null)
        {
            var eligible = new List<CardColor>();
            foreach (var c in new[] { CardColor.Green, CardColor.Blue })
            {
                var s = target.Stack(c);
                if (s.Count >= 2 && s.Splay != Splay.Left) eligible.Add(c);
            }
            if (eligible.Count == 0) return false;

            ctx.PendingChoice = new SelectColorRequest
            {
                Prompt = "Paper: splay your green or blue cards left?",
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
