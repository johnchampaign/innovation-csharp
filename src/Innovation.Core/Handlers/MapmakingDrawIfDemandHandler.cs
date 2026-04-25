namespace Innovation.Core.Handlers;

/// <summary>
/// Mapmaking second effect (age 2, Yellow/Crown, non-demand): "If any card
/// was transferred due to the demand, draw and score a 1."
///
/// Fires at most once regardless of how many opponents transferred — the
/// demand handler sets <see cref="DogmaContext.DemandSuccessful"/> on the
/// shared context, and this effect just observes the flag.
/// </summary>
public sealed class MapmakingDrawIfDemandHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (!ctx.DemandSuccessful) return false;
        int id = Mechanics.DrawAndScore(g, target, 1);
        return id >= 0 || g.IsGameOver;
    }
}
