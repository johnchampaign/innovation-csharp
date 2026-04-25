namespace Innovation.Core;

/// <summary>
/// Per-resolution state for a single dogma activation. Lives only while the
/// <see cref="DogmaEngine"/> is walking the card's effects. Mutable because
/// the engine updates it between levels and handlers may flag a pause.
///
/// Mirrors the VB6 <c>perform_dogma_effect</c>/<c>activate_card</c> locals:
/// the featured icon, the affected-player ordering, the "dogma_copied" flag
/// that drives the shared-bonus draw (main.frm 3719+, 4251+).
/// </summary>
public sealed class DogmaContext
{
    /// <summary>Card whose dogma is being resolved.</summary>
    public int CardId { get; }

    /// <summary>Player who activated the dogma.</summary>
    public int ActivatingPlayerIndex { get; }

    /// <summary>Icon used to compute demand/share eligibility.</summary>
    public Icon FeaturedIcon { get; }

    /// <summary>
    /// Effect index (0-based) currently being resolved. Incremented by the
    /// engine as each level finishes.
    /// </summary>
    public int CurrentLevel { get; set; }

    /// <summary>
    /// Index into <see cref="CurrentTargets"/> for the handler currently
    /// executing. The engine uses this so it can resume after a pause.
    /// </summary>
    public int CurrentTargetIndex { get; set; }

    /// <summary>
    /// Ordered list of player indexes the current level will affect. Computed
    /// once at the start of each level (before any handler mutates state) so
    /// icon-count changes mid-level don't change the affected set — matching
    /// VB6's <c>affected_by_dogma</c> matrix.
    /// </summary>
    public IReadOnlyList<int> CurrentTargets { get; set; } = Array.Empty<int>();

    /// <summary>
    /// Set to true once a non-active player has progressed a non-demand
    /// effect. Triggers the single bonus draw for the activating player after
    /// every level resolves. Matches VB6 <c>dogma_copied</c>.
    /// </summary>
    public bool SharedBonus { get; set; }

    /// <summary>
    /// Set by a handler that needs human input. The engine stops iterating
    /// and returns control to the caller. The caller records the player's
    /// choice and clears <see cref="Paused"/> before calling
    /// <see cref="DogmaEngine.Resume"/>.
    /// </summary>
    public bool Paused { get; set; }

    /// <summary>
    /// The question the paused handler wants answered, if any. The caller
    /// reads the typed request, fills in the <c>Chosen…</c> property, and
    /// leaves it in place for the handler to pick up on resume. The handler
    /// clears it once consumed.
    /// </summary>
    public ChoiceRequest? PendingChoice { get; set; }

    /// <summary>
    /// Scratch space for multi-step handlers that need to remember where
    /// they were between pauses (e.g. Code of Laws: "I tucked a Purple card,
    /// now I'm waiting on a yes/no for splay-left"). Typed per handler.
    /// </summary>
    public object? HandlerState { get; set; }

    /// <summary>
    /// Set by a demand-effect handler when it successfully carried out its
    /// transfer/steal. Mirrors VB6 <c>demand_met</c> (main.frm 4316, 4493).
    /// Read by later effects of the same dogma that change behaviour based
    /// on whether the demand actually did something — currently just Oars's
    /// second effect ("If no cards were transferred due to this demand,
    /// draw a 1.").
    ///
    /// Survives across level transitions because DogmaContext does, and
    /// neither <see cref="DogmaEngine"/> nor any handler resets it between
    /// levels.
    /// </summary>
    public bool DemandSuccessful { get; set; }

    /// <summary>True once <see cref="DogmaEngine"/> has walked every level.</summary>
    public bool IsComplete { get; set; }

    /// <summary>
    /// Stack of "execute card X's non-demand effects for player Y only"
    /// invocations queued by handlers (Robotics, Self Service, Computers,
    /// Software, Satellites). The engine drains the stack after each primary
    /// handler call; frames can themselves push further frames.
    ///
    /// Each frame carries its own handler scratch state so nested handlers
    /// don't clobber their caller's <see cref="HandlerState"/>.
    /// </summary>
    public Stack<NestedDogmaFrame> NestedFrames { get; } = new();

    public DogmaContext(int cardId, int activatingPlayerIndex, Icon featuredIcon)
    {
        CardId = cardId;
        ActivatingPlayerIndex = activatingPlayerIndex;
        FeaturedIcon = featuredIcon;
    }
}

/// <summary>
/// One "execute card X for player Y, non-demand only" queued by a handler
/// (e.g., Robotics). Only the engine mutates these — handlers push them and
/// move on.
/// </summary>
public sealed class NestedDogmaFrame
{
    public int CardId { get; init; }
    public int TargetPlayerIndex { get; init; }
    public int EffectIndex { get; set; }
    public object? HandlerState { get; set; }
}
