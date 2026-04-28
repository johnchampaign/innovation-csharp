namespace Innovation.Core.Handlers;

/// <summary>
/// Road Building (age 2, Red/Castle): "Meld one or two cards from your
/// hand. If you melded two, you may transfer your top red card to another
/// player's board. In exchange, transfer that player's top green card to
/// your board."
///
/// State machine:
///   • null      → post subset request.
///   • "subset"  → subset answered; meld (with order pick if 2 same-colour),
///                 then offer exchange if 2 melded.
///   • TwoPickOrder → order resolved; meld, then offer exchange.
///   • AwaitingExchange → yes/no answered; perform exchange.
///
/// Edge cases:
///   • Active has no top Red after melding: exchange isn't offered.
///   • Chosen opponent has no top Green: Red still transfers, nothing
///     comes back (the legs aren't conditional on each other).
///   • Multi-opponent games: until SelectPlayer exists, auto-picks the
///     next seat in turn order from the active player. (TODO.)
/// </summary>
public sealed class RoadBuildingHandler : IDogmaHandler
{
    private sealed class TwoPickOrder { public int[] Picks = Array.Empty<int>(); }
    private sealed class AwaitingExchange { public int OpponentIndex; }

    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        // Phase 1: post subset request.
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

        // Phase 2: subset answered.
        if (ctx.HandlerState as string == "subset")
        {
            var subset = (SelectHandCardSubsetRequest)ctx.PendingChoice!;
            ctx.PendingChoice = null;

            var picks = subset.ChosenCardIds.ToArray();
            if (picks.Length == 0) { ctx.HandlerState = null; return false; }

            if (picks.Length == 1)
            {
                Mechanics.Meld(g, target, picks[0]);
                ctx.HandlerState = null;
                return true;   // single meld doesn't trigger the exchange option
            }

            // 2 picks. Skip the order prompt if they're different colors.
            if (!Mechanics.OrderMatters(picks, id => g.Cards[id].Color))
            {
                foreach (var id in picks)
                {
                    Mechanics.Meld(g, target, id);
                    if (g.IsGameOver) { ctx.HandlerState = null; return true; }
                }
                return OfferExchangeOrFinish(g, target, ctx);
            }

            // Same color → ask for meld order.
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

        // Phase 3: order resolved.
        if (ctx.HandlerState is TwoPickOrder twoPick)
        {
            var orderReq = (SelectCardOrderRequest)ctx.PendingChoice!;
            ctx.PendingChoice = null;

            var ordered = Mechanics.ValidateOrder(orderReq.ChosenOrder, twoPick.Picks);
            // ChosenOrder is final top-first; reverse for melds.
            for (int i = ordered.Count - 1; i >= 0; i--)
            {
                Mechanics.Meld(g, target, ordered[i]);
                if (g.IsGameOver) { ctx.HandlerState = null; return true; }
            }

            return OfferExchangeOrFinish(g, target, ctx);
        }

        // Phase 4: yes/no answered.
        var wait = (AwaitingExchange)ctx.HandlerState!;
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

    /// <summary>
    /// Post-meld exchange offer. Returns true if the handler is "done"
    /// (with or without offering the exchange). Caller has already
    /// completed the 2-card meld.
    /// </summary>
    private static bool OfferExchangeOrFinish(GameState g, PlayerState target, DogmaContext ctx)
    {
        var redStack = target.Stack(CardColor.Red);
        if (redStack.IsEmpty || g.Players.Length <= 1)
        {
            ctx.HandlerState = null;
            return true;
        }

        // Auto-pick next seat (TODO: real active-player choice).
        int opponentIndex = (target.Index + 1) % g.Players.Length;
        ctx.HandlerState = new AwaitingExchange { OpponentIndex = opponentIndex };
        ctx.PendingChoice = new YesNoChoiceRequest
        {
            Prompt = $"Road Building: transfer your top red card to player "
                   + $"{opponentIndex + 1}, in exchange for their top green card?",
            PlayerIndex = target.Index,
        };
        ctx.Paused = true;
        return true;
    }
}
