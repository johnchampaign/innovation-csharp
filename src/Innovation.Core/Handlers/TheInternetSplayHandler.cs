namespace Innovation.Core.Handlers;

/// <summary>
/// The Internet (age 10, Purple/Clock) — effect 1: "You may splay your
/// green cards up."
/// </summary>
public sealed class TheInternetSplayHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        var s = target.Stack(CardColor.Green);
        if (s.Count < 2 || s.Splay == Splay.Up) return false;

        if (ctx.PendingChoice is null)
        {
            ctx.PendingChoice = new YesNoChoiceRequest
            {
                Prompt = "The Internet: splay your green cards up?",
                PlayerIndex = target.Index,
            };
            ctx.Paused = true;
            return false;
        }

        var yn = (YesNoChoiceRequest)ctx.PendingChoice;
        ctx.PendingChoice = null;
        if (!yn.ChosenYes) return false;
        return Mechanics.Splay(g, target, CardColor.Green, Splay.Up);
    }
}
