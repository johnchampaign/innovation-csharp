namespace Innovation.Core;

/// <summary>
/// Counts visible icons on a player's board according to the splay rules.
/// Mirrors the VB6 update_icon_total logic (main.frm line 7062).
/// </summary>
public static class IconCounter
{
    /// <summary>Icon positions revealed on cards *covered* by the top card.</summary>
    /// <remarks>
    /// Top card always contributes all four slots regardless of splay.
    /// Covered cards contribute only the slots that peek out past the top card.
    /// Unsplayed piles show no covered icons. Innovation uses only Left/Right/Up.
    /// </remarks>
    private static IconSlot[] VisibleSlots(Splay splay) => splay switch
    {
        Splay.Left => new[] { IconSlot.Right },
        Splay.Right => new[] { IconSlot.Top, IconSlot.Left },
        Splay.Up => new[] { IconSlot.Left, IconSlot.Middle, IconSlot.Right },
        _ => Array.Empty<IconSlot>(),
    };

    public static int Count(PlayerState player, Icon icon, IReadOnlyList<Card> cards)
    {
        if (icon == Icon.None) return 0;
        int total = 0;
        foreach (var stack in player.Stacks)
        {
            if (stack.IsEmpty) continue;

            // Top card: all four slots.
            total += CountOnCard(cards[stack.Top], icon, AllSlots);

            // Covered cards: only slots revealed by splay.
            var visible = VisibleSlots(stack.Splay);
            if (visible.Length == 0) continue;
            for (int i = 1; i < stack.Count; i++)
            {
                total += CountOnCard(cards[stack.Cards[i]], icon, visible);
            }
        }
        return total;
    }

    private static readonly IconSlot[] AllSlots =
        { IconSlot.Top, IconSlot.Left, IconSlot.Middle, IconSlot.Right };

    private static int CountOnCard(Card card, Icon icon, IconSlot[] slots)
    {
        int n = 0;
        foreach (var s in slots)
        {
            var i = s switch
            {
                IconSlot.Top => card.Top,
                IconSlot.Left => card.Left,
                IconSlot.Middle => card.Middle,
                IconSlot.Right => card.Right,
                _ => Icon.None,
            };
            if (i == icon) n++;
        }
        return n;
    }
}
