namespace Innovation.Core.Handlers;

/// <summary>
/// The Pirate Code (age 5, Red/Crown) — demand: "I demand you transfer
/// two cards of value 4 or less from your score pile to my score pile!"
///
/// Two sequential picks by the target. If the target has fewer than two
/// eligible cards, they transfer what they have. Demand-successful fires
/// iff at least one card actually moved.
/// </summary>
public sealed class PirateCodeDemandHandler : IDogmaHandler
{
    private sealed class AwaitingSecond { }

    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        var activator = g.Players[ctx.ActivatingPlayerIndex];

        // Phase 1.
        if (ctx.HandlerState is null && ctx.PendingChoice is null)
        {
            var eligible = target.ScorePile.Where(id => g.Cards[id].Age <= 4).ToArray();
            if (eligible.Length == 0) return false;
            return QueuePick(target, ctx, eligible, firstPick: true);
        }

        // Phase 1 resume.
        if (ctx.HandlerState as string == "first")
        {
            var req = (SelectScoreCardRequest)ctx.PendingChoice!;
            ctx.PendingChoice = null;
            ctx.HandlerState = null;
            if (req.ChosenCardId is not int id1) return false;
            Mechanics.TransferScoreToScore(g, target, activator, id1);
            ctx.DemandSuccessful = true;
            if (g.IsGameOver) return true;

            var eligible2 = target.ScorePile.Where(id => g.Cards[id].Age <= 4).ToArray();
            if (eligible2.Length == 0) return true;
            return QueuePick(target, ctx, eligible2, firstPick: false);
        }

        // Phase 2 resume.
        var r2 = (SelectScoreCardRequest)ctx.PendingChoice!;
        ctx.PendingChoice = null;
        ctx.HandlerState = null;
        if (r2.ChosenCardId is not int id2) return true;
        Mechanics.TransferScoreToScore(g, target, activator, id2);
        return true;
    }

    private static bool QueuePick(PlayerState target, DogmaContext ctx, int[] eligible, bool firstPick)
    {
        ctx.HandlerState = firstPick ? "first" : new AwaitingSecond();
        ctx.PendingChoice = new SelectScoreCardRequest
        {
            Prompt = firstPick
                ? "Pirate Code: transfer a card of value 4 or less from your score pile (1 of 2)."
                : "Pirate Code: transfer a second card of value 4 or less from your score pile.",
            PlayerIndex = target.Index,
            EligibleCardIds = eligible,
            AllowNone = false,
        };
        ctx.Paused = true;
        return firstPick ? false : true;
    }
}
