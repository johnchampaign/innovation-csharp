namespace Innovation.Core.Handlers;

/// <summary>
/// Steam Engine (age 5, Yellow/Factory) — non-demand: "Draw and tuck
/// two 4, then score your bottom yellow card."
///
/// Deterministic; bottom-yellow = last index of the Yellow stack after
/// the two tucks (which may themselves have added to it, since age-4
/// yellows exist).
/// </summary>
public sealed class SteamEngineHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        for (int i = 0; i < 2; i++)
        {
            int id = Mechanics.DrawAndTuck(g, target, 4);
            if (id < 0 || g.IsGameOver) return true;
        }

        var yellow = target.Stack(CardColor.Yellow);
        if (yellow.IsEmpty) return true;
        int bottom = yellow.Cards[yellow.Count - 1];
        Mechanics.ScoreFromBoard(g, target, CardColor.Yellow, bottom);
        return true;
    }
}
