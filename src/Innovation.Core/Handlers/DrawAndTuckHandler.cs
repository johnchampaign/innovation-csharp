namespace Innovation.Core.Handlers;

/// <summary>
/// "Draw and tuck a [age]" — draw one card (optionally from a specific age
/// deck, walking up if empty) and immediately tuck it. Used by Monotheism's
/// second effect.
/// </summary>
public sealed class DrawAndTuckHandler : IDogmaHandler
{
    public int Count { get; }
    public int? StartingAge { get; }

    public DrawAndTuckHandler(int count = 1, int? startingAge = null)
    {
        if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
        Count = count;
        StartingAge = startingAge;
    }

    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        bool progressed = false;
        for (int i = 0; i < Count; i++)
        {
            int id = StartingAge.HasValue
                ? Mechanics.DrawFromAge(g, target, StartingAge.Value)
                : Mechanics.Draw(g, target);
            if (id < 0 || g.IsGameOver) return progressed;
            Mechanics.Tuck(g, target, id);
            progressed = true;
            if (g.IsGameOver) break;
        }
        return progressed;
    }
}
