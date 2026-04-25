namespace Innovation.Core.Handlers;

/// <summary>
/// Perspective (age 4, Yellow/Lightbulb): "You may return a card from
/// your hand. If you do, score a card from your hand for every two
/// [Lightbulb] icons on your board."
///
/// Phase 1: optional hand-card return.
/// Phase 2 (only if returned): score (lightbulbs / 2) cards from hand,
/// one at a time.
/// </summary>
public sealed class PerspectiveHandler : IDogmaHandler
{
    private sealed class AwaitingScores { public int Remaining; }

    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        // Phase 1: return-a-card.
        if (ctx.HandlerState is null && ctx.PendingChoice is null)
        {
            if (target.Hand.Count == 0) return false;

            ctx.HandlerState = "return";
            ctx.PendingChoice = new SelectHandCardRequest
            {
                Prompt = "Perspective: return a card from your hand?",
                PlayerIndex = target.Index,
                EligibleCardIds = target.Hand.ToArray(),
                AllowNone = true,
            };
            ctx.Paused = true;
            return false;
        }

        if (ctx.HandlerState as string == "return")
        {
            var req = (SelectHandCardRequest)ctx.PendingChoice!;
            ctx.PendingChoice = null;
            ctx.HandlerState = null;
            if (req.ChosenCardId is not int rid) return false;

            Mechanics.Return(g, target, rid);
            int bulbs = IconCounter.Count(target, Icon.Lightbulb, g.Cards);
            int scores = bulbs / 2;
            if (scores <= 0) return true;

            return QueueNextScore(g, target, ctx, scores);
        }

        // Phase 2 loop.
        var wait = (AwaitingScores)ctx.HandlerState!;
        var sreq = (SelectHandCardRequest)ctx.PendingChoice!;
        ctx.PendingChoice = null;
        ctx.HandlerState = null;
        if (sreq.ChosenCardId is int sid) Mechanics.Score(g, target, sid);
        if (g.IsGameOver) return true;

        int left = wait.Remaining - 1;
        if (left <= 0 || target.Hand.Count == 0) return true;
        return QueueNextScore(g, target, ctx, left);
    }

    private static bool QueueNextScore(GameState g, PlayerState target, DogmaContext ctx, int remaining)
    {
        if (target.Hand.Count == 0) return true;
        ctx.HandlerState = new AwaitingScores { Remaining = remaining };
        ctx.PendingChoice = new SelectHandCardRequest
        {
            Prompt = $"Perspective: score a card from your hand ({remaining} remaining).",
            PlayerIndex = target.Index,
            EligibleCardIds = target.Hand.ToArray(),
            AllowNone = false,
        };
        ctx.Paused = true;
        return true;
    }
}
