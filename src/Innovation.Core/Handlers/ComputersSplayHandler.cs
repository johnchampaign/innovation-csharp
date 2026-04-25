namespace Innovation.Core.Handlers;

/// <summary>
/// Computers (age 9, Blue/Clock) — effect 1: "You may splay your red cards
/// or your green cards up."
/// </summary>
public sealed class ComputersSplayHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (ctx.PendingChoice is null)
        {
            var eligible = new List<CardColor>();
            foreach (var c in new[] { CardColor.Red, CardColor.Green })
            {
                var s = target.Stack(c);
                if (s.Count >= 2 && s.Splay != Splay.Up) eligible.Add(c);
            }
            if (eligible.Count == 0) return false;

            ctx.PendingChoice = new SelectColorRequest
            {
                Prompt = "Computers: splay red or green cards up?",
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
        return Mechanics.Splay(g, target, color, Splay.Up);
    }
}
