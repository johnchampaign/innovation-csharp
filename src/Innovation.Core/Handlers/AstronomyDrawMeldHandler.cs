namespace Innovation.Core.Handlers;

/// <summary>
/// Astronomy (age 5, Purple/Lightbulb) — effect 1: "Draw and reveal a 6.
/// If the card is green or blue, meld it and repeat this dogma effect."
///
/// Non-green/non-blue cards stay in hand. Deterministic loop.
/// </summary>
public sealed class AstronomyDrawMeldHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        bool progressed = false;
        while (true)
        {
            int id = Mechanics.DrawFromAge(g, target, 6);
            if (id < 0 || g.IsGameOver) return true;
            progressed = true;
            var color = g.Cards[id].Color;
            if (color != CardColor.Green && color != CardColor.Blue) return progressed;
            Mechanics.Meld(g, target, id);
            if (g.IsGameOver) return true;
        }
    }
}
