namespace Innovation.Core.Handlers;

/// <summary>
/// "Draw and meld a [age]" — draw one card (optionally from a specific age
/// deck, walking up if empty) and immediately meld it. Covers Sailing
/// ("Draw and meld a 1") and the first effect of Tools ("Draw and meld a 3"),
/// repeated for multi-card variants.
///
/// Mirrors VB6 <c>draw_and_meld</c> (referenced by Sailing at main.frm 4522).
/// </summary>
public sealed class DrawAndMeldHandler : IDogmaHandler
{
    public int Count { get; }
    public int? StartingAge { get; }

    public DrawAndMeldHandler(int count = 1, int? startingAge = null)
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
            Mechanics.Meld(g, target, id);
            progressed = true;
            if (g.IsGameOver) break;
        }
        return progressed;
    }
}
