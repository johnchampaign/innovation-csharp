namespace Innovation.Core.Handlers;

/// <summary>
/// Engineering (age 3, Red/Castle) — non-demand: "You may splay your red
/// cards left."
/// </summary>
public sealed class EngineeringSplayHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        var s = target.Stack(CardColor.Red);
        if (s.Count < 2 || s.Splay == Splay.Left) return false;

        if (ctx.PendingChoice is null)
        {
            ctx.PendingChoice = new YesNoChoiceRequest
            {
                Prompt = "Engineering: splay your red cards left?",
                PlayerIndex = target.Index,
            };
            ctx.Paused = true;
            return false;
        }

        var req = (YesNoChoiceRequest)ctx.PendingChoice;
        ctx.PendingChoice = null;
        if (!req.ChosenYes) return false;
        return Mechanics.Splay(g, target, CardColor.Red, Splay.Left);
    }
}
