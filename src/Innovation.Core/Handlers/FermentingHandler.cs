namespace Innovation.Core.Handlers;

/// <summary>
/// Fermenting (age 2, Yellow/Leaf): "Draw a 2 for every two [Leaf] icons
/// on your board."
///
/// Count includes the card being dogma'd (Fermenting itself). Integer
/// division by 2: 0–1 leaves → 0 draws, 2–3 → 1 draw, 4–5 → 2 draws, …
/// </summary>
public sealed class FermentingHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        int leaves = IconCounter.Count(target, Icon.Leaf, g.Cards);
        int draws = leaves / 2;
        if (draws == 0) return false;
        bool progressed = false;
        for (int i = 0; i < draws; i++)
        {
            int id = Mechanics.DrawFromAge(g, target, 2);
            if (id >= 0) progressed = true;
            if (g.IsGameOver) break;
        }
        return progressed;
    }
}
