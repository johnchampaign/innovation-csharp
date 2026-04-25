namespace Innovation.Core.Handlers;

/// <summary>
/// Translation (age 3, Blue/Crown) — effect 1: "You may meld all the
/// cards in your score pile. If you meld one, you must meld them all."
///
/// Yes/no gates the all-or-nothing commitment, then the player picks
/// the meld order one card at a time (order matters: the last card of
/// a given color ends up on top of that pile).
/// </summary>
public sealed class TranslationMeldScoreHandler : IDogmaHandler
{
    private sealed class AwaitingNext { }

    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (target.ScorePile.Count == 0) return false;

        // Phase 1: yes/no.
        if (ctx.HandlerState is null && ctx.PendingChoice is null)
        {
            ctx.HandlerState = "confirm";
            ctx.PendingChoice = new YesNoChoiceRequest
            {
                Prompt = "Translation: meld every card in your score pile?",
                PlayerIndex = target.Index,
            };
            ctx.Paused = true;
            return false;
        }

        if (ctx.HandlerState as string == "confirm")
        {
            var yn = (YesNoChoiceRequest)ctx.PendingChoice!;
            ctx.PendingChoice = null;
            ctx.HandlerState = null;
            if (!yn.ChosenYes) return false;
            return QueueNextPick(g, target, ctx);
        }

        // Phase 2 resume: meld the chosen card, then queue the next pick.
        var req = (SelectScoreCardRequest)ctx.PendingChoice!;
        ctx.PendingChoice = null;
        ctx.HandlerState = null;
        if (req.ChosenCardId is not int id) return true;

        target.ScorePile.Remove(id);
        target.Stack(g.Cards[id].Color).Meld(id);
        SpecialAchievements.CheckAll(g);
        if (g.IsGameOver) return true;

        return QueueNextPick(g, target, ctx);
    }

    private static bool QueueNextPick(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (target.ScorePile.Count == 0) return true;
        ctx.HandlerState = new AwaitingNext();
        ctx.PendingChoice = new SelectScoreCardRequest
        {
            Prompt = $"Translation: pick the next card to meld "
                   + $"({target.ScorePile.Count} remaining).",
            PlayerIndex = target.Index,
            EligibleCardIds = target.ScorePile.ToArray(),
            AllowNone = false,
        };
        ctx.Paused = true;
        return true;
    }
}
