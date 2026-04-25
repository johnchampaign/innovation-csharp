namespace Innovation.Core.Handlers;

/// <summary>
/// Collaboration (age 9, Green/Crown) — effect 2: "If you have ten or more
/// green cards on your board, you win."
/// </summary>
public sealed class CollaborationWinHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (target.Stack(CardColor.Green).Count < 10) return false;
        GameLog.Log($"{GameLog.P(target)} wins via Collaboration — 10+ green cards");
        g.Winners.Clear();
        g.Winners.Add(target.Index);
        g.Phase = GamePhase.GameOver;
        return true;
    }
}
