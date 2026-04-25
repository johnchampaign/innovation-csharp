using Innovation.Core.Players;

namespace Innovation.Core;

/// <summary>
/// Orchestrates a full game between <see cref="IPlayerController"/>s. Drives
/// the initial meld, asks each controller for its turn-level action, and
/// resolves mid-dogma <see cref="ChoiceRequest"/> prompts by dispatching
/// to the affected seat's controller. Stops when the game ends or the
/// per-run safety limit trips.
///
/// The VB6 equivalent is the main UI event loop — here the controllers
/// are the "UI", abstracted behind a polymorphic interface so AI and human
/// seats share one driver.
/// </summary>
public sealed class GameRunner
{
    private readonly GameState _g;
    private readonly TurnManager _turns;
    private readonly IPlayerController[] _controllers;

    /// <summary>
    /// Hard cap on <see cref="Step"/> iterations in
    /// <see cref="RunToCompletion"/>. The longest sensible Innovation game
    /// would need a few thousand; this guard catches a handler stuck in a
    /// pause loop before it wastes a CI run. Raise if a legitimately long
    /// random game ever trips it.
    /// </summary>
    public int SafetyStepLimit { get; init; } = 10_000;

    /// <summary>
    /// Optional post-step observer. Fires after every top-level action
    /// application, every dogma pause-resume, and once after
    /// <see cref="CompleteInitialMeld"/>. First arg is the seat that
    /// acted (or -1 for setup-level steps); second arg is the applied
    /// action (or null for dogma-resume and initial-meld callbacks).
    ///
    /// Intended for UI refreshes and event logging. AI look-ahead
    /// inside <see cref="Players.GreedyController"/> deliberately
    /// leaves this unset on its trial runners so rollouts don't fire
    /// external side effects.
    /// </summary>
    public Action<int, PlayerAction?>? OnStepCompleted { get; init; }

    /// <summary>
    /// Fires right after a mid-dogma choice is filled in by a controller,
    /// with the fully-resolved request (ChosenXxx set). UIs use this to log
    /// a specific line like "P2 chose Oars" instead of a bare "dogma choice
    /// resolved". Fires before the engine resumes the handler.
    /// </summary>
    public Action<int, ChoiceRequest>? OnChoiceResolved { get; init; }

    /// <summary>
    /// Seat that will be asked to decide on the next <see cref="Step"/>.
    /// If a dogma is paused on a <see cref="ChoiceRequest"/>, that's the
    /// request's target; otherwise it's the active player. UIs use this
    /// to decide whether the *next* step will block on a human prompt —
    /// for example, to skip an "advance" gate when the human is already
    /// driving input directly.
    /// </summary>
    /// <summary>
    /// True when the next <see cref="Step"/> will resolve a mid-dogma
    /// choice prompt rather than apply a fresh top-level action. UIs use
    /// this to skip the between-actions "Continue" gate for choice
    /// resolutions — a mid-dogma prompt is part of the action that
    /// opened it, not a new decision.
    /// </summary>
    public bool IsResolvingChoice =>
        _turns.PendingDogma is { IsComplete: false, Paused: true };

    public int NextActor
    {
        get
        {
            var pending = _turns.PendingDogma;
            if (pending is { IsComplete: false, Paused: true }
                && pending.PendingChoice is { } req)
            {
                return req.PlayerIndex;
            }
            return _g.ActivePlayer;
        }
    }

    public GameRunner(GameState g, IReadOnlyList<IPlayerController> controllers)
    {
        if (controllers.Count != g.Players.Length)
            throw new ArgumentException(
                $"Need exactly {g.Players.Length} controllers, got {controllers.Count}.",
                nameof(controllers));
        _g = g;
        _controllers = controllers.ToArray();
        _turns = new TurnManager(g);
    }

    /// <summary>
    /// Ask each seat for its starting meld and hand the picks off to
    /// <see cref="TurnManager.CompleteInitialMeld"/>.
    /// </summary>
    public void CompleteInitialMeld()
    {
        var picks = new int[_g.Players.Length];
        for (int i = 0; i < _g.Players.Length; i++)
            picks[i] = _controllers[i].ChooseInitialMeld(_g, _g.Players[i]);
        _turns.CompleteInitialMeld(picks);
        OnStepCompleted?.Invoke(-1, null);
    }

    /// <summary>
    /// Pump <see cref="Step"/> until the game is over or the safety limit
    /// trips. Throws if the limit trips — indicates a likely bug somewhere
    /// in a handler's pause/resume protocol.
    /// </summary>
    public void RunToCompletion()
    {
        int steps = 0;
        while (!_g.IsGameOver)
        {
            if (++steps > SafetyStepLimit)
                throw new InvalidOperationException(
                    $"GameRunner exceeded {SafetyStepLimit} steps without finishing.");
            Step();
        }
    }

    /// <summary>
    /// Apply a specific top-level action (bypassing the active seat's
    /// controller) and drive any resulting dogma pause to completion via
    /// the per-seat controllers. Leaves the runner in Action phase on the
    /// next turn (or GameOver). Used by AI look-ahead — a trial runner
    /// wants to inject a chosen move rather than poll its rollout
    /// controllers for the top-level decision.
    /// </summary>
    public void ApplyActionAndResolveDogma(PlayerAction action)
    {
        _turns.Apply(action);
        while (_turns.PendingDogma is { IsComplete: false, Paused: true })
            Step();
    }

    /// <summary>
    /// Advance one decision. Either resolves a paused dogma choice
    /// (dispatch to the affected seat's controller + resume the engine) or
    /// applies a fresh top-level action for the active player.
    /// </summary>
    public void Step()
    {
        var pending = _turns.PendingDogma;
        if (pending is { IsComplete: false, Paused: true })
        {
            // Capture PlayerIndex before ResolvePendingChoice clears
            // PendingChoice — the callback consumer wants to know whose
            // prompt just resolved.
            int actor = pending.PendingChoice?.PlayerIndex ?? -1;
            ResolvePendingChoice(pending);
            OnStepCompleted?.Invoke(actor, null);
            return;
        }

        if (_g.Phase != GamePhase.Action)
            throw new InvalidOperationException(
                $"GameRunner in unexpected phase {_g.Phase} with no pending dogma.");

        var active = _g.Active;
        int activeIndex = active.Index;
        var legal = LegalActions.Enumerate(_g, active);
        var action = _controllers[activeIndex].ChooseAction(_g, active, legal);
        _turns.Apply(action);
        OnStepCompleted?.Invoke(activeIndex, action);
    }

    private void ResolvePendingChoice(DogmaContext ctx)
    {
        var req = ctx.PendingChoice
            ?? throw new InvalidOperationException("DogmaContext.Paused with no PendingChoice.");
        var self = _g.Players[req.PlayerIndex];
        var controller = _controllers[req.PlayerIndex];

        switch (req)
        {
            case SelectHandCardRequest s:
                s.ChosenCardId = controller.ChooseHandCard(_g, self, s);
                break;
            case SelectHandCardSubsetRequest ss:
                ss.ChosenCardIds = controller.ChooseHandCardSubset(_g, self, ss);
                break;
            case YesNoChoiceRequest yn:
                yn.ChosenYes = controller.ChooseYesNo(_g, self, yn);
                break;
            case SelectColorRequest sc:
                sc.ChosenColor = controller.ChooseColor(_g, self, sc);
                break;
            case SelectScoreCardRequest scs:
                scs.ChosenCardId = controller.ChooseScoreCard(_g, self, scs);
                break;
            case SelectStackOrderRequest sso:
                sso.ChosenOrder = controller.ChooseStackOrder(_g, self, sso);
                break;
            case SelectValueRequest sv:
                sv.ChosenValue = controller.ChooseValue(_g, self, sv);
                break;
            default:
                throw new NotSupportedException(
                    $"No dispatch for ChoiceRequest type {req.GetType().Name}.");
        }

        OnChoiceResolved?.Invoke(req.PlayerIndex, req);
        ctx.Paused = false;
        _turns.ResumeDogma();
    }
}
