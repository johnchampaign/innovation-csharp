namespace Innovation.Core.Handlers;

/// <summary>
/// Chemistry (age 5, Blue/Factory) — effect 1: "You may splay your blue
/// cards right."
/// </summary>
public sealed class ChemistrySplayHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        var s = target.Stack(CardColor.Blue);
        if (s.Count < 2 || s.Splay == Splay.Right) return false;

        if (ctx.PendingChoice is null)
        {
            ctx.PendingChoice = new YesNoChoiceRequest
            {
                Prompt = "Chemistry: splay your blue cards right?",
                PlayerIndex = target.Index,
            };
            ctx.Paused = true;
            return false;
        }

        var req = (YesNoChoiceRequest)ctx.PendingChoice;
        ctx.PendingChoice = null;
        if (!req.ChosenYes) return false;
        return Mechanics.Splay(g, target, CardColor.Blue, Splay.Right);
    }
}
