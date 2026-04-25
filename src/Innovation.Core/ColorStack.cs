namespace Innovation.Core;

/// <summary>
/// One color pile on a player's board. Index 0 is the top card (mirrors the
/// VB6 convention: <c>board(player, color, 0)</c> is the top).
/// </summary>
public sealed class ColorStack
{
    private readonly List<int> _cards = new();

    public Splay Splay { get; private set; } = Splay.None;

    /// <summary>Card IDs from top (index 0) to bottom.</summary>
    public IReadOnlyList<int> Cards => _cards;

    public int Count => _cards.Count;
    public bool IsEmpty => _cards.Count == 0;
    public int Top => _cards.Count > 0 ? _cards[0] : -1;

    /// <summary>Place a card on top (meld).</summary>
    public void Meld(int cardId) => _cards.Insert(0, cardId);

    /// <summary>Place a card on the bottom (tuck).</summary>
    public void Tuck(int cardId) => _cards.Add(cardId);

    /// <summary>Remove and return the top card (e.g. for transfer).</summary>
    public int PopTop()
    {
        var id = _cards[0];
        _cards.RemoveAt(0);
        // VB6 rule: a pile of 1 or 0 cards can't be splayed — reset.
        if (_cards.Count <= 1) Splay = Splay.None;
        return id;
    }

    /// <summary>
    /// Apply a splay. A pile with fewer than 2 cards cannot be splayed; a
    /// splay that matches the current direction is a no-op. Returns true if
    /// the splay actually changed.
    /// </summary>
    public bool ApplySplay(Splay direction)
    {
        if (_cards.Count < 2) return false;
        if (Splay == direction) return false;
        Splay = direction;
        return true;
    }

    /// <summary>
    /// Remove a non-top card from the stack by id. Used by board-score
    /// effects like Coal's "score the card beneath" and Steam Engine's
    /// "bottom yellow". Throws if the card is the top or absent — callers
    /// must use <see cref="PopTop"/> for the top card.
    /// </summary>
    public void RemoveCoveredCard(int cardId)
    {
        int i = _cards.IndexOf(cardId);
        if (i <= 0) throw new InvalidOperationException(
            "RemoveCoveredCard requires a covered (non-top, present) card.");
        _cards.RemoveAt(i);
        if (_cards.Count <= 1) Splay = Splay.None;
    }

    internal void ClearForTest() { _cards.Clear(); Splay = Splay.None; }

    /// <summary>
    /// Replace the stack's contents + splay wholesale. Used only by
    /// <see cref="GameStateCodec"/> when restoring a position from a code —
    /// normal gameplay never needs to resurrect a stack from raw data.
    /// </summary>
    public void RestoreFromCode(IReadOnlyList<int> topToBottom, Splay splay)
    {
        _cards.Clear();
        _cards.AddRange(topToBottom);
        Splay = _cards.Count >= 2 ? splay : Splay.None;
    }

    /// <summary>Remove every card from the stack (Fission's nuclear option).</summary>
    public void ClearForFission() { _cards.Clear(); Splay = Splay.None; }

    /// <summary>
    /// Replace the stack's top-to-bottom order with <paramref name="newOrder"/>,
    /// which must be a permutation of the current contents. Used by
    /// Publications. Splay direction is preserved.
    /// </summary>
    public void ReplaceOrder(IReadOnlyList<int> newOrder)
    {
        if (newOrder.Count != _cards.Count)
            throw new ArgumentException("new order must be a permutation of current contents");
        _cards.Clear();
        _cards.AddRange(newOrder);
    }

    /// <summary>
    /// Independent copy — used by <see cref="GameState.DeepClone"/> for AI
    /// look-ahead. Mutations on the copy don't touch the original.
    /// </summary>
    public ColorStack DeepClone()
    {
        var copy = new ColorStack();
        copy._cards.AddRange(_cards);
        copy.Splay = Splay;
        return copy;
    }
}
