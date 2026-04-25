namespace Innovation.Core.Handlers;

/// <summary>
/// Chemistry (age 5, Blue/Factory) — effect 2: "Draw and score a card
/// of value one higher than the highest top card on your board and then
/// return a card from your score pile."
///
/// Draw-and-score is mandatory; highest-top defaults to 0 for an empty
/// board (then draws an age-1 via the FindNextDrawAge fallback). The
/// second step — return a score-pile card — is also mandatory (no
/// optional-return clause). If the pile is empty after the draw-and-
/// score step (shouldn't happen unless something weird), skip it.
/// </summary>
public sealed class ChemistryDrawScoreReturnHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (ctx.HandlerState is null && ctx.PendingChoice is null)
        {
            int highestTop = Mechanics.HighestTopCardAge(g, target);
            int startAge = Math.Max(1, highestTop + 1);
            Mechanics.DrawAndScore(g, target, startAge);
            if (g.IsGameOver) return true;

            if (target.ScorePile.Count == 0) return true;
            ctx.HandlerState = new object();
            ctx.PendingChoice = new SelectScoreCardRequest
            {
                Prompt = "Chemistry: return a card from your score pile.",
                PlayerIndex = target.Index,
                EligibleCardIds = target.ScorePile.ToArray(),
                AllowNone = false,
            };
            ctx.Paused = true;
            return true;
        }

        var req = (SelectScoreCardRequest)ctx.PendingChoice!;
        ctx.PendingChoice = null;
        ctx.HandlerState = null;
        if (req.ChosenCardId is not int id) return true;

        target.ScorePile.Remove(id);
        g.Decks[g.Cards[id].Age].Add(id);
        SpecialAchievements.CheckAll(g);
        return true;
    }
}
