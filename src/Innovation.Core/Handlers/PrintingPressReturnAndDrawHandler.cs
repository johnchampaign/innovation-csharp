namespace Innovation.Core.Handlers;

/// <summary>
/// Printing Press (age 4, Blue/Lightbulb) — effect 1: "You may return a
/// card from your score pile. If you do, draw a card of value two
/// higher than the top purple card on your board."
///
/// If no top purple card exists, the "two higher" base is 0 → draws an
/// age-2 card (per FindNextDrawAge fallback from age 1 with +2).
/// Standard reading: treat absent top purple as age 0.
/// </summary>
public sealed class PrintingPressReturnAndDrawHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (target.ScorePile.Count == 0) return false;

        if (ctx.HandlerState is null && ctx.PendingChoice is null)
        {
            ctx.HandlerState = new object();
            ctx.PendingChoice = new SelectScoreCardRequest
            {
                Prompt = "Printing Press: return a card from your score pile?",
                PlayerIndex = target.Index,
                EligibleCardIds = target.ScorePile.ToArray(),
                AllowNone = true,
            };
            ctx.Paused = true;
            return false;
        }

        var req = (SelectScoreCardRequest)ctx.PendingChoice!;
        ctx.PendingChoice = null;
        ctx.HandlerState = null;
        if (req.ChosenCardId is not int cardId) return false;

        target.ScorePile.Remove(cardId);
        g.Decks[g.Cards[cardId].Age].Add(cardId);
        SpecialAchievements.CheckAll(g);
        if (g.IsGameOver) return true;

        var purple = target.Stack(CardColor.Purple);
        int baseAge = purple.IsEmpty ? 0 : g.Cards[purple.Top].Age;
        int drawAge = Math.Max(1, baseAge + 2);
        Mechanics.DrawFromAge(g, target, drawAge);
        return true;
    }
}
