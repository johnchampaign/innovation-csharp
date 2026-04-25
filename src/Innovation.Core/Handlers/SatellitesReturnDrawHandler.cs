namespace Innovation.Core.Handlers;

/// <summary>
/// Satellites (age 9, Green/Clock) — effect 1: "Return all cards from your
/// hand, and draw three 8s."
/// </summary>
public sealed class SatellitesReturnDrawHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        foreach (var id in target.Hand.ToArray())
            Mechanics.Return(g, target, id);
        for (int i = 0; i < 3; i++)
        {
            Mechanics.DrawFromAge(g, target, 8);
            if (g.IsGameOver) return true;
        }
        return true;
    }
}
