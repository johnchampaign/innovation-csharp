namespace Innovation.Core.Handlers;

/// <summary>
/// Measurement (age 5, Green/Lightbulb): "You may return a card from
/// your hand. If you do, splay any one color of your cards right, and
/// draw a card of value equal to the number of cards of that color on
/// your board."
///
/// Phase 1: optional return.
/// Phase 2 (only if returned): pick a color — must have ≥2 cards to
/// splay — splay right, then draw-from-age-count.
/// </summary>
public sealed class MeasurementHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        // Phase 1: optional return.
        if (ctx.HandlerState is null && ctx.PendingChoice is null)
        {
            if (target.Hand.Count == 0) return false;

            ctx.HandlerState = "return";
            ctx.PendingChoice = new SelectHandCardRequest
            {
                Prompt = "Measurement: return a card from your hand?",
                PlayerIndex = target.Index,
                EligibleCardIds = target.Hand.ToArray(),
                AllowNone = true,
            };
            ctx.Paused = true;
            return false;
        }

        if (ctx.HandlerState as string == "return")
        {
            var req = (SelectHandCardRequest)ctx.PendingChoice!;
            ctx.PendingChoice = null;
            ctx.HandlerState = null;
            if (req.ChosenCardId is not int rid) return false;
            Mechanics.Return(g, target, rid);
            if (g.IsGameOver) return true;

            var eligible = new List<CardColor>();
            foreach (CardColor c in Enum.GetValues<CardColor>())
            {
                var s = target.Stack(c);
                if (s.Count >= 2 && s.Splay != Splay.Right) eligible.Add(c);
            }
            if (eligible.Count == 0) return true;

            ctx.HandlerState = "splay";
            ctx.PendingChoice = new SelectColorRequest
            {
                Prompt = "Measurement: splay a color right (and draw a card of "
                       + "value equal to that color's size).",
                PlayerIndex = target.Index,
                EligibleColors = eligible,
                AllowNone = true,
            };
            ctx.Paused = true;
            return true;
        }

        // Phase 2 resume.
        var creq = (SelectColorRequest)ctx.PendingChoice!;
        ctx.PendingChoice = null;
        ctx.HandlerState = null;
        if (creq.ChosenColor is not CardColor color) return true;

        if (!Mechanics.Splay(g, target, color, Splay.Right)) return true;
        if (g.IsGameOver) return true;
        int count = target.Stack(color).Count;
        Mechanics.DrawFromAge(g, target, Math.Clamp(count, 1, 10));
        return true;
    }
}
