# CLAUDE.md — Innovation port

C# implementation of Carl Chudyk's *Innovation* (2010), ported from a VB6
reference (`main.frm`). The VB6 source is the ground truth for rules; when
something seems ambiguous, find the VB6 line number and cite it in a comment.

This document describes how the project is built — conventions, gotchas, and
patterns that are worth preserving. Read it before changing architecture.

---

## Solution layout

```
src/
  Innovation.Core/        Engine, no UI dependencies. net10.0.
  Innovation.Wpf/         WPF shell. net10.0-windows. WinExe.
  Innovation.Tests/       xUnit. References Core only.
  Innovation.WinForms/    Legacy. Prefer WPF for new work.
```

- `Innovation.Core` embeds `Data/cards.tsv` (Windows-1252). It depends on
  `System.Text.Encoding.CodePages` because .NET 5+ doesn't ship code pages.
- `Innovation.Wpf` embeds `Resources/*.jpg` (icons, achievement art).
- Build: `dotnet build`. Test: `dotnet test`.
- Engine has no `DateTime.Now` and no unseeded `Random`. The seed passed to
  `GameSetup.Create` plus the seat-controller seeds fully determine a run.

---

## Architecture boundaries

The engine and UI share **only** small DTOs (`PlayerAction`, `ChoiceRequest`).
They do not share types beyond that. In particular:

- `Innovation.Core` knows nothing about WPF, threads, or the dispatcher.
- `Innovation.Wpf` knows nothing about card mechanics — it observes
  `GameState`, sends `PlayerAction`s, and answers `ChoiceRequest`s.
- The bridge is `IPlayerController` (engine-side) and `IUserPromptSink`
  (UI-side). `HumanController` forwards every prompt to the sink synchronously.

Keep this boundary clean. If you find yourself importing WPF types in Core or
game-rules logic in MainWindow, stop.

---

## Core domain types (Innovation.Core)

| Type | Purpose |
|---|---|
| `GameState` | Root mutable state: `Players[]`, `Decks[1..10]`, `Phase`, `ActivePlayer`, `ActionsRemaining`, `CurrentTurn`, `IsGameOver`. |
| `PlayerState` | Hand, five `ColorStack`s, score pile, achievements, splay flags, per-turn counters (`ScoredThisTurn`, `TuckedThisTurn`). |
| `Card` (record) | Static card data: id, age, color, title, four icon slots, dogma icon, dogma effects. Loaded from `cards.tsv`. |
| `CardColor`, `Icon`, `Splay` | Enums ordered to match VB6 indices. **Don't reorder.** |
| `ColorStack` | LIFO pile + splay state. Top-first storage. |
| `GamePhase` | `{NewGame, Action, Dogma, GameOver}`. |
| `GameRunner` | Step-pump loop. Owns controllers. Fires `OnStepCompleted` and `OnChoiceResolved`. |
| `TurnManager` | Applies `PlayerAction`s; drives `DogmaEngine`. |
| `DogmaEngine` | Walks effect levels, computes demand/share targets, emits shared-bonus draw. |
| `DogmaContext` | Transient: current level/target, `PendingChoice`, `HandlerState`, nested frames, `Paused`, `IsComplete`. |
| `CardRegistry` / `DogmaDefinition` | Card-id → (featured icon, effects[]). Caches handler instances. |
| `Mechanics` | Static helpers: `Draw`, `Meld`, `Score`, `Tuck`, `Return`, `Splay`, `Transfer*`, `DrawAndScore`, `DrawAndMeld`, `DrawAndTuck`, etc. **Always use these — never mutate piles directly.** |
| `AchievementRules` | `Claim()`, `ClaimSpecial()`, `CheckAchievementWin()`. |
| `IconCounter` | Counts an icon across hand/board/score for one player. |
| `GameStateCodec` | Base64 snapshot. **Does not capture mid-dogma state.** Round-trip safe only at turn boundaries. |
| `GameLog` | Static log writer + `OnLine` event for UI mirroring. |
| `IPlayerController` | Engine-side decision interface. Synchronous methods. |
| `PlayerAction` (sealed abstract) | `DrawAction`, `MeldAction`, `AchieveAction`, `DogmaAction`. |
| `ChoiceRequest` (sealed abstract) | `SelectHandCardRequest`, `SelectHandCardSubsetRequest`, `SelectScoreCardRequest`, `SelectColorRequest`, `YesNoChoiceRequest`, `SelectStackOrderRequest`, `SelectValueRequest`. |

---

## Handler conventions

Every dogma effect is an `IDogmaHandler`:

```csharp
bool Execute(GameState g, PlayerState target, DogmaContext ctx);
```

- Returns `true` if the effect "progressed" (drew, scored, transferred, tucked,
  melded, returned, splayed). Used by the engine to decide shared-bonus draws.
- Returns `false` for no-op or user-declined effects.
- `target` is the player currently executing this effect (sharer or activator
  for non-demand; defender for demand). `g.Players[ctx.ActivatingPlayerIndex]`
  is the activator.

### Pause/resume idiom

Handlers that need user input set `ctx.PendingChoice` and `ctx.Paused = true`,
then return `false`. The engine returns control to the caller, who drives the
UI to fill in the choice, clears `Paused`, and calls `Resume`. The handler
runs again, sees the populated choice, applies it, and either pauses for the
next stage or returns.

Stash multi-step state in `ctx.HandlerState` (typed `object?`):

```csharp
// First entry: post the prompt, pause.
if (ctx.PendingChoice is null) {
    ctx.PendingChoice = new SelectHandCardRequest { ... };
    ctx.Paused = true;
    return false;
}

// Resume: read the answer.
var req = (SelectHandCardRequest)ctx.PendingChoice;
ctx.PendingChoice = null;
if (req.ChosenCardId is int id) Mechanics.Score(g, target, id);
return true;
```

Multi-stage handlers (e.g. `AlchemyDrawRevealHandler`) typically use a small
enum or sentinel string in `HandlerState` to track which stage they're in.

### Registration

`CardRegistrations.RegisterAll` keys by **card title**, not id, for resilience
to TSV reordering. Use `Register` (single-effect) or `RegisterMulti`
(multi-effect):

```csharp
Register(r, cards, "Writing", Icon.Lightbulb, isDemand: false,
    text: "Draw a 2.",
    handler: new DrawHandler(count: 1, startingAge: 2));

RegisterMulti(r, cards, "Democracy", Icon.Lightbulb,
    new DogmaEffect(false, "...", new DemocracyReturnHandler()));
```

The `text` field is shown in the UI; keep it short and faithful to the card.

### Demand "you" = target, not activator

In demand text — `"I demand you transfer X! If you do, draw a Y!"` — the
**second sentence's "you" still refers to the target**. The conditional
reward goes to the defender, not the activator. This is a recurring source
of bugs. Six handlers had this wrong (Banking, Corporations, Enterprise,
Mobility, Monotheism, Societies); the comments in those files now spell it
out. When implementing a new demand handler, always pass `target` (not
`activator`) to `Mechanics.Draw*` for the "if you do, draw" clause.

### Share effects: order matters

Share effects iterate over targets in order: each non-active player with
≥ activator's featured-icon count, in clockwise order, then the activator.
Each target runs the **whole effect** before the next target starts.

If you need to compare counts across targets (Democracy: "more cards than any
other player"), evaluate against counts **already recorded by previous
targets** in `ctx.HandlerState`, not against the final tally. Otherwise
you'll silently break the rule that two players can both score in a share.

---

## Player controllers

`IPlayerController` is synchronous. Each `Choose*` method blocks until a
choice is returned. AI controllers compute and return immediately; the
`HumanController` forwards to `IUserPromptSink` and the WPF layer drives a
`TaskCompletionSource` from a click handler.

Implementations:

- `HumanController` — thin forwarder. No WPF dependency; testable with fake
  sinks.
- `GreedyController` — one-ply lookahead via `HeuristicEvaluator`. Scores each
  legal action, picks the highest. Seeded.
- `RandomController` — uniform over legal actions. Seeded.

All seats receive their seed from a shared `Random` derived from the game seed
(see `MainWindow.xaml.cs` ctor) so one input fully determines a run.

---

## WPF UI conventions (Innovation.Wpf/MainWindow.xaml.cs)

- The game loop runs on a background `Task` (`RunGameLoop`). The UI thread
  marshals via `Dispatcher.Invoke` / `Dispatcher.BeginInvoke`.
- AI turns gate on the **Continue button** via a `TaskCompletionSource<bool>`
  (`_continueTcs`). One step per click.
- Continue gating is **only for top-level actions**, not mid-dogma choices.
  An AI that pauses mid-dogma resolves all of its own choices in one step.
- Human turns block in `RunPromptBlocking<T>` — show panel, install
  click-handlers that complete a TCS, await TCS, hide panel.
- `RefreshAll()` re-renders every panel from `_state` after every step. The
  UI is a pure view; never mutate state from `Refresh*`.
- Engine log lines are mirrored into the UI log panel via `GameLog.OnLine`.
  `ShouldMirrorLine` filters out internal banners (`Dogma:`, `Effect`,
  handler progress, `[state]` lines).
- Click-to-act: legal actions populate `_legalMeldIds`, `_legalAchieveAges`,
  `_legalDogmaColors`, `_legalHandPickIds`. Hand and board tiles dispatch
  through these sets; absent a live prompt, clicks just preview the card.

---

## Logging

`GameLog` is static. Append-only. Call `Start(path?)` once at the top of a
game (default file: `%TEMP%/innovation-last-game.log`). Subscribe to
`OnLine` to mirror lines to a UI.

Conventions:

- `[state] <codec>` — turn-boundary snapshot. Emitted by `TurnManager` after
  the initial meld and after each `AdvanceTurn`. Mid-dogma codes don't
  round-trip — see `GameStateCodec`.
- `— P1 action (DrawAction, turn 3, 2 left)` — top-level action banner.
- `Dogma: P1 activates A5 Chemistry(Blue) (featured Leaf)` — dogma start.
- `Effect 1 (demand) — Leaf counts: P1=2, P2=0; targets: 1,0` — target
  computation per level.
- `  → handler ArcheryHandler on P2` / `    = progressed (paused awaiting choice)` — handler trace.
- `P3 draws a 2`, `P1 tucks A4 Invention(Green)` — handler-emitted effect lines.

Helpers:
- `GameLog.P(int)` → `"P1"` / `GameLog.P(PlayerState)` → `"P1"`.
- `GameLog.C(GameState, int)` → `"A5 Chemistry(Blue)"`.

`GameLog.Pause()` / `Resume()` suppress logs during AI rollouts.

---

## Testing

xUnit. Tests go in `Innovation.Tests`, one file per handler / per concern.

### Encoding fixture (required)

Every test class loading cards needs:

```csharp
static FooHandlerTests() {
    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
}
```

(Without this, `cards.tsv` decode throws because Windows-1252 isn't shipped
in .NET 5+.)

### `Fresh()` factory pattern

Tests typically build a near-empty state and stuff it with what they need:

```csharp
private static IReadOnlyList<Card> AllCards => CardDataLoader.LoadFromEmbeddedResource();
private static int IdOf(string title) => AllCards.Single(c => c.Title == title).Id;

private static GameState Fresh(int players = 2) {
    var g = new GameState(AllCards, players);
    foreach (var c in AllCards) g.Decks[c.Age].Add(c.Id);
    g.Phase = GamePhase.Dogma;
    return g;
}
```

### Direct handler tests

Construct a `DogmaContext`, call `handler.Execute(g, target, ctx)`, inspect
state. For paused handlers, simulate the UI by setting `ctx.PendingChoice`'s
chosen field and clearing `ctx.Paused` between calls. Pattern in
`ReformationHandlerTests.cs`.

### End-to-end runner tests

For multi-action scenarios, use a `ScriptedController` — a fake
`IPlayerController` with `Queue<...>` answer streams. Plug into a
`GameRunner` and call `Step()` until the turn passes. See
`ReformationHandlerTests.TwoActivations_SecondTucksTwoAndSplays_ViaRunner`
for a concrete example.

---

## Known gotchas

These are bugs we've actually hit. Read these before changing the relevant
code.

### `TurnManager.ResumeDogma` and `ExecuteDogma` — don't clobber GameOver

A handler can end the game mid-dogma (e.g. tucking the 6th card claims
Monument, which trips the achievement-count win and sets
`Phase = GameOver`). Both `ResumeDogma` and `ExecuteDogma` reset
`Phase = Action` after the dogma completes — this **must** be guarded by
`if (_g.IsGameOver) return;`. Otherwise the GameOver flag is silently
overwritten and the turn keeps going.

```csharp
if (!PendingDogma.IsComplete) return;
if (_g.IsGameOver) return;       // ← critical
_g.Phase = GamePhase.Action;
```

### Nested dogma frames must save/restore `HandlerState`

Cards that execute *another* card's effects (Robotics, Self Service,
Computers, Software, Satellites) push a `NestedDogmaFrame` onto
`ctx.NestedFrames`. The engine's `RunNestedFrames` swaps `ctx.HandlerState`
with the frame's slot for the duration of the nested call, then swaps back.
Don't bypass this — nested handlers and outer handlers can both have
in-flight `HandlerState` and they must not see each other's.

### Demand text reward goes to the *defender*

(Repeated from the handler section because it's worth reading twice.) In
`"I demand you transfer X! If you do, draw a Y!"`, the "if you do, draw"
clause rewards the demand target, not the activator. This bug has surfaced
in six handlers. New demand handlers default to passing `target` (not
`activator`) to `Mechanics.Draw*`.

### Mid-dogma state codes don't round-trip

`GameStateCodec` captures piles, hands, scores, achievements, turn/phase/
active-player. It does **not** capture `DogmaContext`, `PendingChoice`, or
`HandlerState`. The "Copy state" UI button warns if `Phase == Dogma` and
the load dialog refuses to deserialize a mid-dogma snapshot — both refer
the user to the `[state]` lines in the log file (which are emitted at turn
boundaries only).

### Continue gate must skip mid-dogma choice resolutions

The WPF game loop gates AI turns behind the Continue button. But if the AI
pauses mid-dogma and resumes its own choice, that's not a new top-level
action — `_runner.IsResolvingChoice` is true and the gate must be skipped.
Otherwise mid-dogma AI choices each demand a Continue click, which is noise.

### Initial meld should not run on a loaded state

A loaded state already has melded cards. `RunGameLoop` checks
`_state.Players.All(p => p.Stacks.All(s => s.IsEmpty))` before calling
`CompleteInitialMeld()`. Don't remove that guard.

---

## Comments style

Cite VB6 line numbers on rules-bearing types and methods. Format:
`(main.frm <line>)` or `Mirrors VB6 <function> (main.frm <line>)`.

Examples from the codebase:

```csharp
// Ordered to match VB6 color_lookup() indices (see main.frm line 7452).
public enum CardColor { Yellow, Red, Purple, Blue, Green }
```

```csharp
/// Mirrors VB6 main.frm 4269–4282.
public sealed class AgricultureHandler : IDogmaHandler { ... }
```

```csharp
// Reset Monument trackers for every player ... Matches VB6 play_game lines 3342–3346.
foreach (var pl in _g.Players) {
    pl.ScoredThisTurn = 0;
    pl.TuckedThisTurn = 0;
}
```

When fixing a bug whose cause is non-obvious, leave a comment explaining
*why* the fix is shaped the way it is. Future Claude sessions and future you
will both thank you.

---

## Workflow for fixing a rules bug

1. **Reproduce in the running app** if possible. Note the `[state]` codec
   line for the turn the bug appeared on.
2. **Find the VB6 reference**. Search `main.frm` for the relevant card name
   or mechanic. Read the surrounding code.
3. **Write a failing unit test** in `Innovation.Tests`. Use `Fresh()` +
   direct handler call, or `ScriptedController` for multi-action flows.
4. **Fix the handler / mechanic**. Add a comment citing the VB6 line.
5. **Verify the test passes** and run the full test suite.
6. **If the bug touched a pattern that recurs across cards** (like the
   demand-target/activator confusion), audit other handlers for the same
   shape with `Grep` before declaring done.

---

## What NOT to do

- Don't add `DateTime.Now` or unseeded `Random` to engine code. It breaks
  determinism and replays.
- Don't reorder `CardColor` or `Icon` enums. VB6 indices depend on them.
- Don't mutate piles directly. Use `Mechanics.*`. Direct mutation skips
  Monument tracking, special-achievement checks, and log lines.
- Don't share types between Core and Wpf beyond DTOs. Don't import WPF
  namespaces into Core.
- Don't add a "framework" generalizing across multiple games. The right
  time to extract shared abstractions is *after* a second game exists, not
  speculatively.
- Don't reset `Phase = Action` after a dogma without checking `IsGameOver`
  first.
- Don't consume `ctx.PendingChoice` without nulling it — the next iteration
  will see stale data.
- Don't write documentation files (`*.md`, `README*`) unless explicitly
  asked.
