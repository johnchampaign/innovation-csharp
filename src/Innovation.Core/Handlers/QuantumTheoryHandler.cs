namespace Innovation.Core.Handlers;

/// <summary>
/// Quantum Theory (age 8, Blue/Clock): "You may return up to two cards from
/// your hand. If you return two, draw a 10 and then draw and score a 10."
/// </summary>
public sealed class QuantumTheoryHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (target.Hand.Count == 0) return false;

        if (ctx.PendingChoice is null)
        {
            ctx.PendingChoice = new SelectHandCardSubsetRequest
            {
                Prompt = "Quantum Theory: return up to two cards. If you return two, draw a 10 and draw+score a 10.",
                PlayerIndex = target.Index,
                EligibleCardIds = target.Hand.ToArray(),
                MinCount = 0,
                MaxCount = Math.Min(2, target.Hand.Count),
            };
            ctx.Paused = true;
            return false;
        }

        var req = (SelectHandCardSubsetRequest)ctx.PendingChoice;
        ctx.PendingChoice = null;

        if (req.ChosenCardIds.Count == 0) return false;

        foreach (var id in req.ChosenCardIds)
            Mechanics.Return(g, target, id);

        if (req.ChosenCardIds.Count < 2) return true;

        Mechanics.DrawFromAge(g, target, 10);
        if (g.IsGameOver) return true;
        Mechanics.DrawAndScore(g, target, 10);
        return true;
    }
}
