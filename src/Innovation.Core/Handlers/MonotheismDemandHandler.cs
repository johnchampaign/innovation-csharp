namespace Innovation.Core.Handlers;

/// <summary>
/// Monotheism first effect (age 2, Purple/Castle, <b>demand</b>): "I demand
/// you transfer a top card on your board of a color I do not have to my
/// score pile! If you do, draw and tuck a 1!"
///
/// Eligible colors = target's non-empty piles whose color the activator has
/// no card in at all (empty pile on the activator's side). The follow-up
/// "draw and tuck a 1" is performed by the <em>active</em> player, only if
/// a card actually transferred.
/// </summary>
public sealed class MonotheismDemandHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        var activator = g.Players[ctx.ActivatingPlayerIndex];

        if (ctx.HandlerState is null && ctx.PendingChoice is null)
        {
            var eligible = new List<CardColor>();
            foreach (CardColor c in Enum.GetValues<CardColor>())
            {
                if (target.Stack(c).IsEmpty) continue;
                if (!activator.Stack(c).IsEmpty) continue;
                eligible.Add(c);
            }
            if (eligible.Count == 0) return false;

            ctx.HandlerState = new object();
            ctx.PendingChoice = new SelectColorRequest
            {
                Prompt = $"Monotheism: transfer a top card of a color player "
                       + $"{ctx.ActivatingPlayerIndex + 1} has no cards in.",
                PlayerIndex = target.Index,
                EligibleColors = eligible,
            };
            ctx.Paused = true;
            return false;
        }

        var req = (SelectColorRequest)ctx.PendingChoice!;
        ctx.PendingChoice = null;
        ctx.HandlerState = null;

        if (req.ChosenColor is not CardColor color) return false;

        int moved = Mechanics.TransferBoardToScore(g, target, activator, color);
        if (moved < 0) return false;
        ctx.DemandSuccessful = true;
        if (g.IsGameOver) return true;

        // "If you do, draw and tuck a 1" — "you" is the demand target.
        Mechanics.DrawAndTuck(g, target, 1);
        return true;
    }
}
