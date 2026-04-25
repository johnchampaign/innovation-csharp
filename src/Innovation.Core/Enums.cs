namespace Innovation.Core;

// Ordered to match VB6 color_lookup() indices (see main.frm line 7452).
// Keeping this ordering lets us eventually snapshot game state as int[] in a
// way that's bit-compatible with the original AI scoring code.
public enum CardColor
{
    Yellow = 0,
    Red = 1,
    Purple = 2,
    Blue = 3,
    Green = 4,
}

// Ordered to match VB6 icon_lookup() indices (see main.frm line 7458).
// 0 is the "x" marker (the hexagon slot, no icon).
public enum Icon
{
    None = 0,
    Leaf = 1,
    Castle = 2,
    Lightbulb = 3,
    Crown = 4,
    Factory = 5,
    Clock = 6,
}

// The four card corner positions. The VB6 icons(card, 0..3) array uses this
// same ordering.
public enum IconSlot
{
    Top = 0,
    Left = 1,
    Middle = 2,
    Right = 3,
}

public enum Splay
{
    None = 0,
    Left,
    Right,
    Up,
}
