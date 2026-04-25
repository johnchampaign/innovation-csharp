namespace Innovation.Core.Handlers;

/// <summary>
/// Satellites (age 9, Green/Clock) — effect 2: "You may splay your purple
/// cards up."
/// </summary>
public sealed class SatellitesSplayHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (target.Stack(CardColor.Purple).Count < 2) return false;

        if (ctx.PendingChoice is null)
        {
            ctx.PendingChoice = new YesNoChoiceRequest
            {
                Prompt = "Satellites: splay your purple cards up?",
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
