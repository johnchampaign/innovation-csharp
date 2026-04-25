namespace Innovation.Core.Handlers;

/// <summary>
/// Feudalism (age 3, Purple/Castle) — non-demand: "You may splay your
/// yellow or purple cards left."
/// </summary>
public sealed class FeudalismSplayHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (ctx.PendingChoice is null)
        {
            var eligible = new List<CardColor>();
            foreach (var c in new[] { CardColor.Yellow, CardColor.Purple })
            {
                var s = target.Stack(c);
                if (s.Count >= 2 && s.Splay != Splay.Left) eligible.Add(c);
            }
            if (eligible.Count == 0) return false;

            ctx.PendingChoice = new SelectColorRequest
            {
                Prompt = "Feudalism: splay your yellow or purple cards left?",
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
