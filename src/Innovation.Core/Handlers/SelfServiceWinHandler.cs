namespace Innovation.Core.Handlers;

/// <summary>
/// Self Service (age 10, Green/Crown) — effect 2: "If you have more
/// achievements than each other player, you win."
/// </summary>
public sealed class SelfServiceWinHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        int mine = target.AchievementCount;
        foreach (var p in g.Players)
        {
            if (p.Index == target.Index) continue;
            if (p.AchievementCount >= mine) return false;
        }
        GameLog.Log($"{GameLog.P(target)} wins via Self Service — most achievements");
        g.Winners.Clear();
        g.Winners.Add(target.Index);
        g.Phase = GamePhase.GameOver;
        return true;
    }
}
