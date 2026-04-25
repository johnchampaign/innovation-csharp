namespace Innovation.Core.Handlers;

/// <summary>
/// Philosophy (age 2, Purple/Lightbulb) — effect 2: "You may score a card
/// from your hand."
/// </summary>
public sealed class PhilosophyScoreHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (target.Hand.Count == 0) return false;

        if (ctx.PendingChoice is null)
        {
            ctx.PendingChoice = new SelectHandCardRequest
            {
                Prompt = "Philosophy: score a card from your hand?",
                PlayerIndex = target.Index,
                EligibleCardIds = target.Hand.ToArray(),
                AllowNone = true,
            };
            ctx.Paused = true;
            return false;
        }

        var req = (SelectHandCardRequest)ctx.PendingChoice;
        ctx.PendingChoice = null;

        if (req.ChosenCardId is not int chosen) return false;
        Mechanics.Score(g, target, chosen);
        return true;
    }
}
