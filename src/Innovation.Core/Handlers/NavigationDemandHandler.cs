namespace Innovation.Core.Handlers;

/// <summary>
/// Navigation (age 4, Green/Crown) — demand: "I demand you transfer a
/// 2 or 3 from your score pile to my score pile!"
///
/// Target picks which eligible card. Mandatory if any 2/3 is in the
/// target's score pile.
/// </summary>
public sealed class NavigationDemandHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (ctx.HandlerState is null && ctx.PendingChoice is null)
        {
            var eligible = target.ScorePile
                .Where(id => g.Cards[id].Age == 2 || g.Cards[id].Age == 3)
                .ToArray();
            if (eligible.Length == 0) return false;

            ctx.HandlerState = new object();
            ctx.PendingChoice = new SelectScoreCardRequest
            {
                Prompt = $"Navigation: transfer a 2 or 3 from your score pile "
                       + $"to player {ctx.ActivatingPlayerIndex + 1}'s score pile.",
                PlayerIndex = target.Index,
                EligibleCardIds = eligible,
                AllowNone = false,
            };
            ctx.Paused = true;
            return false;
        }

        var req = (SelectScoreCardRequest)ctx.PendingChoice!;
        ctx.PendingChoice = null;
        ctx.HandlerState = null;
        if (req.ChosenCardId is not int cardId) return false;

        var activator = g.Players[ctx.ActivatingPlayerIndex];
        Mechanics.TransferScoreToScore(g, target, activator, cardId);
        ctx.DemandSuccessful = true;
        return true;
    }
}
