namespace Innovation.Core.Handlers;

/// <summary>
/// Classification (age 6, Green/Lightbulb) — non-demand: "Reveal the
/// color of a card from your hand. Take into your hand all cards of
/// that color from all other players' hands. Then, meld all cards of
/// that color from your hand."
///
/// One hand-card pick (reveals its color). All other players' hand
/// cards of that color move to this target's hand. Then every hand
/// card of that color is melded.
/// </summary>
public sealed class ClassificationHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (target.Hand.Count == 0) return false;

        if (ctx.PendingChoice is null)
        {
            ctx.PendingChoice = new SelectHandCardRequest
            {
                Prompt = "Classification: reveal a card from your hand (its color is chosen).",
                PlayerIndex = target.Index,
                EligibleCardIds = target.Hand.ToArray(),
                AllowNone = false,
            };
            ctx.Paused = true;
            return false;
        }

        var req = (SelectHandCardRequest)ctx.PendingChoice;
        ctx.PendingChoice = null;
        if (req.ChosenCardId is not int revealId) return false;

        var color = g.Cards[revealId].Color;
        GameLog.Log($"{GameLog.P(target)} reveals {GameLog.C(g, revealId)} — color {color}");

        foreach (var other in g.Players)
        {
            if (other.Index == target.Index) continue;
            var taken = other.Hand.Where(id => g.Cards[id].Color == color).ToArray();
            foreach (var id in taken)
                Mechanics.TransferHandToHand(g, other, target, id);
        }

        var toMeld = target.Hand.Where(id => g.Cards[id].Color == color).ToArray();
        foreach (var id in toMeld)
            Mechanics.Meld(g, target, id);

        return true;
    }
}
