namespace Innovation.Core.Handlers;

/// <summary>
/// Physics (age 5, Blue/Lightbulb): "Draw three 6 and reveal them. If
/// two or more of the drawn cards are of the same color, return the
/// drawn cards and all the cards in your hand. Otherwise, keep them."
///
/// Entirely deterministic.
/// </summary>
public sealed class PhysicsHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        var drawn = new List<int>(3);
        for (int i = 0; i < 3; i++)
        {
            int id = Mechanics.DrawFromAge(g, target, 6);
            if (id < 0 || g.IsGameOver) return true;
            drawn.Add(id);
        }

        var seen = new HashSet<CardColor>();
        bool anyDup = false;
        foreach (var id in drawn)
        {
            if (!seen.Add(g.Cards[id].Color)) { anyDup = true; break; }
        }
        if (!anyDup) return true;

        var handSnapshot = target.Hand.ToArray();
        foreach (var id in handSnapshot)
            Mechanics.Return(g, target, id);
        return true;
    }
}
