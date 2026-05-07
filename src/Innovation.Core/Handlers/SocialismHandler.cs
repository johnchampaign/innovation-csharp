namespace Innovation.Core.Handlers;

/// <summary>
/// Socialism (age 8, Purple/Leaf): "You may tuck all cards from your hand.
/// If you tuck one, you must tuck them all. If you tucked at least one
/// purple card, take all the lowest cards in each other player's hand
/// into your hand."
///
/// Three stages:
///   1. Yes/no on tucking the whole hand.
///   2. (Yes only) If 2+ cards share a color the player picks the tuck
///      order; otherwise tucks inline.
///   3. After tucks, if a purple was tucked, take all the lowest cards
///      from each other player's hand.
/// </summary>
public sealed class SocialismHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (target.Hand.Count == 0) return false;

        // Stage 1: yes/no.
        if (ctx.PendingChoice is null && ctx.HandlerState is null)
        {
            ctx.PendingChoice = new YesNoChoiceRequest
            {
                Prompt = "Socialism: tuck your entire hand?",
                PlayerIndex = target.Index,
            };
            ctx.Paused = true;
            return false;
        }

        if (ctx.PendingChoice is YesNoChoiceRequest yn)
        {
            ctx.PendingChoice = null;
            if (!yn.ChosenYes) return false;

            var hand = target.Hand.ToArray();

            // If every tuck lands in a different color stack, the orderings
            // are equivalent — tuck inline. Otherwise prompt.
            if (hand.Length == 1 || !Mechanics.OrderMatters(hand, id => g.Cards[id].Color))
            {
                return TuckAndMaybeTakeOpponents(g, target, hand);
            }

            ctx.HandlerState = hand;
            ctx.PendingChoice = new SelectCardOrderRequest
            {
                Prompt = "Socialism: choose the tuck order (last tucked is at the very bottom of its color pile).",
                PlayerIndex = target.Index,
                Action = "tuck",
                CardIds = hand,
            };
            ctx.Paused = true;
            return false;
        }

        // Stage 3: order resolved.
        var orderReq = (SelectCardOrderRequest)ctx.PendingChoice!;
        var input = (int[])ctx.HandlerState!;
        ctx.PendingChoice = null;
        ctx.HandlerState = null;
        var ordered = Mechanics.ValidateOrder(orderReq.ChosenOrder, input);
        return TuckAndMaybeTakeOpponents(g, target, ordered);
    }

    private static bool TuckAndMaybeTakeOpponents(GameState g, PlayerState target, IReadOnlyList<int> ids)
    {
        bool tuckedPurple = false;
        foreach (var id in ids)
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
            foreach (var oppId in taken)
                Mechanics.TransferHandToHand(g, opp, target, oppId);
        }
        return true;
    }
}
