namespace Innovation.Core.Handlers;

/// <summary>
/// Road Building (age 2, Red/Castle): "Meld one or two cards from your
/// hand. If you melded two, you may transfer your top red card to another
/// player's board. In exchange, transfer that player's top green card to
/// your board."
///
/// Phases:
///   1. <b>Subset pick</b> — 1 or 2 hand cards to meld (MinCount=1).
///   2. If exactly two melded, active has a top Red, <em>and</em> there's
///      at least one opponent: ask a yes/no. If yes, perform the exchange.
///
/// Edge cases (per player ruling):
///   • Active has no top Red after melding: the optional exchange simply
///     isn't offered.
///   • Chosen opponent has no top Green: the Red still transfers,
///     nothing comes back (the exchange isn't conditional on both legs).
///   • Multi-opponent games: until a SelectPlayerRequest exists, the
///     handler auto-picks the next seat in turn order from the active
///     player. Real "let the active player choose" is a TODO.
/// </summary>
public sealed class RoadBuildingHandler : IDogmaHandler
{
    private sealed class AwaitingExchangeYesNo { public int OpponentIndex; }

    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        // ---------- Phase 1: ask for the meld subset ----------
        if (ctx.HandlerState is null && ctx.PendingChoice is null)
        {
            if (target.Hand.Count == 0) return false;
            int max = Math.Min(2, target.Hand.Count);

            ctx.HandlerState = "subset";
            ctx.PendingChoice = new SelectHandCardSubsetRequest
            {
                Prompt = "Road Building: meld one or two cards from your hand.",
                PlayerIndex = target.Index,
                EligibleCardIds = target.Hand.ToArray(),
                MinCount = 1,
                MaxCount = max,
            };
            ctx.Paused = true;
            return false;
        }

        // ---------- Phase 1 resume: perform the melds ----------
        if (ctx.HandlerState as string == "subset")
        {
            var subset = (SelectHandCardSubsetRequest)ctx.PendingChoice!;
            ctx.PendingChoice = null;
            ctx.HandlerState = null;

            int melded = subset.ChosenCardIds.Count;
            if (melded == 0) return false;   // declined with no meld

            foreach (var id in subset.ChosenCardIds)
                Mechanics.Meld(g, target, id);
            if (g.IsGameOver) return true;

            // Exchange is only possible when exactly two were melded AND
            // active has a top Red AND there's an opponent to trade with.
            if (melded < 2) return true;
            var redStack = target.Stack(CardColor.Red);
            if (redStack.IsEmpty) return true;

            // Find opponents.
            if (g.Players.Length <= 1) return true;
            // Auto-pick next seat in turn order (TODO: active-player choice).
            int opponentIndex = (target.Index + 1) % g.Players.Length;

            ctx.HandlerState = new AwaitingExchangeYesNo { OpponentIndex = opponentIndex };
            ctx.PendingChoice = new YesNoChoiceRequest
            {
                Prompt = $"Road Building: transfer your top red card to "
                       + $"player {opponentIndex + 1}, in exchange for their "
                       + $"top green card?",
                PlayerIndex = target.Index,
            };
            ctx.Paused = true;
            return true;   // meld already progressed state
        }

        // ---------- Phase 2 resume: perform the exchange ----------
        var wait = (AwaitingExchangeYesNo)ctx.HandlerState!;
        var yn = (YesNoChoiceRequest)ctx.PendingChoice!;
        ctx.PendingChoice = null;
        ctx.HandlerState = null;

        if (!yn.ChosenYes) return true;

        var opponent = g.Players[wait.OpponentIndex];
        Mechanics.TransferBoardToBoard(g, target, opponent, CardColor.Red);
        if (g.IsGameOver) return true;
        Mechanics.TransferBoardToBoard(g, opponent, target, CardColor.Green);
        return true;
    }
}
