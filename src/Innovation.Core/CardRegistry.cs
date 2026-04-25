namespace Innovation.Core;

/// <summary>
/// Maps card ID to its <see cref="DogmaDefinition"/>. Cards we haven't
/// hand-ported yet fall back to <see cref="PlaceholderHandler"/> so the
/// engine can still drive turns (the effect is a no-op, but the action
/// counter decrements normally).
///
/// The VB6 equivalent is the gigantic switch in <c>perform_dogma_effect</c>
/// that selects by card ID; we break it apart into per-card handler objects
/// registered here.
/// </summary>
public sealed class CardRegistry
{
    private readonly Dictionary<int, DogmaDefinition> _byId = new();
    private readonly IReadOnlyList<Card> _cards;

    public CardRegistry(IReadOnlyList<Card> cards)
    {
        _cards = cards;
    }

    public void Register(int cardId, DogmaDefinition def)
    {
        _byId[cardId] = def;
    }

    /// <summary>
    /// Look up the dogma for a card. Unregistered cards return a single
    /// placeholder effect whose featured icon is the one from the card data
    /// (so icon-count comparisons still work) and whose handler is a no-op.
    /// </summary>
    public DogmaDefinition Get(int cardId)
    {
        if (_byId.TryGetValue(cardId, out var def)) return def;

        var card = _cards[cardId];
        // Placeholder: preserve the featured icon so engine-level tests (which
        // depend on demand/share eligibility) still behave. The effect list
        // is a single non-demand no-op so progress is never reported.
        return new DogmaDefinition(
            card.DogmaIcon,
            new[]
            {
                new DogmaEffect(
                    IsDemand: false,
                    Text: "(not yet implemented)",
                    Handler: PlaceholderHandler.Instance),
            });
    }

    public bool IsRegistered(int cardId) => _byId.ContainsKey(cardId);
}

/// <summary>
/// No-op handler used for cards whose effects haven't been ported. Always
/// returns false (no progress, no shared-bonus).
/// </summary>
public sealed class PlaceholderHandler : IDogmaHandler
{
    public static readonly PlaceholderHandler Instance = new();
    private PlaceholderHandler() { }
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx) => false;
}
