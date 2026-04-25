namespace Innovation.Core.Handlers;

/// <summary>
/// Coal (age 5, Red/Factory) — effect 1: "Draw and tuck a 5."
/// </summary>
public sealed class CoalTuckHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        int id = Mechanics.DrawAndTuck(g, target, 5);
        return id >= 0;
    }
}
