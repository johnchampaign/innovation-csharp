namespace Innovation.Core.Handlers;

/// <summary>
/// Industrialization (age 6, Red/Factory) — effect 2: "You may splay
/// your red or purple cards right."
/// </summary>
public sealed class IndustrializationSplayHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (ctx.PendingChoice is null)
        {
            var eligible = new List<CardColor>();
            foreach (var c in new[] { CardColor.Red, CardColor.Purple })
            {
                var s = target.Stack(c);
                if (s.Count >= 2 && s.Splay != Splay.Right) eligible.Add(c);
            }
            if (eligible.Count == 0) return false;

            ctx.PendingChoice = new SelectColorRequest
            {
                Prompt = "Industrialization: splay your red or purple cards right?",
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
