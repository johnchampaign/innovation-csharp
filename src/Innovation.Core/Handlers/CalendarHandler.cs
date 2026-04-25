namespace Innovation.Core.Handlers;

/// <summary>
/// Calendar (age 2, Blue/Leaf): "If you have more cards in your score pile
/// than in your hand, draw two 3s."
/// </summary>
public sealed class CalendarHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (target.ScorePile.Count <= target.Hand.Count) return false;
        bool progressed = false;
        for (int i = 0; i < 2; i++)
        {
            int id = Mechanics.DrawFromAge(g, target, 3);
            if (id >= 0) progressed = true;
            if (g.IsGameOver) break;
        }
        return progressed;
    }
}
