namespace Innovation.Core.Handlers;

/// <summary>
/// Flight (age 8, Red/Crown) — effect 2: "You may splay your red cards up."
/// </summary>
public sealed class FlightSplayRedHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        var stack = target.Stack(CardColor.Red);
        if (stack.Count < 2 || stack.Splay == Splay.Up) return false;

        if (ctx.PendingChoice is null)
        {
            ctx.PendingChoice = new YesNoChoiceRequest
            {
                Prompt = "Flight: splay your red cards up?",
                PlayerIndex = target.Index,
            };
            ctx.Paused = true;
            return false;
        }

        var yn = (YesNoChoiceRequest)ctx.PendingChoice;
        ctx.PendingChoice = null;
        if (!yn.ChosenYes) return false;
        return Mechanics.Splay(g, target, CardColor.Red, Splay.Up);
    }
}
