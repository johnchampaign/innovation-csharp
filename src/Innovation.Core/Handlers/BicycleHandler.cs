namespace Innovation.Core.Handlers;

/// <summary>
/// Bicycle (age 7, Green/Clock): "You may exchange all the cards in your
/// hand with all the cards in your score pile. If you exchange one, you
/// must exchange them all." Yes/no opt-in, then a wholesale swap.
/// </summary>
public sealed class BicycleHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (ctx.PendingChoice is null)
        {
            if (target.Hand.Count == 0 && target.ScorePile.Count == 0) return false;
            ctx.PendingChoice = new YesNoChoiceRequest
            {
                Prompt = "Bicycle: exchange all your hand cards with all your score-pile cards?",
                PlayerIndex = target.Index,
            };
            ctx.Paused = true;
            return false;
        }

        var req = (YesNoChoiceRequest)ctx.PendingChoice;
        ctx.PendingChoice = null;
        if (!req.ChosenYes) return false;

        var handIds = target.Hand.ToList();
        var scoreIds = target.ScorePile.ToList();
        target.Hand.Clear();
        target.ScorePile.Clear();
        foreach (var id in scoreIds)
        {
            target.Hand.Add(id);
            GameLog.Log($"{GameLog.P(target)} moves {GameLog.C(g, id)} from score pile to hand");
        }
        foreach (var id in handIds)
        {
            target.ScorePile.Add(id);
            GameLog.Log($"{GameLog.P(target)} moves {GameLog.C(g, id)} from hand to score pile");
        }
        return true;
    }
}
