namespace Innovation.Core.Handlers;

/// <summary>
/// Compass (age 3, Green/Crown) — demand: "I demand you transfer a top
/// non-green card with a [Leaf] from your board to my board, and then
/// transfer a top card without a [Leaf] from my board to your board."
///
/// Two sequential transfers:
///   • Leg 1 — target picks a non-green color whose top card has a Leaf.
///   • Leg 2 — activator picks a color whose top card has no Leaf.
///     Conditional on leg 1 happening.
///
/// Per the rulebook, each leg's chooser is the owner giving the card up.
/// If a leg has no eligible color, that leg is skipped (but the demand
/// still "counted" if leg 1 moved something).
/// </summary>
public sealed class CompassDemandHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        var activator = g.Players[ctx.ActivatingPlayerIndex];

        // Phase 1: target picks a non-green, Leaf-iconed top card.
        if (ctx.HandlerState is null && ctx.PendingChoice is null)
        {
            var eligible = new List<CardColor>();
            foreach (CardColor c in Enum.GetValues<CardColor>())
            {
                if (c == CardColor.Green) continue;
                var s = target.Stack(c);
                if (s.IsEmpty) continue;
                if (FeudalismDemandHandler.HasIcon(g.Cards[s.Top], Icon.Leaf))
                    eligible.Add(c);
            }
            if (eligible.Count == 0) return false;

            ctx.HandlerState = "leg1";
            ctx.PendingChoice = new SelectColorRequest
            {
                Prompt = $"Compass: transfer a top non-green [Leaf] card to "
                       + $"player {ctx.ActivatingPlayerIndex + 1}'s board.",
                PlayerIndex = target.Index,
                EligibleColors = eligible,
            };
            ctx.Paused = true;
            return false;
        }

        if (ctx.HandlerState as string == "leg1")
        {
            var req = (SelectColorRequest)ctx.PendingChoice!;
            ctx.PendingChoice = null;
            ctx.HandlerState = null;
            if (req.ChosenColor is not CardColor color) return false;

            Mechanics.TransferBoardToBoard(g, target, activator, color);
            ctx.DemandSuccessful = true;
            if (g.IsGameOver) return true;

            // Phase 2: activator picks a non-Leaf top card to give back.
            var eligible2 = new List<CardColor>();
            foreach (CardColor c in Enum.GetValues<CardColor>())
            {
                var s = activator.Stack(c);
                if (s.IsEmpty) continue;
                if (!FeudalismDemandHandler.HasIcon(g.Cards[s.Top], Icon.Leaf))
                    eligible2.Add(c);
            }
            if (eligible2.Count == 0) return true;

            ctx.HandlerState = "leg2";
            ctx.PendingChoice = new SelectColorRequest
            {
                Prompt = $"Compass: player {ctx.ActivatingPlayerIndex + 1}, "
                       + $"transfer a top card without a [Leaf] from your board "
                       + $"to player {target.Index + 1}'s board.",
                PlayerIndex = activator.Index,
                EligibleColors = eligible2,
            };
            ctx.Paused = true;
            return true;
        }

        // Phase 2 resume.
        var r2 = (SelectColorRequest)ctx.PendingChoice!;
        ctx.PendingChoice = null;
        ctx.HandlerState = null;
        if (r2.ChosenColor is not CardColor c2) return true;
        Mechanics.TransferBoardToBoard(g, activator, target, c2);
        return true;
    }
}
