namespace Innovation.Core.Handlers;

/// <summary>
/// Software (age 10, Blue/Clock) — effect 2: "Draw and meld two 10s, then
/// execute the second card's non-demand dogma effects for yourself only."
/// </summary>
public sealed class SoftwareMeldTwoExecuteHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        int a = Mechanics.DrawAndMeld(g, target, 10);
        if (a < 0 || g.IsGameOver) return true;
        int b = Mechanics.DrawAndMeld(g, target, 10);
        if (b < 0 || g.IsGameOver) return true;
        Mechanics.ExecuteSelfOnly(ctx, b, target);
        return true;
    }
}
