namespace Innovation.Core.Handlers;

/// <summary>
/// Computers (age 9, Blue/Clock) — effect 2: "Draw and meld a 10, then
/// execute its non-demand dogma effects for yourself only."
/// </summary>
public sealed class ComputersDrawMeldExecuteHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        int id = Mechanics.DrawAndMeld(g, target, 10);
        if (id < 0 || g.IsGameOver) return true;
        Mechanics.ExecuteSelfOnly(ctx, id, target);
        return true;
    }
}
