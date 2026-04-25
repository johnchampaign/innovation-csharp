namespace Innovation.Core.Handlers;

/// <summary>
/// Publications effect 2 (age 7, Blue/Lightbulb): "You may splay your
/// yellow or blue cards up."
/// </summary>
public sealed class PublicationsSplayHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (ctx.PendingChoice is null)
        {
            var eligible = new List<CardColor>();
            if (target.Stack(CardColor.Yellow).Count >= 2) eligible.Add(CardColor.Yellow);
            if (target.Stack(CardColor.Blue).Count >= 2) eligible.Add(CardColor.Blue);
            if (eligible.Count == 0) return false;
            ctx.PendingChoice = new SelectColorRequest
            {
                Prompt = "Publications: splay yellow or blue up?",
                PlayerIndex = target.Index,
                EligibleColors = eligible,
                AllowNone = true,
            };
            ctx.Paused = true;
            return false;
        }

        var req = (SelectColorRequest)ctx.PendingChoice;
        ctx.PendingChoice = null;
        if (req.ChosenColor is not CardColor col) return false;
        return Mechanics.Splay(g, target, col, Splay.Up);
    }
}
