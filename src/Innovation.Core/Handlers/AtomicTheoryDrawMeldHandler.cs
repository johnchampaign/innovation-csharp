namespace Innovation.Core.Handlers;

/// <summary>
/// Atomic Theory (age 6, Blue/Lightbulb) — effect 2: "Draw and meld a 7."
/// </summary>
public sealed class AtomicTheoryDrawMeldHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        int id = Mechanics.DrawAndMeld(g, target, 7);
        return id >= 0;
    }
}
