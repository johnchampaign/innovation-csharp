namespace Innovation.Core;

/// <summary>
/// Drives the turn loop: initial meld, action execution, turn advancement.
/// Mirrors VB6 <c>play_game</c> + <c>cmdDraw_Click</c> + <c>achieve</c> + the
/// per-action decrement pattern (main.frm 3333, 3448, 8105, 8701).
///
/// Dogma resolution is out of scope for this class — <see cref="Apply"/>
/// throws on <see cref="DogmaAction"/> until Phase 3b wires in the effect
/// engine. All other actions fully execute here.
/// </summary>
public sealed class TurnManager
{
    private readonly GameState _g;
    private readonly CardRegistry _registry;
    private readonly DogmaEngine _dogma;

    /// <summary>
    /// Most recent dogma resolution context. Null until a dogma action has
    /// fired. Exposed so callers can tell whether the engine paused awaiting
    /// input (<see cref="DogmaContext.Paused"/>) or ran to completion.
    /// </summary>
    public DogmaContext? PendingDogma { get; private set; }

    public TurnManager(GameState g)
    {
        _g = g;
        _registry = new CardRegistry(g.Cards);
        CardRegistrations.RegisterAll(_registry, g.Cards);
        _dogma = new DogmaEngine(g, _registry);
    }

    /// <summary>
    /// Resolve the opening meld for all players. Each player picks one card
    /// from their starting hand; the player whose card has the
    /// alphabetically-lowest title takes turn 1 with 1 action. Mirrors
    /// the VB6 "initial_meld" phase (main.frm 8093–8110).
    /// </summary>
    /// <param name="meldChoices">
    /// One card ID per player, in player-index order. Each must be in that
    /// player's hand.
    /// </param>
    public void CompleteInitialMeld(IReadOnlyList<int> meldChoices)
    {
        if (meldChoices.Count != _g.Players.Length)
            throw new ArgumentException("Need exactly one meld choice per player.", nameof(meldChoices));

        string? lowestTitle = null;
        int startingPlayer = 0;
        for (int i = 0; i < _g.Players.Length; i++)
        {
            var p = _g.Players[i];
            int cardId = meldChoices[i];
            if (!p.Hand.Contains(cardId))
                throw new InvalidOperationException($"Player {i} does not hold card {cardId}.");
            Mechanics.Meld(_g, p, cardId);

            var title = _g.Cards[cardId].Title;
            if (lowestTitle is null || string.Compare(title, lowestTitle, StringComparison.Ordinal) < 0)
            {
                lowestTitle = title;
                startingPlayer = i;
            }
        }

        _g.ActivePlayer = startingPlayer;
        _g.CurrentTurn = 1;
        _g.ActionsRemaining = 1;
        _g.Phase = GamePhase.Action;
        GameLog.Log($"[state] {GameStateCodec.Encode(_g)}");
    }

    /// <summary>
    /// Perform an action for the active player. Throws if no actions remain,
    /// the phase is wrong, or the action is invalid.
    /// </summary>
    public void Apply(PlayerAction action)
    {
        if (_g.Phase != GamePhase.Action)
            throw new InvalidOperationException($"Cannot take action in phase {_g.Phase}.");
        if (_g.ActionsRemaining <= 0)
            throw new InvalidOperationException("No actions remaining.");

        var p = _g.Active;
        GameLog.Log($"— {GameLog.P(p)} action ({action.GetType().Name}, turn {_g.CurrentTurn}, {_g.ActionsRemaining} left)");
        switch (action)
        {
            case DrawAction:
                Mechanics.Draw(_g, p);
                break;

            case MeldAction m:
                if (!p.Hand.Contains(m.CardId))
                    throw new InvalidOperationException($"Card {m.CardId} not in hand.");
                Mechanics.Meld(_g, p, m.CardId);
                break;

            case AchieveAction a:
                if (!AchievementRules.Claim(_g, p, a.Age))
                    throw new InvalidOperationException($"Cannot claim achievement age {a.Age}.");
                GameLog.Log($"{GameLog.P(p)} claims age-{a.Age} achievement");
                break;

            case DogmaAction d:
                ExecuteDogma(p, d.Color);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(action));
        }

        // Achievement wins are already checked inside Claim(); the draw path
        // may have ended the game by exhausting the age-10 deck. If either
        // fired, don't decrement or advance — the game is over.
        if (_g.IsGameOver) return;

        // If a dogma action paused awaiting human input, leave the turn in
        // place — the caller will call Resume*() once the choice is in.
        if (PendingDogma is { IsComplete: false }) return;

        _g.ActionsRemaining--;
        if (_g.ActionsRemaining == 0) AdvanceTurn();
    }

    /// <summary>
    /// Continue a paused dogma. Decrement the action counter and advance the
    /// turn if the engine runs to completion. Throws if no dogma is pending
    /// or the caller hasn't cleared <see cref="DogmaContext.Paused"/>.
    /// </summary>
    public void ResumeDogma()
    {
        if (PendingDogma is null || PendingDogma.IsComplete)
            throw new InvalidOperationException("No pending dogma to resume.");

        _dogma.Resume(PendingDogma);
        if (!PendingDogma.IsComplete) return;   // still paused further

        // Order matters: don't overwrite GameOver. A handler can end the game
        // mid-dogma (e.g. tucking the 6th card claims Monument and trips the
        // achievement-count win). Clobbering Phase here would silently undo
        // that, causing the turn to keep going.
        if (_g.IsGameOver) return;
        _g.Phase = GamePhase.Action;

        _g.ActionsRemaining--;
        if (_g.ActionsRemaining == 0) AdvanceTurn();
    }

    /// <summary>
    /// Resolve a dogma action: activate the top card of the named color for
    /// the given player, driving the <see cref="DogmaEngine"/>. Throws if the
    /// pile is empty. If the engine pauses awaiting human input, the action
    /// counter is NOT decremented here — the caller is expected to drive the
    /// pending <see cref="DogmaContext"/> to completion before the turn
    /// advances. (Today no handlers pause; this is scaffolding for later.)
    /// </summary>
    private void ExecuteDogma(PlayerState p, CardColor color)
    {
        var stack = p.Stack(color);
        if (stack.IsEmpty)
            throw new InvalidOperationException($"No {color} pile to activate.");

        _g.Phase = GamePhase.Dogma;
        PendingDogma = _dogma.Execute(p.Index, stack.Top);

        // If the engine paused mid-resolution we leave Phase=Dogma and return.
        // The normal action-decrement/turn-advance logic below (in Apply)
        // will still run for fully-resolved dogmas; a pause short-circuits by
        // throwing from Apply via the completion check.
        if (!PendingDogma.IsComplete) return;

        // Same ordering as ResumeDogma: a dogma that ends the game (e.g.
        // tucking the 6th card claims Monument) must not have its GameOver
        // phase clobbered by the Action reset.
        if (_g.IsGameOver) return;
        _g.Phase = GamePhase.Action;
    }

    /// <summary>
    /// Move to the next player's turn. Mirrors VB6 play_game lines 3353–3356.
    /// Turn 1 gets 1 action; all later turns get 2, except a 4-player game's
    /// turn 2 which also gets 1 (preserves the VB6 behavior exactly).
    /// </summary>
    private void AdvanceTurn()
    {
        // Reset Monument trackers for every player — a demand/share effect
        // can cause any player to score or tuck, so every counter gets
        // cleared. Matches VB6 play_game lines 3342–3346.
        foreach (var pl in _g.Players)
        {
            pl.ScoredThisTurn = 0;
            pl.TuckedThisTurn = 0;
        }

        _g.CurrentTurn++;
        _g.ActivePlayer = (_g.ActivePlayer + 1) % _g.Players.Length;
        _g.ActionsRemaining = 2;
        if (_g.CurrentTurn == 2 && _g.Players.Length == 4)
            _g.ActionsRemaining = 1;
        if (!_g.IsGameOver)
            GameLog.Log($"[state] {GameStateCodec.Encode(_g)}");
    }
}
