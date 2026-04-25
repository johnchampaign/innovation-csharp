namespace Innovation.Core.Handlers;

/// <summary>
/// Oars second effect (age 1, Red/Crown, non-demand): "If no cards were
/// transferred due to this demand, draw a 1."
///
/// Mirrors VB6 main.frm 4464–4470. The "was anything transferred" signal
/// comes from <see cref="DogmaContext.DemandSuccessful"/>, which the
/// first effect (<see cref="OarsDemandHandler"/>) sets. Because the flag
/// is on the shared <see cref="DogmaContext"/>, it aggregates across all
/// demand-targets — if <em>any</em> of them transferred a card, this
/// effect does nothing for <em>any</em> share-eligible player, matching
/// VB6's single <c>demand_met</c> variable.
///
/// Runs for the activator (always) and for each other player with ≥ the
/// activator's Castle icons (shared effect). The engine itself handles
/// affected-player selection; this handler only checks the flag.
/// </summary>
public sealed class OarsDrawIfNoDemandHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (ctx.DemandSuccessful) return false;   // someone transferred → skip

        int drawn = Mechanics.DrawFromAge(g, target, 1);
        return drawn >= 0 || g.IsGameOver;
    }
}
