namespace Innovation.Core.Handlers;

/// <summary>
/// Education (age 3, Purple/Lightbulb): "You may return the highest card
/// from your score pile. If you do, draw a card of value two higher than
/// the highest card remaining in your score pile."
///
/// Singular — exactly one card is returned. If multiple score-pile cards
/// tie for highest age, the owner chooses which one.
/// </summary>
public sealed class EducationHandler : IDogmaHandler
{
    private enum Stage { AskYesNo, PickTied }

    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (target.ScorePile.Count == 0) return false;

        var stage = (Stage?)ctx.HandlerState ?? Stage.AskYesNo;

        if (stage == Stage.AskYesNo)
        {
            if (ctx.PendingChoice is null)
            {
                ctx.PendingChoice = new YesNoChoiceRequest
                {
                    Prompt = "Education: return the highest card from your score pile?",
                    PlayerIndex = target.Index,
                };
                ctx.Paused = true;
                return false;
            }

            var yn = (YesNoChoiceRequest)ctx.PendingChoice;
            ctx.PendingChoice = null;
            if (!yn.ChosenYes) { ctx.HandlerState = null; return false; }

            int highest = target.ScorePile.Max(id => g.Cards[id].Age);
            var tied = target.ScorePile.Where(id => g.Cards[id].Age == highest).ToArray();

            if (tied.Length == 1)
            {
                ReturnAndDraw(g, target, tied[0]);
                ctx.HandlerState = null;
                return true;
            }

            ctx.PendingChoice = new SelectScoreCardRequest
            {
                Prompt = $"Education: choose which of your age-{highest} cards to return.",
                PlayerIndex = target.Index,
                EligibleCardIds = tied,
                AllowNone = false,
            };
            ctx.HandlerState = Stage.PickTied;
            ctx.Paused = true;
            return false;
        }

        // Stage.PickTied
        var req = (SelectScoreCardRequest)ctx.PendingChoice!;
        ctx.PendingChoice = null;
        ctx.HandlerState = null;
        if (req.ChosenCardId is int chosen)
            ReturnAndDraw(g, target, chosen);
        return true;
    }

    private static void ReturnAndDraw(GameState g, PlayerState target, int cardId)
    {
        target.ScorePile.Remove(cardId);
        int age = g.Cards[cardId].Age;
        g.Decks[age].Add(cardId);
        GameLog.Log($"{GameLog.P(target)} returns {GameLog.C(g, cardId)} from score pile");
        SpecialAchievements.CheckAll(g);
        if (g.IsGameOver) return;

        int remHigh = target.ScorePile.Count == 0 ? 0
            : target.ScorePile.Max(id => g.Cards[id].Age);
        int startAge = Math.Max(1, remHigh + 2);
        Mechanics.DrawFromAge(g, target, startAge);
    }
}
