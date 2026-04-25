namespace Innovation.Core.Handlers;

/// <summary>
/// Metric System (age 6, Green/Crown) — effect 2: "You may splay
/// your green cards right."
/// </summary>
public sealed class MetricSystemSplayGreenHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        var s = target.Stack(CardColor.Green);
        if (s.Count < 2 || s.Splay == Splay.Right) return false;

        if (ctx.PendingChoice is null)
        {
            ctx.PendingChoice = new YesNoChoiceRequest
            {
                Prompt = "Metric System: splay your green cards right?",
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
