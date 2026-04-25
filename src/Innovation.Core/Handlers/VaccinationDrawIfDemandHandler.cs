namespace Innovation.Core.Handlers;

/// <summary>
/// Vaccination (age 6, Yellow/Leaf) — non-demand: "If any card was
/// returned as a result of the demand, draw and meld a 7."
///
/// Fires for the activator when the demand produced any return.
/// </summary>
public sealed class VaccinationDrawIfDemandHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (!ctx.DemandSuccessful) return false;
        int id = Mechanics.DrawAndMeld(g, target, 7);
        return id >= 0;
    }
}
