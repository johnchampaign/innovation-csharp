namespace Innovation.Core.Handlers;

/// <summary>
/// Flight (age 8, Red/Crown) — effect 1: "If your red cards are splayed up,
/// you may splay any one color of your cards up."
/// </summary>
public sealed class FlightAnyColorHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (target.Stack(CardColor.Red).Splay != Splay.Up) return false;

        if (ctx.PendingChoice is null)
        {
            var eligible = new List<CardColor>();
            foreach (CardColor c in Enum.GetValues<CardColor>())
            {
                var s = target.Stack(c);
                if (s.Count >= 2 && s.Splay != Splay.Up) eligible.Add(c);
            }
            if (eligible.Count == 0) return false;

            ctx.PendingChoice = new SelectColorRequest
            {
                Prompt = "Flight: splay any one color up?",
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
