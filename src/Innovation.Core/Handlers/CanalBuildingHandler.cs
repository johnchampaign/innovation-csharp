namespace Innovation.Core.Handlers;

/// <summary>
/// Canal Building (age 2, Yellow/Crown): "You may exchange all the highest
/// cards in your hand with all the highest cards in your score pile."
///
/// "Highest" is computed independently for each pile — the hand's highest
/// age may differ from the score pile's. If one side is empty the exchange
/// still happens (all cards on the non-empty side move across).
///
/// Critically, this is a <em>transfer</em>, not a score. Per the rules,
/// cards moved here do not count toward Monument — see
/// <see cref="Mechanics.TransferHandToScore"/> and
/// <see cref="Mechanics.TransferScoreToHand"/>.
/// </summary>
public sealed class CanalBuildingHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        // No cards on either side: nothing to exchange.
        if (target.Hand.Count == 0 && target.ScorePile.Count == 0) return false;

        if (ctx.PendingChoice is null)
        {
            ctx.PendingChoice = new YesNoChoiceRequest
            {
                Prompt = "Canal Building: exchange all highest cards in your "
                       + "hand with all highest cards in your score pile?",
                PlayerIndex = target.Index,
            };
            ctx.Paused = true;
            return false;
        }

        var req = (YesNoChoiceRequest)ctx.PendingChoice;
        ctx.PendingChoice = null;
        if (!req.ChosenYes) return false;

        int handHi = target.Hand.Count == 0 ? 0
            : target.Hand.Max(id => g.Cards[id].Age);
        int scoreHi = target.ScorePile.Count == 0 ? 0
            : target.ScorePile.Max(id => g.Cards[id].Age);

        var fromHand = target.Hand
            .Where(id => g.Cards[id].Age == handHi).ToArray();
        var fromScore = target.ScorePile
            .Where(id => g.Cards[id].Age == scoreHi).ToArray();

        // Move hand-highest → score pile, then score-highest → hand. Order
        // matters only in that we snapshot both sides first so the pools
        // don't shift under us while iterating.
        foreach (var id in fromHand)
            Mechanics.TransferHandToScore(g, target, target, id);
        foreach (var id in fromScore)
            Mechanics.TransferScoreToHand(g, target, id);

        return true;
    }
}
