namespace Innovation.Core.Handlers;

/// <summary>
/// Empiricism (age 8, Purple/Lightbulb) — effect 2: "If you have twenty or
/// more [Lightbulb] icons on your board, you win."
///
/// Special win condition; skips the normal score/achievement tally.
/// </summary>
public sealed class EmpiricismWinHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        int count = IconCounter.Count(target, Icon.Lightbulb, g.Cards);
        if (count < 20) return false;

        GameLog.Log($"{GameLog.P(target)} wins via Empiricism — {count} [Lightbulb] icons");
        g.Winners.Clear();
        g.Winners.Add(target.Index);
        g.Phase = GamePhase.GameOver;
        return true;
    }
}
