namespace Innovation.Core.Handlers;

/// <summary>
/// Socialism (age 8, Purple/Leaf): "You may tuck all cards from your hand.
/// If you tuck one, you must tuck them all. If you tucked at least one
/// purple card, take all the lowest cards in each other player's hand
/// into your hand."
/// </summary>
public sealed class SocialismHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (target.Hand.Count == 0) return false;

        if (ctx.PendingChoice is null)
        {
            ctx.PendingChoice = new YesNoChoiceRequest
            {
                Prompt = "Socialism: tuck your entire hand?",
                PlayerIndex = target.Index,
            };
            ctx.Paused = true;
            return false;
        }

        var yn = (YesNoChoiceRequest)ctx.PendingChoice;
        ctx.PendingChoice = null;
        if (!yn.ChosenYes) return false;

        bool tuckedPurple = false;
        var snapshot = target.Hand.ToArray();
        foreach (var id in snapshot)
        {
            if (g.Cards[id].Color == CardColor.Purple) tuckedPurple = true;
            Mechanics.Tuck(g, target, id);
            if (g.IsGameOver) return true;
        }

        if (!tuckedPurple) return true;

        foreach (var opp in g.Players)
        {
            if (opp.Index == target.Index) continue;
            if (opp.Hand.Count == 0) continue;
            int low = opp.Hand.Min(id => g.Cards[id].Age);
            var taken = opp.Hand.Where(id => g.Cards[id].Age == low).ToArray();
            foreach (var id in taken)
                Mechanics.TransferHandToHand(g, opp, target, id);
        }
        return true;
    }
}
