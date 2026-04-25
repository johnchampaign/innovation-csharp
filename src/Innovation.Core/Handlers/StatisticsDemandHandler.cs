namespace Innovation.Core.Handlers;

/// <summary>
/// Statistics (age 5, Yellow/Leaf) — demand: "I demand you draw the
/// highest card in your score pile! If you do, and have only one card
/// in your hand afterwards, repeat this demand!"
///
/// "Draw" here moves the target's highest-age score-pile card to their
/// hand. Ties are broken by the target (their card is leaving their
/// score pile). Loop continues while post-transfer hand size == 1.
/// </summary>
public sealed class StatisticsDemandHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        bool any = ctx.HandlerState is bool b && b;

        while (true)
        {
            if (ctx.PendingChoice is SelectScoreCardRequest prior)
            {
                ctx.PendingChoice = null;
                if (prior.ChosenCardId is int chosen)
                {
                    target.ScorePile.Remove(chosen);
                    target.Hand.Add(chosen);
                    GameLog.Log($"{GameLog.P(target)} pulls {GameLog.C(g, chosen)} from score pile to hand");
                    SpecialAchievements.CheckAll(g);
                    ctx.DemandSuccessful = true;
                    any = true;
                    if (g.IsGameOver) { ctx.HandlerState = null; return true; }
                    if (target.Hand.Count != 1) { ctx.HandlerState = null; return any; }
                }
                else
                {
                    ctx.HandlerState = null;
                    return any;
                }
            }

            if (target.ScorePile.Count == 0) { ctx.HandlerState = null; return any; }

            int highest = target.ScorePile.Max(id => g.Cards[id].Age);
            var tied = target.ScorePile.Where(id => g.Cards[id].Age == highest).ToList();

            if (tied.Count == 1)
            {
                int pick = tied[0];
                target.ScorePile.Remove(pick);
                target.Hand.Add(pick);
                GameLog.Log($"{GameLog.P(target)} pulls {GameLog.C(g, pick)} from score pile to hand");
                SpecialAchievements.CheckAll(g);
                ctx.DemandSuccessful = true;
                any = true;
                if (g.IsGameOver) { ctx.HandlerState = null; return true; }
                if (target.Hand.Count != 1) { ctx.HandlerState = null; return any; }
                continue;
            }

            ctx.PendingChoice = new SelectScoreCardRequest
            {
                Prompt = $"Statistics: choose which of your age-{highest} cards to move to your hand.",
                PlayerIndex = target.Index,
                EligibleCardIds = tied.ToArray(),
                AllowNone = false,
            };
            ctx.HandlerState = any;
            ctx.Paused = true;
            return any;
        }
    }
}
