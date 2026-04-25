namespace Innovation.Core.Handlers;

/// <summary>
/// Combustion (age 7, Red/Crown, demand): "I demand you transfer two cards
/// from your score pile to my score pile!" Target picks which cards. If
/// the target has fewer than two, transfer what they have.
/// </summary>
public sealed class CombustionDemandHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (ctx.PendingChoice is null)
        {
            if (target.ScorePile.Count == 0) return false;
            int n = Math.Min(2, target.ScorePile.Count);
            ctx.PendingChoice = new SelectHandCardSubsetRequest
            {
                Prompt = $"Combustion: transfer {n} card(s) from your score pile to "
                       + $"player {ctx.ActivatingPlayerIndex + 1}'s score pile.",
                PlayerIndex = target.Index,
                EligibleCardIds = target.ScorePile.ToArray(),
                MinCount = n,
                MaxCount = n,
            };
            ctx.Paused = true;
            return false;
        }

        var req = (SelectHandCardSubsetRequest)ctx.PendingChoice;
        ctx.PendingChoice = null;

        var activator = g.Players[ctx.ActivatingPlayerIndex];
        foreach (var id in req.ChosenCardIds)
            Mechanics.TransferScoreToScore(g, target, activator, id);

        if (req.ChosenCardIds.Count > 0) ctx.DemandSuccessful = true;
        return true;
    }
}
