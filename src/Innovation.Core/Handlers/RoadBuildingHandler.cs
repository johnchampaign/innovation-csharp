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
    private sealed class TwoPickOrder { public int[] Picks = Array.Empty<int>(); }

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

        // ---------- Phase 1 resume: perform the melds (with order pick if 2) ----------
        if (ctx.HandlerState as string == "subset")
        {
            var subset = (SelectHandCardSubsetRequest)ctx.PendingChoice!;
            ctx.PendingChoice = null;

            var picks = subset.ChosenCardIds.ToArray();
            int melded = picks.Length;
            if (melded == 0) { ctx.HandlerState = null; return false; }

            if (melded == 1)
            {
                Mechanics.Meld(g, target, picks[0]);
                ctx.HandlerState = null;
                if (g.IsGameOver) return true;
                return true;   // single meld, skip the optional exchange (rule requires 2 melded)
            }

            // 2 picks — ask which order to meld in. Last melded is on top.
            ctx.HandlerState = new TwoPickOrder { Picks = picks };
            ctx.PendingChoice = new SelectCardOrderRequest
            {
                Prompt = "Road Building: choose the meld order (last melded is on top).",
                PlayerIndex = target.Index,
                Action = "meld",
                CardIds = picks,
            };
            ctx.Paused = true;
            return false;
        }

        if (ctx.HandlerState is TwoPickOrder twoPick)
        {
            var orderReq = (SelectCardOrderRequest)ctx.PendingChoice!;
            ctx.PendingChoice = null;
            ctx.HandlerState = null;

            var ordered = Mechanics.ValidateOrder(orderReq.ChosenOrder, twoPick.Picks);
            // ChosenOrder is final-arrangement top-first; reverse for melds.
            for (int i = ordered.Count - 1; i >= 0; i--)
            {
                Mechanics.Meld(g, target, ordered[i]);
                if (g.IsGameOver) return true;
            }

            // Exchange is only possible when exactly two were melded (the
            // path we just took) AND active has a top Red AND there's an
            // opponent to trade with.
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
