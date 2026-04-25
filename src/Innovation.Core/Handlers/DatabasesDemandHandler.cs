namespace Innovation.Core.Handlers;

/// <summary>
/// Databases (age 10, Green/Clock) — demand: "Return half (rounded up) of
/// the cards in your score pile!"
/// </summary>
public sealed class DatabasesDemandHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        int n = target.ScorePile.Count;
        if (n == 0) return false;
        int toReturn = (n + 1) / 2;

        if (ctx.PendingChoice is null)
        {
            ctx.PendingChoice = new SelectScoreCardSubsetRequest
            {
                Prompt = $"Databases: return {toReturn} of your {n} score-pile cards.",
                PlayerIndex = target.Index,
                EligibleCardIds = target.ScorePile.ToArray(),
                MinCount = toReturn,
                MaxCount = toReturn,
            };
            ctx.Paused = true;
            return false;
        }

        var req = (SelectScoreCardSubsetRequest)ctx.PendingChoice;
        ctx.PendingChoice = null;
        foreach (var id in req.ChosenCardIds)
        {
            target.ScorePile.Remove(id);
            g.Decks[g.Cards[id].Age].Add(id);
            GameLog.Log($"{GameLog.P(target)} returns {GameLog.C(g, id)} from score (Databases)");
        }
        SpecialAchievements.CheckAll(g);
        if (req.ChosenCardIds.Count > 0) ctx.DemandSuccessful = true;
        return true;
    }
}
