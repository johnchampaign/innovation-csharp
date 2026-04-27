using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Innovation.Core;
using Innovation.Core.Players;
using CoreCard = Innovation.Core.Card;
// Window has an Icon property of its own (the title-bar image),
// which shadows Innovation.Core.Icon in member-resolution contexts
// like static field initializers. Alias the enum to sidestep that.
using CoreIcon = Innovation.Core.Icon;

namespace Innovation.Wpf;

/// <summary>
/// Phase 7.4a: the VB6-style shell goes live. <see cref="GameSetup.Create"/>
/// builds a real starting state, a <see cref="GameRunner"/> pumps it on a
/// background <see cref="Task"/>, and the Continue button releases one
/// <see cref="GameRunner.Step"/> per click. Both seats are driven by AI
/// controllers for now (seat 0 random, seat 1 greedy) — inline human
/// prompts for seat 0 come in Phase 7.4b, which will swap in a
/// HumanController that blocks on UI input.
///
/// <para>All panels are populated procedurally from the live
/// <see cref="GameState"/>; <see cref="RefreshAll"/> is called on the UI
/// thread after every engine step to re-render every panel from the
/// current state. The worker reports back via
/// <see cref="GameRunner.OnStepCompleted"/>, which marshals onto the
/// Dispatcher before touching UI.</para>
/// </summary>
public partial class MainWindow : Window, IUserPromptSink
{
    // Icon ordering for the sidebar totals table matches the VB6
    // reference header row (and Innovation's rulebook): Leaf, Castle,
    // Lightbulb, Crown, Factory, Clock.
    private static readonly CoreIcon[] IconOrder =
    {
        CoreIcon.Leaf, CoreIcon.Castle, CoreIcon.Lightbulb,
        CoreIcon.Crown, CoreIcon.Factory, CoreIcon.Clock,
    };

    // "Your Board" / "Opponent Board" pile column order is
    // Yellow, Red, Purple, Blue, Green — the VB6 board layout. An
    // empty color just leaves its column blank (per user spec).
    private static readonly CardColor[] ColorOrder =
    {
        CardColor.Yellow, CardColor.Red, CardColor.Purple,
        CardColor.Blue,   CardColor.Green,
    };

    private readonly IReadOnlyList<CoreCard> _cards;
    private IReadOnlyList<CoreCard> Cards => _cards;

    // Live engine state. _state/_detailCard are mutated on the worker
    // thread and re-read on the UI thread only after the worker has
    // handed control back (via Dispatcher.Invoke inside OnStepCompleted,
    // or while parked on the Continue-button TCS).
    private readonly GameState _state;
    private CoreCard _detailCard;
    private readonly ObservableCollection<string> _log = new();
    private readonly GameRunner _runner;
    // Parallel copy of the same registrations TurnManager builds. Used by
    // the UI to look up dogma metadata (featured icon, demand flag) for
    // tinting legal-dogma top cards — engine state itself is unaffected.
    private readonly CardRegistry _registry;

    // Release-one-step gate. Continue button sets the TCS; the worker
    // loop awaits it, runs a single Step(), then installs a fresh TCS.
    private TaskCompletionSource<bool>? _continueTcs;
    private readonly CancellationTokenSource _shutdown = new();
    private Task? _gameTask;

    // Seat kinds, indexed by player number. "Your Board" / "Your Hand"
    // always render from seat 0; if seat 0 is AI-driven, those panels
    // just show the AI's state read-only. Continue-gating and the
    // human-prompt sink branch on IsHumanSeat(index).
    private SeatKind[] _seatKinds = Array.Empty<SeatKind>();
    private const int HumanIndex = 0;
    private bool IsHumanSeat(int i) => i >= 0 && i < _seatKinds.Length && _seatKinds[i] == SeatKind.Human;

    public MainWindow()
    {
        InitializeComponent();

        _cards = CardDataLoader.LoadFromEmbeddedResource();

        NewGameOptions opts;
        string? loadCode = PendingLoadCode;
        var loadSeats = PendingSeats;
        PendingLoadCode = null;
        PendingSeats = null;

        if (loadCode is not null && loadSeats is not null)
        {
            opts = new NewGameOptions { Seed = 0, Seats = loadSeats };
        }
        else
        {
            // Ask up front for seed + seat kinds. Cancel shuts down the app —
            // there's nothing to show without a game to drive.
            var dlg = new NewGameDialog { Owner = null };
            if (dlg.ShowDialog() != true || dlg.Result is not { } picked)
            {
                Application.Current.Shutdown();
                throw new OperationCanceledException("New Game cancelled.");
            }
            opts = picked;
        }

        GameLog.Start();
        GameLog.Log(loadCode is null
            ? $"New game: seed={opts.Seed}, seats={string.Join(",", opts.Seats)}"
            : $"Loaded state, seats={string.Join(",", opts.Seats)}");
        _log.Add($"[log] {GameLog.CurrentPath}");

        var rng = new Random(opts.Seed);
        _state = loadCode is not null
            ? GameStateCodec.Decode(loadCode, _cards)
            : GameSetup.Create(_cards, numPlayers: opts.Seats.Length, rng);
        _detailCard = _cards[0];
        _registry = new CardRegistry(_cards);
        CardRegistrations.RegisterAll(_registry, _cards);

        // Controller seeds are derived from the chosen seed so one seed
        // fully determines the run.
        _seatKinds = opts.Seats;
        var seatRng = new Random(opts.Seed);
        var controllers = new IPlayerController[opts.Seats.Length];
        for (int i = 0; i < opts.Seats.Length; i++)
        {
            controllers[i] = opts.Seats[i] switch
            {
                SeatKind.Human => new HumanController(this),
                SeatKind.Greedy => new GreedyController(seatRng.Next()),
                SeatKind.Random => new RandomController(seatRng.Next()),
                _ => throw new InvalidOperationException($"Unknown seat kind {opts.Seats[i]}."),
            };
        }
        _runner = new GameRunner(_state, controllers)
        {
            OnStepCompleted = OnEngineStepped,
            OnChoiceResolved = OnEngineChoiceResolved,
        };

        LogItems.ItemsSource = _log;
        PopulateViewCardCombo();
        WirePromptHandlers();

        ContinueButton.Click += (_, _) => _continueTcs?.TrySetResult(true);
        NewGameButton.Click += (_, _) => StartNewGame();
        Closed += OnWindowClosed;

        GameLog.OnLine += OnEngineLogLine;

        RefreshAll();
        _gameTask = Task.Run(() => RunGameLoop(_shutdown.Token));
    }

    // ---------- Game loop ----------

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        GameLog.OnLine -= OnEngineLogLine;
        _shutdown.Cancel();
        _continueTcs?.TrySetCanceled();
    }

    // Engine-emitted log lines worth surfacing in the UI panel. The file
    // log has everything; the UI panel only wants the stuff a player
    // actually cares about (draws, returns, transfers, melds-by-effect,
    // scores). We filter by prefix to keep setup noise out.
    private void OnEngineLogLine(string line)
    {
        if (!ShouldMirrorLine(line)) return;
        var redacted = RedactOpponentHandCards(line);
        try
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _log.Add("  · " + redacted);
                LogScroller.ScrollToEnd();
            }));
        }
        catch { /* window closing */ }
    }

    // Card-label format produced by GameLog.C: "A5 Title(Color)". Strip
    // the title+color portion, keeping just the age, when the line
    // describes an opponent's hand event (a draw, return, or transfer
    // into/out of an opponent's hand). The file log keeps full info so
    // the player can review post-game; only the in-window mirror redacts.
    private static readonly System.Text.RegularExpressions.Regex CardLabelRx =
        new(@"A(\d+)\s+[^(]+\(\w+\)",
            System.Text.RegularExpressions.RegexOptions.Compiled);

    private static string RedactOpponentHandCards(string line)
    {
        // Opponent (non-P1) drawing or returning, or score→hand. Lines
        // start with the player tag.
        if (System.Text.RegularExpressions.Regex.IsMatch(line,
                @"^P[2-9]\b.*\b(draws|returns|moves)\b"))
        {
            return CardLabelRx.Replace(line, "A$1");
        }

        // hand→hand transfer where the destination is an opponent.
        if (line.StartsWith("transfer hand→hand "))
        {
            // Pattern: "... → P{n}". Redact when n != 1.
            var m = System.Text.RegularExpressions.Regex.Match(line, @"→\s*P(\d+)\b");
            if (m.Success && m.Groups[1].Value != "1")
                return CardLabelRx.Replace(line, "A$1");
        }

        return line;
    }

    private static bool ShouldMirrorLine(string line)
    {
        // GameLog.P returns "P1"/"P2"… so lines describing a player's
        // engine-side event start with that prefix. Exclude the "— P1
        // action" top-level action banner (already covered by OnStepCompleted)
        // and the dogma-activation banner (same).
        if (line.StartsWith("— ")) return false;
        if (line.StartsWith("Dogma:")) return false;
        if (line.StartsWith("Effect ")) return false;
        if (line.StartsWith("  → handler") || line.StartsWith("    =")) return false;
        if (line.StartsWith("  ↪ nested")) return false;
        if (line.StartsWith("[state]")) return false;
        if (line.StartsWith("New game:")) return false;
        if (line.StartsWith("#")) return false;
        return true;
    }

    private async Task RunGameLoop(CancellationToken ct)
    {
        try
        {
            // CompleteInitialMeld asks every controller for its opening
            // pick synchronously. The human seat's pick blocks inside
            // PromptInitialMeld on the inline panel — no extra Continue
            // needed.
            // A loaded state is already past the opening meld (every seat
            // has a populated board). Skip the meld pass — otherwise we'd
            // try to meld from empty starting hands.
            if (_state.Players.All(p => p.Stacks.All(s => s.IsEmpty)))
                _runner.CompleteInitialMeld();
            while (!_state.IsGameOver)
            {
                ct.ThrowIfCancellationRequested();
                // Continue gate is for AI transparency only. When the
                // next actor is the human, the inline prompt panel
                // already blocks the step — requiring an extra Continue
                // click would just be noise.
                // Only gate Continue on top-level actions. Mid-dogma
                // choice resolutions are part of the action that opened
                // them — an AI's inline pick shouldn't need a click, and
                // the engine should drive the whole dogma chain before
                // handing control to the next top-level decider.
                if (!_runner.IsResolvingChoice && !IsHumanSeat(_runner.NextActor))
                {
                    var tcs = new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    _continueTcs = tcs;
                    Dispatcher.Invoke(SetContinuePrompt);
                    await tcs.Task.ConfigureAwait(false);
                    ct.ThrowIfCancellationRequested();
                }
                _runner.Step();
            }
            Dispatcher.Invoke(ShowGameOver);
        }
        catch (OperationCanceledException) { /* window closing */ }
    }

    private void OnEngineStepped(int actor, PlayerAction? action)
    {
        _shutdown.Token.ThrowIfCancellationRequested();
        // Choice-resolution fires are described by OnEngineChoiceResolved —
        // skip the generic fallback line to avoid a duplicate entry, but
        // still refresh so the post-resolution state (melds, draws, scores)
        // shows up in the UI.
        bool isChoiceResolution = actor != -1 && action is null;
        string? line = isChoiceResolution
            ? null
            : actor == -1
                ? "(setup) Initial melds applied."
                : $"P{actor + 1}: {DescribeAction(action!, actor)}";
        Dispatcher.Invoke(() =>
        {
            if (line is not null) _log.Add(line);
            RefreshAll();
            // Defer the scroll until after the ItemsControl realizes the
            // new row, otherwise ScrollToEnd sees the pre-add extent.
            Dispatcher.BeginInvoke(new Action(() => LogScroller.ScrollToEnd()),
                System.Windows.Threading.DispatcherPriority.Background);
        });
    }

    private string DescribeAction(PlayerAction action, int actor) => action switch
    {
        DrawAction => "Draw",
        MeldAction m => $"Meld {_cards[m.CardId].Title}",
        AchieveAction a => $"Achieve Age {a.Age}",
        DogmaAction d => DescribeDogma(d, actor),
        _ => action.ToString() ?? "?",
    };

    private string DescribeDogma(DogmaAction d, int actor)
    {
        // The activator's top card of the chosen color is the one whose
        // dogma just fired. If its id isn't in the registry we're still
        // running on PlaceholderHandler — flag it in the log so the user
        // understands why nothing visible happened.
        var stack = _state.Players[actor].Stack(d.Color);
        if (stack.IsEmpty) return $"Dogma ({d.Color})";
        int topId = stack.Top;
        string title = _cards[topId].Title;
        return _registry.IsRegistered(topId)
            ? $"Dogma ({d.Color}) — {title}"
            : $"Dogma ({d.Color}) — {title} (not yet implemented)";
    }

    private void OnEngineChoiceResolved(int actor, ChoiceRequest req)
    {
        _shutdown.Token.ThrowIfCancellationRequested();
        string desc = DescribeChoice(req);
        string line = $"P{actor + 1}: chose {desc}";
        Dispatcher.Invoke(() =>
        {
            _log.Add(line);
            RefreshAll();
            Dispatcher.BeginInvoke(new Action(() => LogScroller.ScrollToEnd()),
                System.Windows.Threading.DispatcherPriority.Background);
        });
    }

    private string DescribeChoice(ChoiceRequest req) => req switch
    {
        SelectHandCardRequest s => s.ChosenCardId is int id
            ? $"card {_cards[id].Title} (age {_cards[id].Age})"
            : "none",
        SelectHandCardSubsetRequest ss => ss.ChosenCardIds.Count == 0
            ? "no cards"
            : $"{ss.ChosenCardIds.Count} card(s): " +
              string.Join(", ", ss.ChosenCardIds.Select(id => _cards[id].Title)),
        SelectScoreCardSubsetRequest sss => sss.ChosenCardIds.Count == 0
            ? "no cards"
            : $"{sss.ChosenCardIds.Count} score card(s): " +
              string.Join(", ", sss.ChosenCardIds.Select(id => _cards[id].Title)),
        SelectScoreCardRequest scs => scs.ChosenCardId is int id
            ? $"score card {_cards[id].Title}"
            : "none",
        YesNoChoiceRequest yn => yn.ChosenYes ? "Yes" : "No",
        SelectColorRequest sc => sc.ChosenColor is CardColor c ? c.ToString() : "none",
        SelectValueRequest sv => sv.ChosenValue is int v ? $"value {v}" : "none",
        SelectStackOrderRequest sso => "stack order",
        _ => "?",
    };

    private void SetContinuePrompt()
    {
        // AI turn — Continue button drives the next step. Hide any
        // lingering prompt panels so the UI looks consistent.
        HideAllPromptPanels();
        ContinueButton.Visibility = Visibility.Visible;
        ContinueButton.IsEnabled = true;
        PromptText.Text = _state.ActionsRemaining > 0
            ? $"Player {_state.ActivePlayer + 1}'s turn — {_state.ActionsRemaining} action(s) left. Click Continue."
            : $"Player {_state.ActivePlayer + 1} is thinking. Click Continue to step.";
    }

    private void ShowGameOver()
    {
        RefreshAll();
        // The human's action/choice panel may still be open if the game
        // ended on their own Draw (age-11 cascade) — collapse everything
        // so the final state is unambiguous.
        HideAllPromptPanels();
        var winners = _state.Winners.Count == 0
            ? "(none)"
            : string.Join(", ", _state.Winners.Select(i => $"Player {i + 1}"));
        PromptText.Text = $"Game over. Winners: {winners}.";
        ContinueButton.Visibility = Visibility.Collapsed;
        ContinueButton.IsEnabled = false;
        NewGameButton.Visibility = Visibility.Visible;
    }

    private void StartNewGame()
    {
        // Open a fresh MainWindow (which prompts for seed/seats), then close
        // this one. Mirrors the LoadStateButton flow but with no pending code,
        // so the new window's ctor takes the NewGameDialog path.
        PendingLoadCode = null;
        PendingSeats = null;
        var w = new MainWindow();
        w.Show();
        Close();
    }

    private void RefreshAll()
    {
        PopulateDetailCard();
        PopulateCurrentPlayerBanner();
        PopulateIconTotals();
        PopulateAchievements();
        PopulateCardsRemaining();
        PopulateYourBoard();
        PopulateYourHand();
        PopulateYourScore();
        PopulateOpponents();
    }

    // ---------- Top strip ----------

    private void PopulateDetailCard()
    {
        DetailCard.Card = _detailCard;
    }

    private void PopulateCurrentPlayerBanner()
    {
        // VB6 is 1-indexed in UI copy. PromptText is set separately by
        // SetWaitingPrompt / ShowGameOver; leave it alone here so the
        // per-refresh redraw doesn't clobber those transient messages.
        CurrentPlayerText.Text = $"Current Player: {_state.ActivePlayer + 1}";
    }

    // ---------- Sidebar ----------

    private void PopulateViewCardCombo()
    {
        // Alphabetical list of every card — player can preview any
        // card in the detail panel, live or not. Confirmed with the
        // user as "all 105 cards alphabetically."
        var titles = Cards.Select(c => c.Title).OrderBy(t => t).ToList();
        ViewCardCombo.ItemsSource = titles;
        ViewCardCombo.SelectedItem = _detailCard.Title;
        ViewCardCombo.SelectionChanged += (_, _) =>
        {
            if (ViewCardCombo.SelectedItem is string title)
            {
                var card = Cards.FirstOrDefault(c => c.Title == title);
                if (card is not null)
                {
                    _detailCard = card;
                    DetailCard.Card = card;
                }
            }
        };
    }

    private void PopulateIconTotals()
    {
        // (header + N player) rows × 9 columns
        // (label, 6 icons, A, S). Each cell is a TextBlock or Image.
        var grid = IconTotalsGrid;
        grid.Children.Clear();
        grid.RowDefinitions.Clear();
        grid.ColumnDefinitions.Clear();

        int totalRows = 1 + _state.Players.Length;
        for (int r = 0; r < totalRows; r++)
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Column 0 for the "Player N" label, then 6 icon columns,
        // then A and S.
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
        for (int i = 0; i < IconOrder.Length; i++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Header row: icon images + "A" / "S".
        for (int i = 0; i < IconOrder.Length; i++)
        {
            var iconCell = new Border
            {
                Child = CardVisuals.BuildIconTile(IconOrder[i], size: 22),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 2),
            };
            Grid.SetRow(iconCell, 0);
            Grid.SetColumn(iconCell, 1 + i);
            grid.Children.Add(iconCell);
        }
        grid.Children.Add(HeaderLetter("A", 1 + IconOrder.Length));
        grid.Children.Add(HeaderLetter("S", 2 + IconOrder.Length));

        // Per-player count rows.
        var state = _state;
        // Precompute max/min per icon across all seats so we can color
        // each cell: green for the max (assuming non-tie), red for the
        // min (assuming non-tie). Confirmed rule from the user.
        var maxByIcon = new Dictionary<CoreIcon, int>();
        var minByIcon = new Dictionary<CoreIcon, int>();
        foreach (var icon in IconOrder)
        {
            var counts = state.Players.Select(p => IconCounter.Count(p, icon, Cards)).ToArray();
            maxByIcon[icon] = counts.Max();
            minByIcon[icon] = counts.Min();
        }

        for (int pi = 0; pi < state.Players.Length; pi++)
        {
            var player = state.Players[pi];
            int row = 1 + pi;

            var label = new TextBlock
            {
                Text = $"Player {pi + 1}",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                Foreground = CardVisuals.DarkText,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetRow(label, row);
            Grid.SetColumn(label, 0);
            grid.Children.Add(label);

            for (int i = 0; i < IconOrder.Length; i++)
            {
                var icon = IconOrder[i];
                int count = IconCounter.Count(player, icon, Cards);
                var countBlock = new TextBlock
                {
                    Text = count.ToString(),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontWeight = FontWeights.Bold,
                    FontSize = 13,
                    Foreground = ColorForIconCount(count, maxByIcon[icon], minByIcon[icon]),
                    HorizontalAlignment = HorizontalAlignment.Center,
                };
                Grid.SetRow(countBlock, row);
                Grid.SetColumn(countBlock, 1 + i);
                grid.Children.Add(countBlock);
            }

            // A = achievements, S = score points. Plain dark — no
            // green/red since they're totals, not icon contests.
            var achCell = PlainCountCell(player.AchievementCount);
            Grid.SetRow(achCell, row);
            Grid.SetColumn(achCell, 1 + IconOrder.Length);
            grid.Children.Add(achCell);

            var scoreCell = PlainCountCell(player.Score(Cards));
            Grid.SetRow(scoreCell, row);
            Grid.SetColumn(scoreCell, 2 + IconOrder.Length);
            grid.Children.Add(scoreCell);
        }
    }

    private static TextBlock HeaderLetter(string text, int column)
    {
        var t = new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily("Segoe UI"),
            FontWeight = FontWeights.Bold,
            FontSize = 14,
            Foreground = CardVisuals.DarkText,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 2),
        };
        Grid.SetRow(t, 0);
        Grid.SetColumn(t, column);
        return t;
    }

    private static TextBlock PlainCountCell(int n) => new()
    {
        Text = n.ToString(),
        FontFamily = new FontFamily("Segoe UI"),
        FontWeight = FontWeights.Bold,
        FontSize = 13,
        Foreground = CardVisuals.DarkText,
        HorizontalAlignment = HorizontalAlignment.Center,
    };

    private static Brush ColorForIconCount(int count, int max, int min)
    {
        // Green on the icon-race leader, red on the laggard; dark
        // neutral when everyone's tied so we don't flash meaningless
        // color. This matches the VB6 reference UI per user spec.
        if (max != min)
        {
            if (count == max) return new SolidColorBrush(Color.FromRgb(0x1E, 0x88, 0x3E)); // green
            if (count == min) return new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28)); // red
        }
        return CardVisuals.DarkText;
    }

    private void PopulateAchievements()
    {
        AgeAchievementsList.Children.Clear();
        foreach (var age in _state.AvailableAgeAchievements.OrderBy(a => a))
        {
            var tile = new TextBlock
            {
                Text = age.ToString(),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = CardVisuals.DarkText,
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
            };
            int captured = age;
            tile.MouseLeftButtonUp += (_, _) => OnAgeAchievementClicked(captured);
            AgeAchievementsList.Children.Add(tile);
        }

        void Apply(TextBlock t, string name)
        {
            var claimed = !_state.AvailableSpecialAchievements.Contains(name);
            t.Foreground = claimed
                ? new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA))
                : CardVisuals.DarkText;
        }
        Apply(SaMonument, "Monument");
        Apply(SaEmpire,   "Empire");
        Apply(SaWonder,   "Wonder");
        Apply(SaWorld,    "World");
        Apply(SaUniverse, "Universe");
    }

    private void PopulateCardsRemaining()
    {
        var grid = CardsRemainingGrid;
        grid.Children.Clear();

        // Ages 1–5 on top row, 6–10 on bottom. Matches the VB6
        // screenshot's two-row "1) 3   2) 9 ..." layout.
        for (int age = 1; age <= 10; age++)
        {
            int row = (age <= 5) ? 0 : 1;
            int col = (age - 1) % 5;
            var label = new TextBlock
            {
                Text = $"{age}) {_state.Decks[age].Count}",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                Foreground = CardVisuals.DarkText,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 2, 0, 2),
            };
            Grid.SetRow(label, row);
            Grid.SetColumn(label, col);
            grid.Children.Add(label);
        }
    }

    // ---------- Your board / hand / score ----------

    private void PopulateYourBoard()
    {
        var me = _state.Players[HumanIndex];
        var grid = YourBoardGrid;
        grid.Children.Clear();

        int highestTop = 0;

        for (int i = 0; i < ColorOrder.Length; i++)
        {
            var color = ColorOrder[i];
            var stack = me.Stack(color);

            // Empty color: leave the column blank per user spec.
            if (stack.IsEmpty) continue;

            var topCard = Cards[stack.Top];
            if (topCard.Age > highestTop) highestTop = topCard.Age;

            var cell = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(4, 0, 4, 0),
            };
            var topTile = new CardTileView
            {
                Card = topCard,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Cursor = System.Windows.Input.Cursors.Hand,
            };
            var capturedColor = color;
            var capturedTop = topCard;
            topTile.MouseLeftButtonUp += (_, _) => OnBoardCardClicked(capturedColor, capturedTop);
            topTile.MouseEnter += (_, _) => PreviewCard(capturedTop);

            // Dogma-legality tint: only when an action prompt is live and
            // this color is a legal Dogma target. Red = will trigger a
            // demand on ≥1 opponent; Gold = will be shared with ≥1
            // opponent; Green = solo (opponent has too few icons to share,
            // and no demand fires).
            var tintedTop = new Border
            {
                Child = topTile,
                BorderThickness = new Thickness(3),
                BorderBrush = BorderForDogmaLegality(color, capturedTop),
                Padding = new Thickness(1),
            };
            cell.Children.Add(tintedTop);

            // Size + Splay on one line; splay label only when splayed.
            var sizeLine = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(6, 4, 0, 0),
            };
            var sizeLabel = new TextBlock
            {
                Text = $"Size: {stack.Count}",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = CardVisuals.DarkText,
                Cursor = System.Windows.Input.Cursors.Hand,
                TextDecorations = TextDecorations.Underline,
            };
            var capturedStackColor = color;
            sizeLabel.MouseLeftButtonUp += (_, _) => ShowStackDialog(HumanIndex, capturedStackColor);
            sizeLine.Children.Add(sizeLabel);
            if (stack.Splay != Splay.None)
            {
                sizeLine.Children.Add(new TextBlock
                {
                    Text = $"   Splay: {stack.Splay}",
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    Foreground = CardVisuals.DarkText,
                });
            }
            cell.Children.Add(sizeLine);

            var viewBtn = new Button
            {
                Content = "View Stack",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11,
                Padding = new Thickness(6, 1, 6, 1),
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 4, 4, 0),
            };
            viewBtn.Click += (_, _) => ShowStackDialog(HumanIndex, capturedStackColor);
            cell.Children.Add(viewBtn);

            Grid.SetColumn(cell, i);
            grid.Children.Add(cell);
        }

        HighestTopText.Text = highestTop.ToString();
    }

    private void PopulateYourHand()
    {
        var me = _state.Players[HumanIndex];
        YourHandPanel.Children.Clear();
        foreach (var id in me.Hand)
        {
            var tile = new CardSummaryView
            {
                Card = Cards[id],
                Width = 220,
                HorizontalAlignment = HorizontalAlignment.Left,
                Cursor = System.Windows.Input.Cursors.Hand,
            };
            int captured = id;
            tile.MouseLeftButtonUp += (_, _) => OnHandCardClicked(captured);
            tile.MouseEnter += (_, _) => PreviewCard(_cards[captured]);

            // Wrap in a Border so we can show a subset-pick highlight
            // without having to mutate CardSummaryView's own chrome.
            var frame = new Border
            {
                Child = tile,
                Margin = new Thickness(0, 0, 0, 2),
                Padding = new Thickness(2),
                BorderThickness = new Thickness(3),
                BorderBrush = _subsetPicks.Contains(id)
                    ? new SolidColorBrush(Color.FromRgb(0xE6, 0xB8, 0x1C))   // gold = picked
                    : Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            YourHandPanel.Children.Add(frame);
        }
    }

    private void PopulateYourScore()
    {
        var me = _state.Players[HumanIndex];
        YourScoreHeader.Text = $"Your Score Pile ({me.Score(Cards)} points)";

        YourScorePanel.Children.Clear();
        foreach (var id in me.ScorePile)
        {
            var scoreTile = new CardSummaryView
            {
                Card = Cards[id],
                Width = 220,
                HorizontalAlignment = HorizontalAlignment.Left,
                Cursor = _scoreSubsetEligibleIds is not null
                    ? System.Windows.Input.Cursors.Hand
                    : System.Windows.Input.Cursors.Arrow,
            };
            int capturedId = id;
            scoreTile.MouseEnter += (_, _) => PreviewCard(_cards[capturedId]);
            scoreTile.MouseLeftButtonUp += (_, _) => OnScoreCardClicked(capturedId);

            // Same gold-border pick highlight as hand-card subset prompts.
            var frame = new Border
            {
                Child = scoreTile,
                Margin = new Thickness(0, 0, 0, 2),
                Padding = new Thickness(2),
                BorderThickness = new Thickness(3),
                BorderBrush = _subsetPicks.Contains(id)
                    ? new SolidColorBrush(Color.FromRgb(0xE6, 0xB8, 0x1C))   // gold = picked
                    : Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            YourScorePanel.Children.Add(frame);
        }
    }

    private void OnScoreCardClicked(int cardId)
    {
        _detailCard = _cards[cardId];
        DetailCard.Card = _detailCard;
        ViewCardCombo.SelectedItem = _detailCard.Title;

        if (_scoreSubsetEligibleIds is { } subs && subs.Contains(cardId))
        {
            if (!_subsetPicks.Add(cardId)) _subsetPicks.Remove(cardId);
            PopulateYourScore();
            UpdateSubsetOkState();
        }
    }

    // ---------- Opponent(s) ----------

    private void PopulateOpponents()
    {
        OpponentsPanel.Children.Clear();
        for (int pi = 0; pi < _state.Players.Length; pi++)
        {
            if (pi == HumanIndex) continue;
            OpponentsPanel.Children.Add(BuildOpponentRow(_state.Players[pi]));
        }
    }

    private FrameworkElement BuildOpponentRow(PlayerState opponent)
    {
        // One row: "Player N" label + 5 color-slot summaries (Yellow,
        // Red, Purple, Blue, Green) + Hand count + Score Pile count.
        var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });

        var label = new TextBlock
        {
            Text = $"Player {opponent.Index + 1}",
            FontFamily = new FontFamily("Segoe UI"),
            FontWeight = FontWeights.Bold,
            FontSize = 13,
            Foreground = CardVisuals.DarkText,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 4, 0, 0),
        };
        Grid.SetColumn(label, 0);
        row.Children.Add(label);

        // Per-color strip: colored summary + "Size: N" below.
        var pilesGrid = new Grid();
        for (int i = 0; i < ColorOrder.Length; i++)
            pilesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        for (int i = 0; i < ColorOrder.Length; i++)
        {
            var stack = opponent.Stack(ColorOrder[i]);
            if (stack.IsEmpty) continue;

            var cell = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(3, 0, 3, 0),
            };
            var oppTop = Cards[stack.Top];
            var oppTile = new CardSummaryView
            {
                Card = oppTop,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            oppTile.MouseEnter += (_, _) => PreviewCard(oppTop);
            cell.Children.Add(oppTile);
            var oppSizeLabel = new TextBlock
            {
                Text = $"Size: {stack.Count}" +
                       (stack.Splay != Splay.None ? $"   Splay: {stack.Splay}" : ""),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = CardVisuals.DarkText,
                Margin = new Thickness(4, 2, 0, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                TextDecorations = TextDecorations.Underline,
            };
            var oppCapturedColor = ColorOrder[i];
            int oppIndex = opponent.Index;
            oppSizeLabel.MouseLeftButtonUp += (_, _) => ShowStackDialog(oppIndex, oppCapturedColor);
            cell.Children.Add(oppSizeLabel);
            Grid.SetColumn(cell, i);
            pilesGrid.Children.Add(cell);
        }
        Grid.SetColumn(pilesGrid, 1);
        row.Children.Add(pilesGrid);

        var handPanel = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 4, 0, 0),
        };
        var handAges = opponent.Hand
            .Select(id => Cards[id].Age)
            .OrderBy(a => a);
        foreach (var age in handAges)
        {
            handPanel.Children.Add(new TextBlock
            {
                Text = age.ToString(),
                FontFamily = new FontFamily("Segoe UI"),
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Foreground = CardVisuals.DarkText,
                Margin = new Thickness(2, 0, 2, 0),
            });
        }
        Grid.SetColumn(handPanel, 2);
        row.Children.Add(handPanel);

        var scorePanel = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 4, 0, 0),
        };
        var ages = opponent.ScorePile
            .Select(id => Cards[id].Age)
            .OrderBy(a => a);
        foreach (var age in ages)
        {
            scorePanel.Children.Add(new TextBlock
            {
                Text = age.ToString(),
                FontFamily = new FontFamily("Segoe UI"),
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Foreground = CardVisuals.DarkText,
                Margin = new Thickness(2, 0, 2, 0),
            });
        }
        Grid.SetColumn(scorePanel, 3);
        row.Children.Add(scorePanel);

        return row;
    }

    // ---------- Inline prompt sink (IUserPromptSink) ----------
    //
    // Every prompt method runs on the engine's worker thread. It
    // allocates a TaskCompletionSource, marshals onto the UI thread to
    // configure the relevant panel and install button handlers that
    // complete the TCS, then blocks synchronously on TCS.Task. When the
    // user clicks, the result is read back, the panel is hidden, and the
    // worker continues. The cancellation token shorts the wait if the
    // window is closing.

    private TaskCompletionSource<int?>? _handCardTcs;
    private TaskCompletionSource<IReadOnlyList<int>>? _subsetTcs;
    private TaskCompletionSource<bool>? _yesNoTcs;
    private TaskCompletionSource<CardColor?>? _colorTcs;
    private TaskCompletionSource<int?>? _scoreCardTcs;
    private TaskCompletionSource<PlayerAction>? _actionTcs;

    // Click-to-act dispatch sets. Populated while the action prompt is
    // live; consulted by the tile click handlers in the three affordance
    // zones (hand / achievements / your board). When all three are null,
    // clicks just preview the card in DetailCard.
    // - _legalMeldIds: click a hand card → MeldAction.
    // - _legalAchieveAges: click an age tile → AchieveAction.
    // - _legalDogmaColors: click your-board top card → DogmaAction.
    // - _legalHandPickIds: a single-card hand pick is live → that id
    //   satisfies the pending SelectHandCardRequest.
    private HashSet<int>? _legalMeldIds;
    private HashSet<int>? _legalAchieveAges;
    private HashSet<CardColor>? _legalDogmaColors;
    private HashSet<int>? _legalHandPickIds;

    // Subset state (Currency, Masonry, Pottery, …). Non-null _subsetEligibleIds
    // means "subset prompt is live"; clicks on hand tiles toggle
    // membership in _subsetPicks, and OK resolves when the count is in
    // [min, max].
    private HashSet<int>? _subsetEligibleIds;
    // Score-pile variant of the same subset prompt (Combustion, Databases).
    // Only one of _subsetEligibleIds / _scoreSubsetEligibleIds is non-null
    // at a time; clicks in the corresponding panel toggle into _subsetPicks.
    private HashSet<int>? _scoreSubsetEligibleIds;
    private readonly HashSet<int> _subsetPicks = new();
    private int _subsetMin, _subsetMax;

    private void WirePromptHandlers()
    {
        DrawButton.Click += (_, _) => _actionTcs?.TrySetResult(new DrawAction());
        HandCardNoneButton.Click += (_, _) => _handCardTcs?.TrySetResult(null);
        SubsetOkButton.Click += (_, _) =>
        {
            if (_subsetPicks.Count >= _subsetMin && _subsetPicks.Count <= _subsetMax)
                _subsetTcs?.TrySetResult(_subsetPicks.ToList());
        };
        YesButton.Click += (_, _) => _yesNoTcs?.TrySetResult(true);
        NoButton.Click += (_, _) => _yesNoTcs?.TrySetResult(false);
    }

    // Central hand-tile click: dispatched by PopulateYourHand.
    private void OnHandCardClicked(int cardId)
    {
        // Always keep the detail pane in sync — even when a click also
        // satisfies a prompt, the user may want to see what they picked.
        _detailCard = _cards[cardId];
        DetailCard.Card = _detailCard;
        ViewCardCombo.SelectedItem = _detailCard.Title;

        if (_legalMeldIds is { } melds && melds.Contains(cardId))
        {
            _actionTcs?.TrySetResult(new MeldAction(cardId));
            return;
        }
        if (_legalHandPickIds is { } picks && picks.Contains(cardId))
        {
            _handCardTcs?.TrySetResult(cardId);
            return;
        }
        if (_subsetEligibleIds is { } subs && subs.Contains(cardId))
        {
            if (!_subsetPicks.Add(cardId)) _subsetPicks.Remove(cardId);
            PopulateYourHand();           // redraw to flip the selection border
            UpdateSubsetOkState();
            return;
        }
    }

    private void UpdateSubsetOkState()
    {
        SubsetCountText.Text = $"Selected: {_subsetPicks.Count} (need {_subsetMin}–{_subsetMax})";
        SubsetOkButton.IsEnabled =
            _subsetPicks.Count >= _subsetMin && _subsetPicks.Count <= _subsetMax;
    }

    // Tint colors for the dogma-legality border. Kept as statics so the
    // tint decision stays in one place for easy tweaking.
    private static readonly Brush DogmaDemandBrush = new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28));
    private static readonly Brush DogmaSharedBrush = new SolidColorBrush(Color.FromRgb(0xE6, 0xB8, 0x1C));
    private static readonly Brush DogmaSoloBrush   = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));

    private Brush BorderForDogmaLegality(CardColor color, CoreCard topCard)
    {
        if (_legalDogmaColors is not { } legal || !legal.Contains(color))
            return Brushes.Transparent;

        var def = _registry.Get(topCard.Id);
        var featured = def.FeaturedIcon;
        int activatorIcons = IconCounter.Count(_state.Players[_state.ActivePlayer], featured, _cards);
        bool anyDemandApplies = false, anyShareApplies = false;
        for (int pi = 0; pi < _state.Players.Length; pi++)
        {
            if (pi == _state.ActivePlayer) continue;
            int oppIcons = IconCounter.Count(_state.Players[pi], featured, _cards);
            foreach (var eff in def.Effects)
            {
                if (eff.IsDemand && oppIcons < activatorIcons) anyDemandApplies = true;
                if (!eff.IsDemand && oppIcons >= activatorIcons) anyShareApplies = true;
            }
        }
        if (anyDemandApplies) return DogmaDemandBrush;
        if (anyShareApplies) return DogmaSharedBrush;
        return DogmaSoloBrush;
    }

    private void PreviewCard(CoreCard card)
    {
        _detailCard = card;
        DetailCard.Card = card;
        ViewCardCombo.SelectedItem = card.Title;
    }

    private void ShowStackDialog(int playerIndex, CardColor color)
    {
        var stack = _state.Players[playerIndex].Stack(color);
        var topFirst = new List<CoreCard>();
        // ColorStack.Cards is stored top-first already (see ColorStack.Meld).
        for (int i = 0; i < stack.Count; i++)
            topFirst.Add(_cards[stack.Cards[i]]);
        string who = playerIndex == HumanIndex ? "Your" : $"Player {playerIndex + 1}'s";
        var dlg = new StackViewDialog($"{who} {color} stack", topFirst, stack.Splay, PreviewCard)
        {
            Owner = this,
        };
        dlg.ShowDialog();
    }

    private void OnAgeAchievementClicked(int age)
    {
        if (_legalAchieveAges is { } ages && ages.Contains(age))
        {
            _actionTcs?.TrySetResult(new AchieveAction(age));
        }
    }

    private void OnBoardCardClicked(CardColor color, CoreCard topCard)
    {
        _detailCard = topCard;
        DetailCard.Card = topCard;
        ViewCardCombo.SelectedItem = topCard.Title;

        if (_legalDogmaColors is { } colors && colors.Contains(color))
        {
            _actionTcs?.TrySetResult(new DogmaAction(color));
        }
    }

    // Subset list uses this for its ListBox items; other affordances
    // are driven by click-through on the existing board/hand/ach tiles.
    private sealed record HandCardChoice(int CardId, string Name, int Age, CardColor Color)
    { public override string ToString() => $"[{Age}] {Name} ({Color})"; }

    private void HideAllPromptPanels()
    {
        ActionPanel.Visibility = Visibility.Collapsed;
        HandCardPanel.Visibility = Visibility.Collapsed;
        SubsetPanel.Visibility = Visibility.Collapsed;
        YesNoPanel.Visibility = Visibility.Collapsed;
        ColorPanel.Visibility = Visibility.Collapsed;
        ScoreCardPanel.Visibility = Visibility.Collapsed;
        // Clear click-to-act dispatch so later clicks are preview-only.
        _legalMeldIds = null;
        _legalAchieveAges = null;
        _legalDogmaColors = null;
        _legalHandPickIds = null;
        _subsetEligibleIds = null;
        _scoreSubsetEligibleIds = null;
        _subsetPicks.Clear();
        // Re-render hand + board + score once the prompt goes away so
        // any lingering selection/tint borders are cleared.
        PopulateYourHand();
        PopulateYourBoard();
        PopulateYourScore();
    }

    private T RunPromptBlocking<T>(Action<TaskCompletionSource<T>> setup)
    {
        if (_shutdown.IsCancellationRequested) throw new OperationCanceledException();
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var reg = _shutdown.Token.Register(() => tcs.TrySetCanceled());
        Dispatcher.Invoke(() =>
        {
            ContinueButton.Visibility = Visibility.Collapsed;
            ContinueButton.IsEnabled = false;
            setup(tcs);
        });
        try
        {
            return tcs.Task.GetAwaiter().GetResult();
        }
        finally
        {
            try { Dispatcher.Invoke(HideAllPromptPanels); }
            catch (TaskCanceledException) { /* shutting down */ }
        }
    }

    public int PromptInitialMeld(GameState g, PlayerState self)
    {
        var req = new SelectHandCardRequest
        {
            Prompt = "Choose your starting card to meld.",
            PlayerIndex = self.Index,
            EligibleCardIds = self.Hand.ToList(),
            AllowNone = false,
        };
        var pick = PromptHandCard(g, self, req);
        return pick ?? self.Hand[0];
    }

    public PlayerAction PromptAction(GameState g, PlayerState self, IReadOnlyList<PlayerAction> legal)
    {
        return RunPromptBlocking<PlayerAction>(tcs =>
        {
            _actionTcs = tcs;
            PromptText.Text = $"Your turn — {g.ActionsRemaining} action(s) left. Pick one.";
            PopulateActionPanel(legal, self);
            ActionPanel.Visibility = Visibility.Visible;
        });
    }

    private void PopulateActionPanel(IReadOnlyList<PlayerAction> legal, PlayerState self)
    {
        DrawButton.IsEnabled = legal.Any(a => a is DrawAction);
        _legalMeldIds = legal.OfType<MeldAction>().Select(m => m.CardId).ToHashSet();
        _legalAchieveAges = legal.OfType<AchieveAction>().Select(a => a.Age).ToHashSet();
        _legalDogmaColors = legal.OfType<DogmaAction>().Select(d => d.Color).ToHashSet();

        var parts = new List<string>();
        if (DrawButton.IsEnabled) parts.Add("Draw");
        if (_legalMeldIds.Count > 0) parts.Add("click a card in Your Hand to Meld");
        if (_legalAchieveAges.Count > 0) parts.Add("click an age in Achievements Remaining to Achieve");
        if (_legalDogmaColors.Count > 0) parts.Add("click a card on Your Board to activate its Dogma");
        ActionHintText.Text = parts.Count == 0
            ? "(no legal actions)"
            : string.Join(" · ", parts) + ".";

        // Redraw the board so the dogma-legality tint frames appear.
        PopulateYourBoard();
    }

    public int? PromptHandCard(GameState g, PlayerState self, SelectHandCardRequest req)
    {
        return RunPromptBlocking<int?>(tcs =>
        {
            _handCardTcs = tcs;
            _legalHandPickIds = req.EligibleCardIds.ToHashSet();
            PromptText.Text = req.Prompt;
            HandCardNoneButton.Visibility = req.AllowNone ? Visibility.Visible : Visibility.Collapsed;
            HandCardPanel.Visibility = Visibility.Visible;
        });
    }

    public IReadOnlyList<int> PromptHandCardSubset(GameState g, PlayerState self, SelectHandCardSubsetRequest req)
    {
        return RunPromptBlocking<IReadOnlyList<int>>(tcs =>
        {
            _subsetTcs = tcs;
            _subsetEligibleIds = req.EligibleCardIds.ToHashSet();
            _subsetPicks.Clear();
            _subsetMin = req.MinCount;
            _subsetMax = req.MaxCount;
            PromptText.Text = $"{req.Prompt} (pick {req.MinCount}–{req.MaxCount})";
            SubsetPanel.Visibility = Visibility.Visible;
            // Rerender so eligible tiles start with a fresh (unselected)
            // frame; UpdateSubsetOkState gates the OK button.
            PopulateYourHand();
            UpdateSubsetOkState();
        });
    }

    public IReadOnlyList<int> PromptScoreCardSubset(GameState g, PlayerState self, SelectScoreCardSubsetRequest req)
    {
        return RunPromptBlocking<IReadOnlyList<int>>(tcs =>
        {
            _subsetTcs = tcs;
            _scoreSubsetEligibleIds = req.EligibleCardIds.ToHashSet();
            _subsetPicks.Clear();
            _subsetMin = req.MinCount;
            _subsetMax = req.MaxCount;
            PromptText.Text = $"{req.Prompt} (pick {req.MinCount}–{req.MaxCount})";
            SubsetPanel.Visibility = Visibility.Visible;
            // Rerender score pile so eligible tiles get the click cursor +
            // fresh (unselected) frame.
            PopulateYourScore();
            UpdateSubsetOkState();
        });
    }

    public bool PromptYesNo(GameState g, PlayerState self, YesNoChoiceRequest req)
    {
        return RunPromptBlocking<bool>(tcs =>
        {
            _yesNoTcs = tcs;
            PromptText.Text = req.Prompt;
            YesNoPanel.Visibility = Visibility.Visible;
        });
    }

    public CardColor? PromptColor(GameState g, PlayerState self, SelectColorRequest req)
    {
        return RunPromptBlocking<CardColor?>(tcs =>
        {
            _colorTcs = tcs;
            PromptText.Text = req.Prompt;
            ColorPanel.Children.Clear();
            foreach (var color in req.EligibleColors)
            {
                var btn = new Button
                {
                    Content = color.ToString(),
                    Width = 90,
                    Margin = new Thickness(0, 0, 6, 0),
                    Padding = new Thickness(6, 2, 6, 2),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 12,
                };
                var captured = color;
                btn.Click += (_, _) => _colorTcs?.TrySetResult(captured);
                ColorPanel.Children.Add(btn);
            }
            if (req.AllowNone)
            {
                var skip = new Button
                {
                    Content = "Skip",
                    Width = 90,
                    Margin = new Thickness(0, 0, 6, 0),
                    Padding = new Thickness(6, 2, 6, 2),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 12,
                };
                skip.Click += (_, _) => _colorTcs?.TrySetResult(null);
                ColorPanel.Children.Add(skip);
            }
            ColorPanel.Visibility = Visibility.Visible;
        });
    }

    public int? PromptScoreCard(GameState g, PlayerState self, SelectScoreCardRequest req)
    {
        return RunPromptBlocking<int?>(tcs =>
        {
            _scoreCardTcs = tcs;
            PromptText.Text = req.Prompt;
            ScoreCardPanel.Children.Clear();
            foreach (var cardId in req.EligibleCardIds)
            {
                var card = g.Cards[cardId];
                var btn = new Button
                {
                    Content = $"[{card.Age}] {card.Title}",
                    Margin = new Thickness(0, 0, 6, 0),
                    Padding = new Thickness(6, 2, 6, 2),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 12,
                };
                var captured = cardId;
                btn.Click += (_, _) => _scoreCardTcs?.TrySetResult(captured);
                ScoreCardPanel.Children.Add(btn);
            }
            if (req.AllowNone)
            {
                var skip = new Button
                {
                    Content = "Skip",
                    Margin = new Thickness(0, 0, 6, 0),
                    Padding = new Thickness(6, 2, 6, 2),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 12,
                };
                skip.Click += (_, _) => _scoreCardTcs?.TrySetResult(null);
                ScoreCardPanel.Children.Add(skip);
            }
            ScoreCardPanel.Visibility = Visibility.Visible;
        });
    }

    private void CopyStateButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        try
        {
            var code = GameStateCodec.Encode(_state);
            System.Windows.Clipboard.SetText(code);
            string suffix = _state.Phase == GamePhase.Dogma
                ? " — WARNING: mid-dogma, can't be reloaded (use the turn-start [state] lines in the log)"
                : "";
            _log.Add($"[state copied to clipboard, {code.Length} chars]{suffix}");
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, ex.Message, "Copy state");
        }
    }

    private void LoadStateButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var dlg = new LoadStateDialog { Owner = this };
        if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.Code)) return;
        GameState decoded;
        try
        {
            decoded = GameStateCodec.Decode(dlg.Code.Trim(), _cards);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, $"Invalid state code:\n{ex.Message}", "Load state");
            return;
        }
        if (decoded.Phase == GamePhase.Dogma)
        {
            System.Windows.MessageBox.Show(this,
                "This code was captured mid-dogma. The codec doesn't preserve "
                + "in-flight dogma resolution state, so loading it would deadlock. "
                + "Use a turn-start state code (the [state] lines emitted in the log "
                + "at the beginning of each turn) instead.",
                "Load state");
            return;
        }
        PendingLoadCode = dlg.Code.Trim();
        PendingSeats = _seatKinds;
        var w = new MainWindow();
        w.Show();
        Close();
    }

    internal static string? PendingLoadCode;
    internal static SeatKind[]? PendingSeats;

    private void OpenLogButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var path = GameLog.CurrentPath;
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
        {
            System.Windows.MessageBox.Show(this, "No log file available yet.", "Open log");
            return;
        }
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, $"Couldn't open log:\n{ex.Message}", "Open log");
        }
    }

    public IReadOnlyList<int> PromptCardOrder(GameState g, PlayerState self, SelectCardOrderRequest req)
    {
        // Dialog shows the FINAL placement (top-first). Each action gets
        // labels appropriate to where the cards land:
        //   • meld   → top of resulting pile / bottom of melded chunk
        //   • tuck   → top of tucked chunk (just below existing bottom) / very bottom
        //   • return → next card drawn from this age / last card drawn
        IReadOnlyList<int>? chosen = null;
        Dispatcher.Invoke(() =>
        {
            var cards = req.CardIds.Select(id => _cards[id]).ToList();
            string title = req.Action switch
            {
                "meld"   => $"Meld order — final pile arrangement",
                "tuck"   => $"Tuck order — tucked chunk arrangement",
                "return" => $"Return order — deck arrangement",
                _        => $"Order ({req.Action})",
            };
            (string top, string bottom, string sub) = req.Action switch
            {
                "meld"   => ("Top of pile (last melded)",
                             "Bottom of melded chunk (first melded)",
                             "Order these cards as you want them stacked, top to bottom. The top card is the one you'll show as the active dogma; the bottom card is melded first so it ends up underneath."),
                "tuck"   => ("Top of tucked chunk (just below existing bottom)",
                             "Very bottom of pile (last tucked)",
                             "Order these cards as you want them stacked at the bottom of their colour piles, top to bottom of the new chunk."),
                "return" => ("Next card drawn from this age",
                             "Last card drawn from this age",
                             "Order these cards from next-drawn (top) to drawn-last (bottom of deck)."),
                _        => ("First", "Last", req.Prompt),
            };
            var dlg = new StackReorderDialog(title, cards, Splay.None,
                topLabel: top, bottomLabel: bottom, subtitle: sub) { Owner = this };
            dlg.ShowDialog();
            chosen = dlg.Result;
        });
        return chosen ?? req.CardIds.ToList();
    }

    public IReadOnlyList<int> PromptStackOrder(GameState g, PlayerState self, SelectStackOrderRequest req)
    {
        // Modal Up/Down reorder dialog. Cancel keeps the original order.
        IReadOnlyList<int>? chosen = null;
        Dispatcher.Invoke(() =>
        {
            var topFirst = req.CurrentOrder.Select(id => _cards[id]).ToList();
            var dlg = new StackReorderDialog(
                $"Publications: reorder your {req.Color} pile",
                topFirst,
                self.Stack(req.Color).Splay)
            {
                Owner = this,
            };
            dlg.ShowDialog();
            chosen = dlg.Result;
        });
        return chosen ?? req.CurrentOrder.ToList();
    }

    public int? PromptValue(GameState g, PlayerState self, SelectValueRequest req)
    {
        int? chosen = null;
        Dispatcher.Invoke(() =>
        {
            var dlg = new Window
            {
                Title = "Choose a value",
                SizeToContent = SizeToContent.WidthAndHeight,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF3, 0xEF, 0xD3)),
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                FontSize = 13,
            };
            var panel = new System.Windows.Controls.StackPanel { Margin = new Thickness(12) };
            panel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = req.Prompt,
                Margin = new Thickness(0, 0, 0, 8),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 360,
            });
            var buttons = new System.Windows.Controls.WrapPanel();
            foreach (var v in req.EligibleValues)
            {
                int captured = v;
                var b = new System.Windows.Controls.Button
                {
                    Content = v.ToString(),
                    MinWidth = 40,
                    Margin = new Thickness(2),
                    Padding = new Thickness(8, 4, 8, 4),
                };
                b.Click += (_, _) => { chosen = captured; dlg.Close(); };
                buttons.Children.Add(b);
            }
            if (req.AllowNone)
            {
                var none = new System.Windows.Controls.Button
                {
                    Content = "None",
                    MinWidth = 60,
                    Margin = new Thickness(2),
                    Padding = new Thickness(8, 4, 8, 4),
                };
                none.Click += (_, _) => { chosen = null; dlg.Close(); };
                buttons.Children.Add(none);
            }
            panel.Children.Add(buttons);
            dlg.Content = panel;
            dlg.ShowDialog();
        });
        return chosen;
    }
}
