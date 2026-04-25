namespace Innovation.Core.Handlers;

/// <summary>
/// Enterprise (age 4, Purple/Crown) — non-demand: "You may splay your
/// green cards right."
/// </summary>
public sealed class EnterpriseSplayHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        var s = target.Stack(CardColor.Green);
        if (s.Count < 2 || s.Splay == Splay.Right) return false;

        if (ctx.PendingChoice is null)
        {
            ctx.PendingChoice = new YesNoChoiceRequest
            {
                Prompt = "Enterprise: splay your green cards right?",
                PlayerIndex = target.Index,
            };
            ctx.Paused = true;
            return false;
        }

        var req = (YesNoChoiceRequest)ctx.PendingChoice;
        ctx.PendingChoice = null;
        if (!req.ChosenYes) return false;
        return Mechanics.Splay(g, target, CardColor.Green, Splay.Right);
    }
}
