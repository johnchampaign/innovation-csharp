namespace Innovation.Core.Handlers;

/// <summary>
/// Explosives (age 7, Red/Factory, demand): "I demand you transfer your
/// three highest cards from your hand to my hand! If you do, and then
/// have no cards in hand, draw a 7!"
///
/// Defender always transfers their three highest (ties → defender's choice
/// among the cards sharing the lowest age of those three). Up to three if
/// they have fewer. Then if hand is empty, draw a 7.
/// </summary>
public sealed class ExplosivesDemandHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (ctx.PendingChoice is null)
        {
            if (target.Hand.Count == 0) return false;

            int take = Math.Min(3, target.Hand.Count);

            // Determine which cards are unambiguously "highest" (forced picks)
            // and which tier is contested. Cards strictly above the
            // threshold age are forced; cards at the threshold are the pool
            // from which the defender picks.
            var byAge = target.Hand.OrderByDescending(id => g.Cards[id].Age).ToList();
            int thresholdAge = g.Cards[byAge[take - 1]].Age;
            var forced = byAge.Where(id => g.Cards[id].Age > thresholdAge).ToList();
            var tied = byAge.Where(id => g.Cards[id].Age == thresholdAge).ToList();
            int tieSlots = take - forced.Count;

            if (tied.Count == tieSlots)
            {
                // No choice needed.
                TransferAll(g, target, forced.Concat(tied).ToList(), ctx);
                return PostTransferDraw(g, target);
            }

            // Need defender to pick which of the tied cards to give up.
            ctx.HandlerState = forced;
            ctx.PendingChoice = new SelectHandCardSubsetRequest
            {
                Prompt = $"Explosives: choose {tieSlots} of your age-{thresholdAge} cards to transfer.",
                PlayerIndex = target.Index,
                EligibleCardIds = tied.ToArray(),
                MinCount = tieSlots,
                MaxCount = tieSlots,
            };
            ctx.Paused = true;
            return false;
        }

        var req = (SelectHandCardSubsetRequest)ctx.PendingChoice;
        ctx.PendingChoice = null;
        var forcedList = (List<int>)ctx.HandlerState!;
        ctx.HandlerState = null;

        TransferAll(g, target, forcedList.Concat(req.ChosenCardIds).ToList(), ctx);
        return PostTransferDraw(g, target);
    }

    private static void TransferAll(GameState g, PlayerState target, List<int> ids, DogmaContext ctx)
    {
        var activator = g.Players[ctx.ActivatingPlayerIndex];
        foreach (var id in ids)
            Mechanics.TransferHandToHand(g, target, activator, id);
        if (ids.Count > 0) ctx.DemandSuccessful = true;
    }

    private static bool PostTransferDraw(GameState g, PlayerState target)
    {
        if (target.Hand.Count == 0)
            Mechanics.DrawFromAge(g, target, 7);
        return true;
    }
}
