namespace Innovation.Core.Handlers;

/// <summary>
/// Metalworking (age 1, Red/Castle): "Draw and reveal a 1. If it has a
/// [Castle], score it and repeat this dogma effect. Otherwise, keep it."
///
/// VB6 main.frm 4436–4453: a <c>while count = 0</c> loop that draws from
/// age 1, scores the card if any of its icons is a Castle, and exits on
/// the first castle-free draw. Bug-1 guard: if the age-1 deck and every
/// higher deck are empty, <c>Mechanics.DrawFromAge</c> returns -1 and we
/// stop; the original VB6 would have infinite-looped because draw_num
/// returned 0 (Agriculture) every time.
/// </summary>
public sealed class MetalworkingHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        bool progressed = false;
        while (true)
        {
            int id = Mechanics.DrawFromAge(g, target, 1);
            if (id < 0) return progressed;   // game just ended
            progressed = true;

            if (CardHasCastle(g.Cards[id]))
            {
                Mechanics.Score(g, target, id);
                if (g.IsGameOver) return progressed;
                continue;    // loop again
            }

            // Non-castle draw — stays in hand, stop.
            return progressed;
        }
    }

    private static bool CardHasCastle(Card c) =>
        c.Top == Icon.Castle || c.Left == Icon.Castle ||
        c.Middle == Icon.Castle || c.Right == Icon.Castle;
}
