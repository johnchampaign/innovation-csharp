namespace Innovation.Core.Handlers;

/// <summary>
/// "Draw N [age]s" — the simplest dogma shape. Covers The Wheel ("Draw two
/// 1s"), Writing ("Draw a 2"), and similar unconditional draw effects.
///
/// If <see cref="StartingAge"/> is null, the draw floor is the target's
/// highest top card (same as a regular action draw). If it's specified, the
/// draw starts from that deck and walks up per VB6 <c>draw(player, age)</c>.
/// </summary>
public sealed class DrawHandler : IDogmaHandler
{
    public int Count { get; }
    public int? StartingAge { get; }

    public DrawHandler(int count = 1, int? startingAge = null)
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
            if (id >= 0) progressed = true;
            if (g.IsGameOver) break;
        }
        return progressed;
    }
}
