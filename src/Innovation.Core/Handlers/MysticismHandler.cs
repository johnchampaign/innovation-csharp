namespace Innovation.Core.Handlers;

/// <summary>
/// Mysticism (age 1, Purple/Castle): "Draw a 1. If it is the same color as
/// any card on your board, meld it and draw a 1."
///
/// Mirrors VB6 main.frm 4455–4461: the meld check is "board has any card
/// of the drawn color" (i.e. the color pile is non-empty), not "the top
/// card matches" — reflected by <c>board(player, color(id), 0) &gt; -1</c>.
/// </summary>
public sealed class MysticismHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        int drawn = Mechanics.DrawFromAge(g, target, 1);
        if (drawn < 0) return false;

        // If the drawn card's color matches any existing pile, meld it and
        // draw another 1.
        var color = g.Cards[drawn].Color;
        if (!target.Stack(color).IsEmpty)
        {
            Mechanics.Meld(g, target, drawn);
            if (!g.IsGameOver) Mechanics.DrawFromAge(g, target, 1);
        }
        return true;   // drawing always counts as progress
    }
}
