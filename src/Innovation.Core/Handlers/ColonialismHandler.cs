namespace Innovation.Core.Handlers;

/// <summary>
/// Colonialism (age 4, Red/Factory): "Draw and tuck a 3. If it has a
/// [Crown], repeat this dogma effect."
///
/// Entirely deterministic — iterative loop, stopping when a tucked card
/// has no Crown or the deck runs out.
/// </summary>
public sealed class ColonialismHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        while (true)
        {
            int id = Mechanics.DrawAndTuck(g, target, 3);
            if (id < 0 || g.IsGameOver) return true;
            if (!Mechanics.HasIcon(g.Cards[id], Icon.Crown)) return true;
        }
    }
}
