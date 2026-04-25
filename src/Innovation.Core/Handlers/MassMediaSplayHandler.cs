namespace Innovation.Core.Handlers;

/// <summary>
/// Mass Media (age 8, Green/Lightbulb) — effect 2: "You may splay your
/// purple cards up."
/// </summary>
public sealed class MassMediaSplayHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        var stack = target.Stack(CardColor.Purple);
        if (stack.Count < 2 || stack.Splay == Splay.Up) return false;

        if (ctx.PendingChoice is null)
        {
            ctx.PendingChoice = new YesNoChoiceRequest
            {
                Prompt = "Mass Media: splay your purple cards up?",
                PlayerIndex = target.Index,
            };
            ctx.Paused = true;
            return false;
        }

        var yn = (YesNoChoiceRequest)ctx.PendingChoice;
        ctx.PendingChoice = null;
        if (!yn.ChosenYes) return false;
        return Mechanics.Splay(g, target, CardColor.Purple, Splay.Up);
    }
}
