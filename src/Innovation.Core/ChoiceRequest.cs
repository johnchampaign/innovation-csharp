namespace Innovation.Core;

/// <summary>
/// A question the <see cref="DogmaEngine"/> needs answered before a handler
/// can continue. Set on <see cref="DogmaContext.PendingChoice"/> when the
/// handler pauses; the caller (UI or AI) fills in the <c>Chosen…</c>
/// property, clears <see cref="DogmaContext.Paused"/>, and calls
/// <see cref="DogmaEngine.Resume"/>.
///
/// The typed subclasses keep the API simple while letting handlers describe
/// constraints (legal cards, optional vs required, min/max counts, etc.) so
/// the caller doesn't have to re-derive them.
/// </summary>
public abstract class ChoiceRequest
{
    /// <summary>Human-readable prompt. Mainly for the UI; AI ignores it.</summary>
    public string Prompt { get; init; } = "";

    /// <summary>Player making the choice (almost always the current target).</summary>
    public int PlayerIndex { get; init; }
}

/// <summary>
/// "Choose one card from your hand (or none)." Used by Agriculture-style
/// dogmas. <see cref="ChosenCardId"/> is <c>null</c> when the caller declines
/// an optional pick — only legal if <see cref="AllowNone"/> is true.
/// </summary>
public sealed class SelectHandCardRequest : ChoiceRequest
{
    /// <summary>Legal picks. A handler builds this from the player's hand.</summary>
    public IReadOnlyList<int> EligibleCardIds { get; init; } = Array.Empty<int>();

    /// <summary>Whether the caller may decline (pass "null" as the answer).</summary>
    public bool AllowNone { get; init; }

    /// <summary>Caller writes this before resuming. Null means "declined".</summary>
    public int? ChosenCardId { get; set; }
}

/// <summary>
/// "Choose any subset of cards from your hand (between Min and Max)." Used
/// by Pottery and Masonry.
/// </summary>
public sealed class SelectHandCardSubsetRequest : ChoiceRequest
{
    public IReadOnlyList<int> EligibleCardIds { get; init; } = Array.Empty<int>();
    public int MinCount { get; init; }
    public int MaxCount { get; init; }

    /// <summary>Caller writes this before resuming.</summary>
    public IReadOnlyList<int> ChosenCardIds { get; set; } = Array.Empty<int>();
}

/// <summary>
/// "Choose one card from a score pile (or none)." Score pile contents are
/// open information, so the source (self vs opponent) is encoded as a
/// regular card id list like <see cref="SelectHandCardRequest"/>.
/// Used by Mapmaking (defender picks which age-1 card to give up) and
/// Optics (activator picks which card to transfer).
/// </summary>
public sealed class SelectScoreCardRequest : ChoiceRequest
{
    public IReadOnlyList<int> EligibleCardIds { get; init; } = Array.Empty<int>();
    public bool AllowNone { get; init; }
    public int? ChosenCardId { get; set; }
}

/// <summary>Simple yes/no confirmation — e.g. Code of Laws's splay-or-not.</summary>
public sealed class YesNoChoiceRequest : ChoiceRequest
{
    /// <summary>Caller writes this before resuming.</summary>
    public bool ChosenYes { get; set; }
}

/// <summary>
/// "Pick a new top-to-bottom order for a stack." Used by Publications to
/// rearrange a pile. Callers may return the same order unchanged.
/// </summary>
public sealed class SelectStackOrderRequest : ChoiceRequest
{
    /// <summary>The color of the stack being reordered.</summary>
    public CardColor Color { get; init; }

    /// <summary>Current top-to-bottom card ids (information for the caller).</summary>
    public IReadOnlyList<int> CurrentOrder { get; init; } = Array.Empty<int>();

    /// <summary>
    /// Caller writes a permutation of <see cref="CurrentOrder"/> representing
    /// the new top-to-bottom order. Setting the same list is a no-op.
    /// </summary>
    public IReadOnlyList<int> ChosenOrder { get; set; } = Array.Empty<int>();
}

/// <summary>
/// "Choose a card value (age 1-10)." Used by Mass Media to pick which age of
/// cards to return from every score pile.
/// </summary>
public sealed class SelectValueRequest : ChoiceRequest
{
    public IReadOnlyList<int> EligibleValues { get; init; } = Array.Empty<int>();
    public bool AllowNone { get; init; }
    public int? ChosenValue { get; set; }
}

/// <summary>
/// "Choose one of these colors on your board." Used by City States (pick a
/// top-castle to give up) and anything else that demands the caller name a
/// color pile without singling out an individual card.
/// </summary>
public sealed class SelectColorRequest : ChoiceRequest
{
    /// <summary>Legal color picks.</summary>
    public IReadOnlyList<CardColor> EligibleColors { get; init; } = Array.Empty<CardColor>();

    /// <summary>
    /// If true, <see cref="ChosenColor"/>=null is a valid "declined" answer
    /// (e.g., Philosophy's optional splay). Demands and mandatory picks
    /// leave this false, and controllers must return a concrete color.
    /// </summary>
    public bool AllowNone { get; init; }

    /// <summary>Caller writes this before resuming. Null means "declined".</summary>
    public CardColor? ChosenColor { get; set; }
}
