namespace Innovation.Core.Handlers;

/// <summary>
/// Ecology (age 9, Yellow/Lightbulb): "You may return a card from your hand.
/// If you do, score a card from your hand and draw two 10s."
/// </summary>
public sealed class EcologyHandler : IDogmaHandler
{
    private enum Stage { PickReturn, PickScore }

    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        var stage = (Stage?)ctx.HandlerState ?? Stage.PickReturn;

        if (stage == Stage.PickReturn)
        {
            if (target.Hand.Count == 0) return false;

            if (ctx.PendingChoice is null)
            {
                ctx.PendingChoice = new SelectHandCardRequest
                {
                    Prompt = "Ecology: return a card from your hand (then score a card and draw two 10s)?",
                    PlayerIndex = target.Index,
                    EligibleCardIds = target.Hand.ToArray(),
                    AllowNone = true,
                };
                ctx.HandlerState = Stage.PickReturn;
                ctx.Paused = true;
                return false;
            }

            var r = (SelectHandCardRequest)ctx.PendingChoice;
            ctx.PendingChoice = null;
            if (r.ChosenCardId is not int rid) { ctx.HandlerState = null; return false; }
            Mechanics.Return(g, target, rid);
            if (g.IsGameOver) { ctx.HandlerState = null; return true; }

            if (target.Hand.Count == 0)
            {
                // No card to score — still draw two 10s.
                Mechanics.DrawFromAge(g, target, 10);
                if (!g.IsGameOver) Mechanics.DrawFromAge(g, target, 10);
                ctx.HandlerState = null;
                return true;
            }

            ctx.PendingChoice = new SelectHandCardRequest
            {
                Prompt = "Ecology: choose a card to score.",
                PlayerIndex = target.Index,
                EligibleCardIds = target.Hand.ToArray(),
                AllowNone = false,
            };
            ctx.HandlerState = Stage.PickScore;
            ctx.Paused = true;
            return true;
        }

        var sr = (SelectHandCardRequest)ctx.PendingChoice!;
        ctx.PendingChoice = null;
        ctx.HandlerState = null;
        if (sr.ChosenCardId is int sid) Mechanics.Score(g, target, sid);
        if (g.IsGameOver) return true;
        Mechanics.DrawFromAge(g, target, 10);
        if (!g.IsGameOver) Mechanics.DrawFromAge(g, target, 10);
        return true;
    }
}
