namespace Innovation.Core.Handlers;

/// <summary>Coal (age 5, Red/Factory) — effect 2: "You may splay your red cards right."</summary>
public sealed class CoalSplayHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        var s = target.Stack(CardColor.Red);
        if (s.Count < 2 || s.Splay == Splay.Right) return false;

        if (ctx.PendingChoice is null)
        {
            ctx.PendingChoice = new YesNoChoiceRequest
            {
                Prompt = "Coal: splay your red cards right?",
                PlayerIndex = target.Index,
            };
            ctx.Paused = true;
            return false;
        }

        var req = (YesNoChoiceRequest)ctx.PendingChoice;
        ctx.PendingChoice = null;
        if (!req.ChosenYes) return false;
        return Mechanics.Splay(g, target, CardColor.Red, Splay.Right);
    }
}
