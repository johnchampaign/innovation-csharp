namespace Innovation.Core.Handlers;

/// <summary>
/// Industrialization (age 6, Red/Factory) — effect 1: "Draw and tuck
/// a 6 for every two [Factory] icons on your board."
/// </summary>
public sealed class IndustrializationTuckHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        int factories = IconCounter.Count(target, Icon.Factory, g.Cards);
        int n = factories / 2;
        if (n == 0) return false;
        for (int i = 0; i < n; i++)
        {
            if (Mechanics.DrawAndTuck(g, target, 6) < 0 || g.IsGameOver) return true;
        }
        return true;
    }
}
