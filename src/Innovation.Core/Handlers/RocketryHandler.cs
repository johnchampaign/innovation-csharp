namespace Innovation.Core.Handlers;

/// <summary>
/// Rocketry (age 8, Blue/Clock): "Return a card in any other player's score
/// pile for every two [Clock] icons on your board."
///
/// Target (the executing player) picks which card to pull from which
/// opponent's score pile. Repeats floor(clocks/2) times, pausing once per
/// return. State tracks how many returns remain.
/// </summary>
public sealed class RocketryHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        int remaining = ctx.HandlerState switch
        {
            int n => n,
            _ => IconCounter.Count(target, Icon.Clock, g.Cards) / 2,
        };

        if (ctx.PendingChoice is SelectScoreCardRequest prior)
        {
            ctx.PendingChoice = null;
            if (prior.ChosenCardId is int chosen)
            {
                foreach (var pl in g.Players)
                {
                    if (pl.Index == target.Index) continue;
                    if (pl.ScorePile.Remove(chosen))
                    {
                        g.Decks[g.Cards[chosen].Age].Add(chosen);
                        GameLog.Log($"{GameLog.P(pl)} returns {GameLog.C(g, chosen)} from score (Rocketry by {GameLog.P(target)})");
                        break;
                    }
                }
                SpecialAchievements.CheckAll(g);
                if (g.IsGameOver) { ctx.HandlerState = null; return true; }
                remaining--;
            }
            else
            {
                ctx.HandlerState = null;
                return true;
            }
        }

        if (remaining <= 0) { ctx.HandlerState = null; return true; }

        var eligible = new List<int>();
        foreach (var pl in g.Players)
        {
            if (pl.Index == target.Index) continue;
            eligible.AddRange(pl.ScorePile);
        }
        if (eligible.Count == 0) { ctx.HandlerState = null; return true; }

        ctx.PendingChoice = new SelectScoreCardRequest
        {
            Prompt = $"Rocketry: choose an opponent score-pile card to return ({remaining} left).",
            PlayerIndex = target.Index,
            EligibleCardIds = eligible,
            AllowNone = false,
        };
        ctx.HandlerState = remaining;
        ctx.Paused = true;
        return true;
    }
}
