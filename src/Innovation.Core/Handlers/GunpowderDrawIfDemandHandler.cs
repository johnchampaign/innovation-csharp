namespace Innovation.Core.Handlers;

/// <summary>
/// Gunpowder (age 4, Red/Factory) — non-demand: "If any card was
/// transferred due to the demand, draw and score a 2."
/// </summary>
public sealed class GunpowderDrawIfDemandHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (!ctx.DemandSuccessful) return false;
        Mechanics.DrawAndScore(g, target, 2);
        return true;
    }
}
