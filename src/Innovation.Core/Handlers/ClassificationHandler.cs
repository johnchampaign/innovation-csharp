namespace Innovation.Core.Handlers;

/// <summary>
/// Classification (age 6, Green/Lightbulb) — non-demand: "Reveal the
/// color of a card from your hand. Take into your hand all cards of
/// that color from all other players' hands. Then, meld all cards of
/// that color from your hand."
///
/// Three stages:
///   1. <b>Reveal</b> — pick which card to reveal (its color drives the
///      transfers and the meld).
///   2. <b>Transfer</b> — every other player's hand cards of that color
///      move to this target's hand.
///   3. <b>Order</b> — when more than one card is about to be melded,
///      the target picks the meld order so the final top-card and
///      stacking is their choice.
/// </summary>
public sealed class ClassificationHandler : IDogmaHandler
{
    private enum Stage { Reveal, Order }

    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        var stage = (Stage?)ctx.HandlerState ?? Stage.Reveal;

        if (stage == Stage.Reveal)
        {
            if (ctx.PendingChoice is null)
            {
                if (target.Hand.Count == 0) return false;

                ctx.PendingChoice = new SelectHandCardRequest
                {
                    Prompt = "Classification: reveal a card from your hand (its color is chosen).",
                    PlayerIndex = target.Index,
                    EligibleCardIds = target.Hand.ToArray(),
                    AllowNone = false,
                };
                ctx.HandlerState = Stage.Reveal;
                ctx.Paused = true;
                return false;
            }

            var req = (SelectHandCardRequest)ctx.PendingChoice;
            ctx.PendingChoice = null;
            if (req.ChosenCardId is not int revealId) { ctx.HandlerState = null; return false; }

            var color = g.Cards[revealId].Color;
            GameLog.Log($"{GameLog.P(target)} reveals {GameLog.C(g, revealId)} — color {color}");

            // Pull every other player's hand cards of that color.
            foreach (var other in g.Players)
            {
                if (other.Index == target.Index) continue;
                var taken = other.Hand.Where(id => g.Cards[id].Color == color).ToArray();
                foreach (var id in taken)
                    Mechanics.TransferHandToHand(g, other, target, id);
            }

            // Now compute the to-meld list. Hand-card ordering is
            // unspecified, so we shouldn't pretend it's meaningful — the
            // player picks the meld order if there's more than one.
            var toMeld = target.Hand.Where(id => g.Cards[id].Color == color).ToArray();
            if (toMeld.Length == 0) { ctx.HandlerState = null; return true; }
            if (toMeld.Length == 1)
            {
                Mechanics.Meld(g, target, toMeld[0]);
                ctx.HandlerState = null;
                return true;
            }

            ctx.PendingChoice = new SelectCardOrderRequest
            {
                Prompt = $"Classification: choose the order to meld your {color} cards (last melded ends up on top).",
                PlayerIndex = target.Index,
                Action = "meld",
                CardIds = toMeld,
            };
            ctx.HandlerState = Stage.Order;
            ctx.Paused = true;
            return false;
        }

        // Stage.Order — apply the chosen meld order.
        var orderReq = (SelectCardOrderRequest)ctx.PendingChoice!;
        ctx.PendingChoice = null;
        ctx.HandlerState = null;

        var ordered = Mechanics.ValidateOrder(orderReq.ChosenOrder, orderReq.CardIds);
        // ChosenOrder is the FINAL pile arrangement, top-first. Mechanics.Meld
        // pushes onto the top, so we apply in REVERSE: the LAST card melded
        // ends up on top, which should be the FIRST id in the chosen order.
        for (int i = ordered.Count - 1; i >= 0; i--)
            Mechanics.Meld(g, target, ordered[i]);
        return true;
    }
}
