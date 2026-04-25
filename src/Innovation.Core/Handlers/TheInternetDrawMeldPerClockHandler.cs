namespace Innovation.Core.Handlers;

/// <summary>
/// The Internet (age 10, Purple/Clock) — effect 3: "Draw and meld a 10 for
/// every two [Clock] icons on your board."
/// </summary>
public sealed class TheInternetDrawMeldPerClockHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        int clocks = IconCounter.Count(target, Icon.Clock, g.Cards);
        int n = clocks / 2;
        for (int i = 0; i < n; i++)
        {
            Mechanics.DrawAndMeld(g, target, 10);
            if (g.IsGameOver) return true;
        }
        return n > 0;
    }
}
