namespace Innovation.Core.Handlers;

/// <summary>
/// Mapmaking first effect (age 2, Yellow/Crown, <b>demand</b>): "I demand
/// you transfer a 1 from your score pile to my score pile!"
///
/// Target picks which age-1 card to surrender (VB6 auto-picks; we raise
/// the choice so the caller decides — there is a real decision when the
/// target has multiple 1s). Sets <see cref="DogmaContext.DemandSuccessful"/>
/// on a successful transfer so <see cref="MapmakingDrawIfDemandHandler"/>
/// (effect 2) fires.
/// </summary>
public sealed class MapmakingDemandHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (ctx.HandlerState is null && ctx.PendingChoice is null)
        {
            var eligible = target.ScorePile
                .Where(id => g.Cards[id].Age == 1)
                .ToArray();
            if (eligible.Length == 0) return false;

            ctx.HandlerState = new object();
            ctx.PendingChoice = new SelectScoreCardRequest
            {
                Prompt = $"Mapmaking: transfer a 1 from your score pile to "
                       + $"player {ctx.ActivatingPlayerIndex + 1}'s score pile.",
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
