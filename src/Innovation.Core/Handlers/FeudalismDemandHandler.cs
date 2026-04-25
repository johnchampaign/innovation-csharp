namespace Innovation.Core.Handlers;

/// <summary>
/// Feudalism (age 3, Purple/Castle) — demand: "I demand you transfer a
/// card with a [Castle] from your hand to my hand!"
///
/// Target picks which castle-iconed hand card to surrender. Mandatory
/// when any castle card is in hand.
/// </summary>
public sealed class FeudalismDemandHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (ctx.HandlerState is null && ctx.PendingChoice is null)
        {
            var eligible = target.Hand
                .Where(id => HasIcon(g.Cards[id], Icon.Castle))
                .ToArray();
            if (eligible.Length == 0) return false;

            ctx.HandlerState = new object();
            ctx.PendingChoice = new SelectHandCardRequest
            {
                Prompt = $"Feudalism: transfer a [Castle] card from your "
                       + $"hand to player {ctx.ActivatingPlayerIndex + 1}'s hand.",
                PlayerIndex = target.Index,
                EligibleCardIds = eligible,
                AllowNone = false,
            };
            ctx.Paused = true;
            return false;
        }

        var req = (SelectHandCardRequest)ctx.PendingChoice!;
        ctx.PendingChoice = null;
        ctx.HandlerState = null;
        if (req.ChosenCardId is not int cardId) return false;

        var activator = g.Players[ctx.ActivatingPlayerIndex];
        Mechanics.TransferHandToHand(g, target, activator, cardId);
        ctx.DemandSuccessful = true;
        return true;
    }

    internal static bool HasIcon(Card c, Icon icon) =>
        c.Top == icon || c.Left == icon || c.Middle == icon || c.Right == icon;
}
