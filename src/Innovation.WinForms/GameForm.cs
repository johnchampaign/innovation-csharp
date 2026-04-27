using Innovation.Core;
using Innovation.Core.Players;
using Innovation.WinForms.Cards;
using Innovation.WinForms.Prompts;

// Disambiguate: both System.Drawing.Icon and Form.Icon (property)
// shadow the card-icon enum inside this class. Alias to a name that
// doesn't collide with either.
using CardIcon = Innovation.Core.Icon;

namespace Innovation.WinForms;

/// <summary>
/// Main game window. Hosts the engine on a background <see cref="Task"/>
/// so the UI thread stays responsive while the worker blocks waiting for
/// a human's click. Also implements <see cref="IUserPromptSink"/> — each
/// prompt method is called on the worker thread, marshals work onto the
/// UI thread via <see cref="Control.Invoke(Delegate)"/>, and blocks the
/// worker until the UI supplies an answer.
///
/// Board is rendered as per-color rows of clickable card "tiles" (flat
/// buttons color-coded by the card's color) rather than plain text, so
/// the user can jump the card viewer to any visible card with one
/// click. Mid-session New Game tears down the worker cleanly, including
/// auto-closing any in-flight modal prompt via a CancellationToken.
/// </summary>
public sealed class GameForm : Form, IUserPromptSink
{
    // Mutable so StartNewGame can swap in a fresh config + token source
    // without needing to re-open the whole form.
    private NewGameConfig _config;
    private readonly IReadOnlyList<Card> _cards;

    // Engine state. Written on the worker thread, read on the UI thread
    // only while a prompt has the worker parked.
    private GameState _g = null!;
    private GameRunner _runner = null!;
    private Task? _gameTask;
    private CancellationTokenSource _shutdown = new();

    // UI controls.
    private readonly ToolStripStatusLabel _statusLabel = new();
    // Player-board panels. _selfBoard gets the full treatment (pile
    // tiles + summary + hand); _opponentBoard is rendered in a compact
    // one-line-per-pile style at the bottom of the main column.
    private readonly TableLayoutPanel _selfBoard = NewBoardPanel();
    private readonly TableLayoutPanel _opponentBoard = NewBoardPanel();
    private readonly RichTextBox _logBox = new() { Dock = DockStyle.Fill, ReadOnly = true, Font = new Font("Consolas", 9f), BackColor = Color.White };
    private readonly ComboBox _achieveCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80 };
    private readonly ComboBox _meldCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly ComboBox _dogmaCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
    private readonly Button _achieveBtn = new() { Text = "Achieve", Width = 80 };
    private readonly Button _drawBtn = new() { Text = "Draw", Width = 80 };
    private readonly Button _meldBtn = new() { Text = "Meld", Width = 80 };
    private readonly Button _dogmaBtn = new() { Text = "Dogma", Width = 80 };

    // Left sidebar: card viewer + reference panels. The viewer's
    // dropdown is the user's ALL-CARDS browse; clicks on any tile
    // elsewhere drive its SelectedIndex via ShowCardInViewer.
    private readonly ComboBox _cardViewerPicker = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CardControl _cardViewer = new() { Dock = DockStyle.Fill, Margin = new Padding(2) };
    private readonly RichTextBox _cardViewerText = new() { Dock = DockStyle.Bottom, ReadOnly = true, Font = new Font("Consolas", 8.5f), BackColor = Color.WhiteSmoke, Height = 110, ScrollBars = RichTextBoxScrollBars.Vertical };
    // Per-render rebuild targets on the left sidebar.
    private readonly TableLayoutPanel _achRemainingPanel = new() { Dock = DockStyle.Fill, ColumnCount = 1 };
    private readonly TableLayoutPanel _cardsRemainingPanel = new() { Dock = DockStyle.Fill, ColumnCount = 2 };

    // Top of main column: a bold prompt/status line and a per-player
    // icon comparison so both seats' totals are visible at once.
    private readonly Label _promptLabel = new()
    {
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
        ForeColor = Color.MidnightBlue,
        BackColor = Color.FromArgb(0xFF, 0xE8, 0xEE, 0xF8),
        BorderStyle = BorderStyle.FixedSingle,
        Padding = new Padding(10, 0, 8, 0),
        Margin = new Padding(0, 0, 0, 0),
    };
    private readonly TableLayoutPanel _iconComparePanel = new() { Dock = DockStyle.Fill, ColumnCount = 2 };
    // The viewer's own hand, laid out as clickable tiles. Rebuilt on
    // every render from _g.Players[0].Hand — separate row below the
    // boards so it's always visible without scrolling the self-board.
    private readonly FlowLayoutPanel _handPanel = new()
    {
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.LeftToRight,
        WrapContents = true,
        AutoScroll = true,
        BackColor = Color.White,
    };

    // How long to pause after each engine step so the user can see a
    // state transition flash past. Only meaningful when running without
    // a human in the loop — human turns naturally pause on the prompt.
    private static readonly TimeSpan StepVisibilityDelay = TimeSpan.FromMilliseconds(200);

    // Set when PromptAction is waiting; UI button clicks complete it.
    private TaskCompletionSource<PlayerAction>? _pendingAction;

    public GameForm(NewGameConfig config)
    {
        _config = config;
        _cards = CardDataLoader.LoadFromEmbeddedResource();

        Text = "Innovation";
        // Height bumped so the action bar + "Cards Remaining" bottom rows
        // aren't clipped at common desktop sizes. 940 still fits 1080p
        // with a taskbar.
        ClientSize = new Size(1440, 940);
        StartPosition = FormStartPosition.CenterScreen;

        BuildMenuAndStatus();
        BuildLayout();
        WireActionHandlers();

        Shown += (_, _) => StartGame();
        FormClosing += OnFormClosing;
    }

    // ---------- UI construction ----------

    private void BuildMenuAndStatus()
    {
        var menu = new MenuStrip();
        var fileMenu = new ToolStripMenuItem("&File");
        fileMenu.DropDownItems.Add("&New Game…", null, async (_, _) => await StartNewGameAsync());
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add("E&xit", null, (_, _) => Close());
        menu.Items.Add(fileMenu);
        MainMenuStrip = menu;
        Controls.Add(menu);

        var status = new StatusStrip();
        _statusLabel.Text = "Starting…";
        _statusLabel.Spring = true;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        status.Items.Add(_statusLabel);
        Controls.Add(status);
    }

    private void BuildLayout()
    {
        // Root = three columns: left sidebar (card viewer + reference
        // panels), main play area (prompt, icon compare, boards, hand,
        // actions), right sidebar (event log). Matches the VB6 layout's
        // reading order: information to consult on the left, play area
        // in the middle, event history on the right.
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(4),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        root.Controls.Add(BuildLeftSidebar(), 0, 0);
        root.Controls.Add(BuildMainColumn(), 1, 0);
        root.Controls.Add(WrapInGroup("Event Log", _logBox), 2, 0);

        Controls.Add(root);
    }

    /// <summary>
    /// Left sidebar: View Card preview on top (big), then reference
    /// panels for Achievements Remaining and Cards Remaining. The
    /// reference panels are passive — they're rebuilt from the engine's
    /// <c>AvailableAgeAchievements</c> / <c>AvailableSpecialAchievements</c>
    /// / <c>Decks</c> on every step.
    /// </summary>
    private Control BuildLeftSidebar()
    {
        var col = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
        };
        col.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        // View Card = stretch (biggest informative area); Achievements +
        // Cards remaining are Absolute so their content (chips + 5-row
        // grid) never gets compressed below the text height.
        col.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        col.RowStyles.Add(new RowStyle(SizeType.Absolute, 180));
        col.RowStyles.Add(new RowStyle(SizeType.Absolute, 180));

        col.Controls.Add(WrapInGroup("View Card", BuildCardViewerPanel()), 0, 0);
        col.Controls.Add(WrapInGroup("Achievements Remaining", _achRemainingPanel), 0, 1);
        col.Controls.Add(WrapInGroup("Cards Remaining", _cardsRemainingPanel), 0, 2);
        return col;
    }

    /// <summary>
    /// Main play area. Top-down: the prompt strip telling the user
    /// what's happening, a per-player icon comparison row, the full
    /// "Your Board" pile tiles, the compact opponent strip, your hand,
    /// and the action buttons. Each section sits in its own row so
    /// layout changes stay local.
    /// </summary>
    private Control BuildMainColumn()
    {
        var col = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
        };
        col.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        col.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));   // prompt
        col.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));  // icon compare (header + 2 player rows + group-box chrome)
        col.RowStyles.Add(new RowStyle(SizeType.Percent, 60));    // your board
        col.RowStyles.Add(new RowStyle(SizeType.Percent, 40));    // opponent compact
        col.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));   // hand
        col.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));   // actions — Absolute, not AutoSize (AutoSize + Percent rows is unreliable)

        col.Controls.Add(BuildPromptStrip(), 0, 0);
        col.Controls.Add(WrapInGroup("Players", _iconComparePanel), 0, 1);
        col.Controls.Add(WrapInGroup("Your Board", _selfBoard), 0, 2);
        col.Controls.Add(WrapInGroup("Opponent Board", _opponentBoard), 0, 3);
        col.Controls.Add(WrapInGroup("Your Hand", _handPanel), 0, 4);
        col.Controls.Add(BuildActionPanel(), 0, 5);
        return col;
    }

    /// <summary>
    /// Prompt strip. Single tinted label whose text is refreshed in
    /// <see cref="RenderState"/> to describe the current phase /
    /// waiting state / game-over. Returned directly (no wrapping Panel)
    /// because a FixedSingle-bordered Panel wrapping a Fill label was
    /// eating the top pixel row of text on our 34px strip.
    /// </summary>
    private Control BuildPromptStrip() => _promptLabel;

    /// <summary>
    /// View Card pane — picker on top, a dynamically-painted
    /// <see cref="CardControl"/> filling the middle, and a compact
    /// text fallback docked below for the full-length dogma
    /// paragraphs (the rendered card truncates long effects to fit).
    /// </summary>
    private Panel BuildCardViewerPanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill };
        // Dock order, remembering WinForms processes children in
        // insert order: picker (Top) → text (Bottom) → card (Fill).
        panel.Controls.Add(_cardViewer);
        panel.Controls.Add(_cardViewerText);
        panel.Controls.Add(_cardViewerPicker);

        // Populate the picker with every card, sorted by age then title —
        // matches how the rulebook's index is organized.
        foreach (var card in _cards.OrderBy(c => c.Age).ThenBy(c => c.Title))
            _cardViewerPicker.Items.Add(new CardEntry(card));
        _cardViewerPicker.SelectedIndexChanged += (_, _) =>
        {
            if (_cardViewerPicker.SelectedItem is CardEntry e)
            {
                _cardViewer.Card = e.Card;
                _cardViewerText.Text = FormatCardDetails(e.Card);
            }
        };
        if (_cardViewerPicker.Items.Count > 0) _cardViewerPicker.SelectedIndex = 0;
        return panel;
    }

    private sealed record CardEntry(Card Card)
    {
        public override string ToString() => $"[{Card.Age}] {Card.Title} ({Card.Color})";
    }

    private static string FormatCardDetails(Card c)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[Age {c.Age}] {c.Title}");
        sb.AppendLine($"Color: {c.Color}");
        sb.AppendLine();
        sb.AppendLine("Icons:");
        sb.AppendLine($"  Top-left   : {c.Left}");
        sb.AppendLine($"  Top-middle : {c.Middle}");
        sb.AppendLine($"  Top-right  : {c.Right}");
        sb.AppendLine($"  Hex ({c.HexagonSlot}) : {c.Top}");
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(c.HexagonDescription))
        {
            sb.AppendLine($"Hexagon: {c.HexagonDescription}");
            sb.AppendLine();
        }
        sb.AppendLine($"Dogma icon: {c.DogmaIcon}");
        if (c.DogmaEffects.Count == 0)
        {
            sb.AppendLine("  (no dogma effects)");
        }
        else
        {
            for (int i = 0; i < c.DogmaEffects.Count; i++)
                sb.AppendLine($"  {i + 1}. {c.DogmaEffects[i]}");
        }
        return sb.ToString();
    }

    private static GroupBox WrapInGroup(string title, Control inner)
    {
        var gb = new GroupBox { Text = title, Dock = DockStyle.Fill, Padding = new Padding(6) };
        inner.Dock = DockStyle.Fill;
        gb.Controls.Add(inner);
        return gb;
    }

    private FlowLayoutPanel BuildActionPanel()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
        };
        void Add(params Control[] cs) { foreach (var c in cs) panel.Controls.Add(c); }
        Add(new Label { Text = "Achieve:", AutoSize = true, Margin = new Padding(8, 8, 2, 0) });
        Add(_achieveCombo, _achieveBtn);
        Add(new Label { Text = "   Draw:", AutoSize = true, Margin = new Padding(8, 8, 2, 0) });
        Add(_drawBtn);
        Add(new Label { Text = "   Meld:", AutoSize = true, Margin = new Padding(8, 8, 2, 0) });
        Add(_meldCombo, _meldBtn);
        Add(new Label { Text = "   Dogma:", AutoSize = true, Margin = new Padding(8, 8, 2, 0) });
        Add(_dogmaCombo, _dogmaBtn);
        DisableAllActions();
        return panel;
    }

    private void WireActionHandlers()
    {
        _achieveBtn.Click += (_, _) =>
        {
            if (_achieveCombo.SelectedItem is AchieveChoice a) CompleteAction(new AchieveAction(a.Age));
        };
        _drawBtn.Click += (_, _) => CompleteAction(new DrawAction());
        _meldBtn.Click += (_, _) =>
        {
            if (_meldCombo.SelectedItem is MeldChoice m) CompleteAction(new MeldAction(m.CardId));
        };
        _dogmaBtn.Click += (_, _) =>
        {
            if (_dogmaCombo.SelectedItem is DogmaChoice d) CompleteAction(new DogmaAction(d.Color));
        };
    }

    // Small record types so the combos can show friendly text via ToString.
    private sealed record AchieveChoice(int Age) { public override string ToString() => $"Age {Age}"; }
    private sealed record MeldChoice(int CardId, string Name, int Age, CardColor Color)
    { public override string ToString() => $"[{Age}] {Name} ({Color})"; }
    private sealed record DogmaChoice(CardColor Color, string Top)
    { public override string ToString() => $"{Color}: {Top}"; }

    // ---------- Game lifecycle ----------

    private void StartGame()
    {
        _g = GameSetup.Create(_cards, 2, new Random(_config.Seed));
        var c0 = MakeController(_config.Player0, 0);
        var c1 = MakeController(_config.Player1, 1);
        _runner = new GameRunner(_g, new[] { c0, c1 })
        {
            // Fires on the worker thread after every step. We marshal a
            // repaint + log line onto the UI thread and then briefly
            // sleep so the user can see the transition before the next
            // step runs (important for AI-vs-AI pacing).
            OnStepCompleted = OnEngineStepped,
        };
        AppendLog($"New game — seed {_config.Seed}. P0={_config.Player0}, P1={_config.Player1}.");
        _gameTask = Task.Run(() => RunGameLoop(_shutdown.Token));
    }

    private void OnEngineStepped(int actor, PlayerAction? action)
    {
        // If the form is shutting down, propagate cancellation so
        // RunGameLoop's catch exits the worker — otherwise the loop
        // would keep stepping and failing on every UiInvoke.
        _shutdown.Token.ThrowIfCancellationRequested();

        UiInvoke(() =>
        {
            RenderState(Array.Empty<PlayerAction>(), self: null);
            if (actor == -1) AppendLog("(setup) Initial melds applied.");
            else if (action != null) AppendLog($"P{actor} → {DescribeAction(action)}");
            else AppendLog($"P{actor} → (dogma resolved)");
        });

        // Interruptible pause so the user can see each transition. If
        // the shutdown token fires during the wait, we throw on the
        // next line and the worker exits.
        _shutdown.Token.WaitHandle.WaitOne(StepVisibilityDelay);
        _shutdown.Token.ThrowIfCancellationRequested();
    }

    private IPlayerController MakeController(SeatKind kind, int seat) => kind switch
    {
        SeatKind.Human => new HumanController(this),
        SeatKind.Random => new RandomController(_config.Seed + 1_000 * (seat + 1)),
        SeatKind.Greedy => new GreedyController(_config.Seed + 10_000 * (seat + 1)),
        _ => throw new InvalidOperationException($"Unknown SeatKind {kind}"),
    };

    private void RunGameLoop(CancellationToken ct)
    {
        try
        {
            _runner.CompleteInitialMeld();
            _runner.RunToCompletion();
            ct.ThrowIfCancellationRequested();
            UiInvoke(() =>
            {
                RenderState(Array.Empty<PlayerAction>(), self: null);
                var winners = _g.Winners.Count == 0 ? "(none)" : string.Join(", ", _g.Winners.Select(i => $"P{i}"));
                AppendLog($"Game over. Winners: {winners}.");
                MessageBox.Show(this, $"Game over. Winners: {winners}.", "Innovation", MessageBoxButtons.OK);
            });
        }
        catch (OperationCanceledException) { /* form closing */ }
        catch (ObjectDisposedException) { /* form disposed mid-Invoke */ }
        catch (Exception ex)
        {
            try { UiInvoke(() => MessageBox.Show(this, $"Engine error:\n{ex}", "Innovation", MessageBoxButtons.OK, MessageBoxIcon.Error)); }
            catch { /* form disposed */ }
        }
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        _shutdown.Cancel();
        // Unblock any pending PromptAction so the worker can exit.
        _pendingAction?.TrySetCanceled();
    }

    /// <summary>
    /// Tear down the current game loop (including any in-flight modal
    /// prompt — each of those is registered against
    /// <see cref="_shutdown"/> and auto-closes on cancel), prompt for
    /// fresh settings, and start a new game. Runs on the UI thread;
    /// the worker task is awaited so we never have two engines live at
    /// once.
    /// </summary>
    private async Task StartNewGameAsync()
    {
        // Cancel first — this closes any open modal prompts (they
        // register against the token) and makes PromptAction throw.
        _shutdown.Cancel();
        _pendingAction?.TrySetCanceled();

        if (_gameTask is { } oldTask)
        {
            try { await oldTask.ConfigureAwait(true); }
            catch (OperationCanceledException) { /* expected */ }
            catch (ObjectDisposedException) { /* expected */ }
        }
        _shutdown.Dispose();
        _shutdown = new CancellationTokenSource();
        _gameTask = null;
        _pendingAction = null;

        // Collect new settings; user can back out of the dialog to keep
        // the (now-dead) game state visible.
        using var dlg = new NewGameDialog(_config);
        if (dlg.ShowDialog(this) != DialogResult.OK)
        {
            AppendLog("New Game canceled; previous game was terminated.");
            return;
        }

        _config = dlg.Config;
        _logBox.Clear();
        DisableAllActions();
        StartGame();
    }

    // ---------- Rendering ----------

    /// <summary>
    /// Repaint every surface from the current <see cref="GameState"/>,
    /// then enable whichever action controls correspond to
    /// <paramref name="legal"/>. Must be called on the UI thread.
    /// </summary>
    private void RenderState(IReadOnlyList<PlayerAction> legal, PlayerState? self)
    {
        _statusLabel.Text = _g.IsGameOver
            ? $"Game over — {_g.Winners.Count} winner(s)"
            : $"Turn {_g.CurrentTurn}   ·   Active: P{_g.ActivePlayer}   ·   Phase: {_g.Phase}   ·   Actions left: {_g.ActionsRemaining}";

        UpdatePromptStrip(self);
        RefreshIconComparison();
        RefreshAchievementsRemaining();
        RefreshCardsRemaining();

        // Viewer perspective is fixed at P0 for now. P0 gets the full
        // "Your Board" treatment; P1 goes to the compact opponent
        // strip; P0's hand is rendered separately in its own row.
        RenderYourBoardInto(_selfBoard, _g.Players[0]);
        RenderOpponentCompactInto(_opponentBoard, _g.Players[1]);
        RenderHandInto(_handPanel, _g.Players[0]);

        PopulateActionControls(legal, self);
    }

    /// <summary>
    /// Human-readable summary of what's happening right now. Drives
    /// the blue-tinted prompt strip at the top of the main column.
    /// Argument <paramref name="active"/> is non-null only when we're
    /// paused waiting for a top-level action from a human — we
    /// distinguish that "your turn" case so the user knows to click.
    /// </summary>
    private void UpdatePromptStrip(PlayerState? active)
    {
        if (_g.IsGameOver)
        {
            var winners = _g.Winners.Count == 0 ? "(none)" : string.Join(", ", _g.Winners.Select(i => $"P{i}"));
            _promptLabel.Text = $"Game over. Winners: {winners}.";
            _promptLabel.ForeColor = Color.DarkGreen;
            return;
        }

        _promptLabel.ForeColor = Color.MidnightBlue;
        if (active is { } a && SeatKindOf(a.Index) == SeatKind.Human)
        {
            _promptLabel.Text = $"Your turn (P{a.Index}) · Action {3 - _g.ActionsRemaining} of 2 · Choose Achieve / Draw / Meld / Dogma.";
        }
        else
        {
            string who = $"P{_g.ActivePlayer}";
            string kind = SeatKindOf(_g.ActivePlayer) switch
            {
                SeatKind.Human => "Human",
                SeatKind.Greedy => "Greedy AI",
                SeatKind.Random => "Random AI",
                _ => "AI",
            };
            _promptLabel.Text = $"Turn {_g.CurrentTurn} · {who} ({kind}) thinking… · {_g.Phase}, {_g.ActionsRemaining} action(s) left.";
        }
    }

    /// <summary>Seat kind from the live config — keeps the prompt
    /// strip free of the runner/controller plumbing.</summary>
    private SeatKind SeatKindOf(int seat) => seat switch
    {
        0 => _config.Player0,
        1 => _config.Player1,
        _ => SeatKind.Random,
    };

    // ---------- Icon comparison (both players) ----------

    /// <summary>
    /// Grid of icon totals per player. Column 0 is the player label;
    /// columns 1–6 are the six icons. Row 0 is a colored header row
    /// (icon name, tinted to its thematic color — matches how the VB6
    /// original used a colored-band header); rows 1 and 2 are P0 and
    /// P1 counts. Using absolute row heights (not percentage) because
    /// fractional splits of this short strip rounded down below the
    /// text height, which is what caused the pills to visually
    /// overlap in earlier builds.
    /// </summary>
    private void RefreshIconComparison()
    {
        _iconComparePanel.SuspendLayout();
        for (int i = _iconComparePanel.Controls.Count - 1; i >= 0; i--)
            _iconComparePanel.Controls[i].Dispose();
        _iconComparePanel.Controls.Clear();
        _iconComparePanel.RowStyles.Clear();
        _iconComparePanel.ColumnStyles.Clear();
        _iconComparePanel.RowCount = 3;
        _iconComparePanel.ColumnCount = 1 + IconBarOrder.Length;
        _iconComparePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        for (int i = 0; i < IconBarOrder.Length; i++)
            _iconComparePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / IconBarOrder.Length));
        _iconComparePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24)); // header
        _iconComparePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); // P0
        _iconComparePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); // P1

        // Row 0 — column headers.
        _iconComparePanel.Controls.Add(new Label
        {
            Text = "Player",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.DimGray,
            Padding = new Padding(6, 0, 0, 0),
            Margin = new Padding(0),
        }, 0, 0);
        for (int c = 0; c < IconBarOrder.Length; c++)
        {
            var icon = IconBarOrder[c];
            _iconComparePanel.Controls.Add(new Label
            {
                Text = ShortName(icon),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = ColorForIcon(icon),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Margin = new Padding(1),
            }, c + 1, 0);
        }

        // Rows 1–2 — one per player.
        for (int pi = 0; pi < _g.Players.Length; pi++)
        {
            var p = _g.Players[pi];
            bool isActive = pi == _g.ActivePlayer;

            _iconComparePanel.Controls.Add(new Label
            {
                Text = pi == 0 ? "You (P0)" : $"Opponent (P{pi})",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 9.5f, isActive ? FontStyle.Bold : FontStyle.Regular),
                ForeColor = isActive ? Color.MidnightBlue : Color.Black,
                Padding = new Padding(6, 0, 0, 0),
                Margin = new Padding(0),
            }, 0, pi + 1);

            for (int c = 0; c < IconBarOrder.Length; c++)
            {
                var icon = IconBarOrder[c];
                int n = IconCounter.Count(p, icon, _cards);
                _iconComparePanel.Controls.Add(new Label
                {
                    Text = n.ToString(),
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    // Faintly tint each count cell with its icon color
                    // so the row is still readable left-to-right even
                    // when the headers scroll out of view.
                    BackColor = n == 0 ? Color.WhiteSmoke : ControlPaint.LightLight(ColorForIcon(icon)),
                    ForeColor = n == 0 ? Color.Gray : Color.Black,
                    Font = new Font("Segoe UI", 10f, n > 0 ? FontStyle.Bold : FontStyle.Regular),
                    Margin = new Padding(1),
                }, c + 1, pi + 1);
            }
        }

        _iconComparePanel.ResumeLayout(performLayout: true);
    }

    // ---------- Achievements Remaining ----------

    /// <summary>
    /// Lists the unclaimed age achievement tiles (1–9) and the five
    /// special achievements still on the table. Each entry is a small
    /// chip — claimed tiles don't appear (they'd live under a player's
    /// <c>AgeAchievements</c> / <c>SpecialAchievements</c> instead).
    /// </summary>
    private void RefreshAchievementsRemaining()
    {
        _achRemainingPanel.SuspendLayout();
        for (int i = _achRemainingPanel.Controls.Count - 1; i >= 0; i--)
            _achRemainingPanel.Controls[i].Dispose();
        _achRemainingPanel.Controls.Clear();
        _achRemainingPanel.RowStyles.Clear();
        _achRemainingPanel.RowCount = 0;

        void AddRow(Control c, int h)
        {
            int r = _achRemainingPanel.RowCount++;
            _achRemainingPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, h));
            c.Dock = DockStyle.Fill;
            _achRemainingPanel.Controls.Add(c, 0, r);
        }

        AddRow(new Label
        {
            Text = $"{_g.AvailableAgeAchievements.Count} age · {_g.AvailableSpecialAchievements.Count} special",
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.DimGray,
            Padding = new Padding(4, 0, 0, 0),
        }, 22);

        // Age tiles — one row of small gray chips for the ages still
        // on offer. Claimed ages simply don't appear.
        var ageRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(2),
            Margin = new Padding(0),
        };
        foreach (var age in _g.AvailableAgeAchievements.OrderBy(a => a))
            ageRow.Controls.Add(BuildAchievementChip($"Age {age}", Color.SlateGray));
        if (_g.AvailableAgeAchievements.Count == 0)
            ageRow.Controls.Add(new Label
            {
                Text = "(all claimed)",
                AutoSize = true,
                ForeColor = Color.Silver,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                Margin = new Padding(4, 2, 0, 0),
            });
        AddRow(ageRow, 58);

        // Special tiles — claimed by the "hardest" conditions (e.g.
        // Universe = 5+ of every icon). Tint darker so they stand out
        // from the age tiles.
        var specRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(2),
            Margin = new Padding(0),
        };
        foreach (var name in _g.AvailableSpecialAchievements.OrderBy(n => n))
            specRow.Controls.Add(BuildAchievementChip(name, Color.DarkSlateBlue));
        if (_g.AvailableSpecialAchievements.Count == 0)
            specRow.Controls.Add(new Label
            {
                Text = "(all claimed)",
                AutoSize = true,
                ForeColor = Color.Silver,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                Margin = new Padding(4, 2, 0, 0),
            });
        AddRow(specRow, 60);

        _achRemainingPanel.ResumeLayout(performLayout: true);
    }

    private static Label BuildAchievementChip(string text, Color bg) => new()
    {
        Text = text,
        AutoSize = false,
        Width = 70, Height = 22,
        TextAlign = ContentAlignment.MiddleCenter,
        BackColor = bg,
        ForeColor = Color.White,
        Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
        Margin = new Padding(2),
    };

    // ---------- Cards Remaining (per age deck) ----------

    /// <summary>
    /// Ten rows in a compact two-column table showing how many cards
    /// are still in each age deck (1–10). The engine keeps these in
    /// <see cref="GameState.Decks"/>; an exhausted pile ages the
    /// Draw action (you skip to the next age that has cards).
    /// </summary>
    private void RefreshCardsRemaining()
    {
        _cardsRemainingPanel.SuspendLayout();
        for (int i = _cardsRemainingPanel.Controls.Count - 1; i >= 0; i--)
            _cardsRemainingPanel.Controls[i].Dispose();
        _cardsRemainingPanel.Controls.Clear();
        _cardsRemainingPanel.RowStyles.Clear();
        _cardsRemainingPanel.ColumnStyles.Clear();
        _cardsRemainingPanel.RowCount = 5;
        _cardsRemainingPanel.ColumnCount = 4;
        for (int i = 0; i < 4; i++)
            _cardsRemainingPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        for (int r = 0; r < 5; r++)
            _cardsRemainingPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 20));

        // Ages 1..10 packed into a 2-ages-per-row layout (age + count).
        for (int age = 1; age <= 10; age++)
        {
            int count = _g.Decks[age].Count;
            int rowIdx = (age - 1) / 2;
            int colBase = ((age - 1) % 2) * 2;

            var ageLbl = new Label
            {
                Text = $"Age {age}:",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = count == 0 ? Color.Silver : Color.Black,
                Padding = new Padding(4, 0, 0, 0),
            };
            var countLbl = new Label
            {
                Text = count.ToString(),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = count == 0 ? Color.Silver : Color.Black,
                Padding = new Padding(0, 0, 6, 0),
            };
            _cardsRemainingPanel.Controls.Add(ageLbl, colBase, rowIdx);
            _cardsRemainingPanel.Controls.Add(countLbl, colBase + 1, rowIdx);
        }

        _cardsRemainingPanel.ResumeLayout(performLayout: true);
    }

    /// <summary>
    /// Full-detail board for the viewing player: summary (score,
    /// histogram, achievements), then the five color-pile tiles.
    /// Icons row is NOT drawn here — both seats' icons live in the
    /// shared <see cref="_iconComparePanel"/> above this row, which
    /// reads better than hiding them inside each board.
    /// </summary>
    private void RenderYourBoardInto(TableLayoutPanel target, PlayerState p)
    {
        target.SuspendLayout();
        // Dispose so native handles don't leak between refreshes. Iterate
        // backwards because Dispose removes the control from its parent.
        for (int i = target.Controls.Count - 1; i >= 0; i--)
            target.Controls[i].Dispose();
        target.Controls.Clear();
        target.RowStyles.Clear();
        target.ColumnStyles.Clear();
        target.RowCount = 0;
        target.ColumnCount = 1;
        target.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddBoardRow(target, BuildSummaryRow(p, viewerIsOwner: true), SizeType.Absolute, 32);
        AddBoardRow(target, BuildPilesRow(p), SizeType.Percent, 0);

        target.ResumeLayout(performLayout: true);
    }

    /// <summary>
    /// Compact opponent view: summary line, then a single horizontal
    /// row of five mini-pile tiles. Each mini-tile shows the color
    /// banner, the top card title, and a "+N" indicator for covered
    /// cards — enough to see what the opponent might dogma without
    /// consuming half the play area.
    /// </summary>
    private void RenderOpponentCompactInto(TableLayoutPanel target, PlayerState p)
    {
        target.SuspendLayout();
        for (int i = target.Controls.Count - 1; i >= 0; i--)
            target.Controls[i].Dispose();
        target.Controls.Clear();
        target.RowStyles.Clear();
        target.ColumnStyles.Clear();
        target.RowCount = 0;
        target.ColumnCount = 1;
        target.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddBoardRow(target, BuildSummaryRow(p, viewerIsOwner: false), SizeType.Absolute, 32);
        AddBoardRow(target, BuildCompactPilesRow(p), SizeType.Percent, 0);

        target.ResumeLayout(performLayout: true);
    }

    /// <summary>
    /// Rebuild the hand row's tiles from the player's current hand.
    /// One clickable tile per card. Hand cards are ordered as the
    /// engine returns them (roughly draw order); we don't sort so the
    /// user's hand doesn't reshuffle under them as play progresses.
    /// </summary>
    private void RenderHandInto(FlowLayoutPanel target, PlayerState p)
    {
        target.SuspendLayout();
        for (int i = target.Controls.Count - 1; i >= 0; i--)
            target.Controls[i].Dispose();
        target.Controls.Clear();
        if (p.Hand.Count == 0)
        {
            target.Controls.Add(new Label
            {
                Text = "(empty)",
                AutoSize = true,
                ForeColor = Color.Silver,
                Font = new Font("Segoe UI", 9f, FontStyle.Italic),
                Margin = new Padding(6, 8, 0, 0),
            });
        }
        else
        {
            foreach (var id in p.Hand)
                target.Controls.Add(BuildCardTile(_cards[id], isTop: false));
        }
        target.ResumeLayout(performLayout: true);
    }

    /// <summary>
    /// Five compact mini-tiles, one per color, rendered in a single
    /// horizontal row. Used for the opponent view where the full
    /// pile detail would eat too much vertical space.
    /// </summary>
    private Control BuildCompactPilesRow(PlayerState p)
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 1,
            Padding = new Padding(0),
            Margin = new Padding(0),
        };
        for (int i = 0; i < 5; i++)
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        int col = 0;
        foreach (CardColor c in Enum.GetValues<CardColor>())
            row.Controls.Add(BuildCompactPile(p.Stack(c), c), col++, 0);
        return row;
    }

    /// <summary>
    /// Single-color mini-tile: banner + top card + "+N covered" hint.
    /// The whole tile's top-card button is clickable to jump the
    /// viewer, so the opponent's cards remain inspectable.
    /// </summary>
    private Control BuildCompactPile(ColorStack stack, CardColor color)
    {
        var bg = ColorFor(color);
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(2),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White,
        };

        // Body first so the banner docks above it (see note in
        // BuildColorPile about docking order).
        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(2),
            Margin = new Padding(0),
            BackColor = Color.White,
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        // VB6-style compact opponent tile: tiny top-card summary
        // (header + icons only at 56px), a single-line covered count,
        // then whitespace filler so short opponent boards don't look
        // stretched.
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        if (stack.IsEmpty)
        {
            var emptyLbl = new Label
            {
                Text = "(empty)",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.Silver,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
            };
            body.Controls.Add(emptyLbl, 0, 0);
            // Span all three rows so the label centers vertically in
            // the tile — without this it stays pinned to the fixed
            // 56px top row and the rest of the tile looks blank.
            body.SetRowSpan(emptyLbl, 3);
        }
        else
        {
            var topCard = _cards[stack.Top];
            var topCtl = new CardControl
            {
                Card = topCard,
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                Tag = topCard.Id,
            };
            topCtl.Click += (_, _) => ShowCardInViewer(topCard.Id);
            body.Controls.Add(topCtl, 0, 0);

            body.Controls.Add(new Label
            {
                Text = stack.Cards.Count > 1 ? $"+{stack.Cards.Count - 1} covered" : "",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.DimGray,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            }, 0, 1);
        }
        panel.Controls.Add(body);

        panel.Controls.Add(new Label
        {
            Text = BuildPileHeaderText(color, stack),
            Dock = DockStyle.Top,
            Height = 20,
            BackColor = bg,
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
        });

        return panel;
    }

    private static void AddBoardRow(TableLayoutPanel target, Control content, SizeType sizeType, int size)
    {
        int row = target.RowCount;
        target.RowCount = row + 1;
        target.RowStyles.Add(sizeType == SizeType.Percent
            ? new RowStyle(SizeType.Percent, size == 0 ? 100 : size)
            : new RowStyle(sizeType, size));
        content.Dock = DockStyle.Fill;
        content.Margin = new Padding(0, 2, 0, 2);
        target.Controls.Add(content, 0, row);
    }

    // ---------- Icon totals row ----------

    /// <summary>
    /// Order matches how the VB6 original displayed icons along the
    /// resource bar — castles first (military / succeed), then crowns
    /// (wealth), leaf (culture), bulb (science), factory (industry),
    /// clock (progress).
    /// </summary>
    private static readonly CardIcon[] IconBarOrder =
    {
        CardIcon.Castle, CardIcon.Crown, CardIcon.Leaf, CardIcon.Lightbulb, CardIcon.Factory, CardIcon.Clock,
    };

    private static string ShortName(CardIcon i) => i switch
    {
        CardIcon.Lightbulb => "Bulb",
        _ => i.ToString(),
    };

    private static Color ColorForIcon(CardIcon i) => i switch
    {
        CardIcon.Castle    => Color.SaddleBrown,
        CardIcon.Crown     => Color.Goldenrod,
        CardIcon.Leaf      => Color.ForestGreen,
        CardIcon.Lightbulb => Color.DarkOrange,
        CardIcon.Factory   => Color.DimGray,
        CardIcon.Clock     => Color.SteelBlue,
        _                  => Color.Gray,
    };

    // ---------- Summary row ----------

    private Control BuildSummaryRow(PlayerState p, bool viewerIsOwner)
    {
        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(2, 0, 2, 0),
            Margin = new Padding(0),
        };

        // Score text tag — full "X pts (Y cards)" so the histogram
        // right beside it is read as the breakdown.
        flow.Controls.Add(new Label
        {
            Text = $"Score: {p.Score(_cards)} pts ({p.ScorePile.Count})",
            AutoSize = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Margin = new Padding(0, 6, 6, 0),
        });
        flow.Controls.Add(BuildScoreHistogram(p));

        flow.Controls.Add(new Label
        {
            Text = viewerIsOwner ? $"Hand: {p.Hand.Count}" : $"Hand: {p.Hand.Count} (hidden)",
            AutoSize = true,
            Font = new Font("Segoe UI", 9f),
            Margin = new Padding(16, 6, 6, 0),
        });
        flow.Controls.Add(new Label
        {
            Text = $"Achievements: {FormatAchievements(p)}",
            AutoSize = true,
            Font = new Font("Segoe UI", 9f),
            Margin = new Padding(16, 6, 6, 0),
        });
        return flow;
    }

    /// <summary>
    /// Horizontal strip of ten small cells (ages 1–10), each showing
    /// how many score-pile cards of that age the player has. Mirrors
    /// the VB6 "Your Score Pile" histogram. Empty cells dim; filled
    /// cells grow slightly bolder in color with higher counts so at a
    /// glance you see where the points are concentrated.
    /// </summary>
    private Control BuildScoreHistogram(PlayerState p)
    {
        var counts = new int[11]; // indices 1..10
        foreach (var id in p.ScorePile)
        {
            int age = _cards[id].Age;
            if (age >= 1 && age <= 10) counts[age]++;
        }

        var row = new TableLayoutPanel
        {
            ColumnCount = 10,
            RowCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 2, 0, 0),
            Padding = new Padding(0),
        };
        for (int i = 0; i < 10; i++)
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30));
        row.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));

        var tooltip = new ToolTip();
        for (int age = 1; age <= 10; age++)
        {
            int count = counts[age];
            var cell = new Label
            {
                Text = count > 0 ? $"{age}·{count}" : age.ToString(),
                Width = 30, Height = 26,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = count == 0 ? Color.WhiteSmoke
                    : count == 1 ? Color.LightSteelBlue
                    : count <= 3 ? Color.CornflowerBlue
                    : Color.SteelBlue,
                ForeColor = count == 0 ? Color.Silver : Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 8.25f, count > 0 ? FontStyle.Bold : FontStyle.Regular),
                Margin = new Padding(0),
            };
            tooltip.SetToolTip(cell, $"Age {age}: {count} card(s) — {count * age} pts");
            row.Controls.Add(cell, age - 1, 0);
        }
        return row;
    }

    // ---------- Color pile columns ----------

    private Control BuildPilesRow(PlayerState p)
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 1,
            Padding = new Padding(0),
            Margin = new Padding(0),
        };
        for (int i = 0; i < 5; i++)
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        int col = 0;
        foreach (CardColor c in Enum.GetValues<CardColor>())
            row.Controls.Add(BuildColorPile(p.Stack(c), c), col++, 0);
        return row;
    }

    /// <summary>
    /// A single color-pile column styled like the VB6 original:
    ///   • colored banner (pile color) with card-count + splay direction
    ///   • prominent, clickable "top card" tile — age + title in large
    ///     bold text, with its dogma icon called out in its own colored
    ///     chip directly below
    ///   • a compact list of covered cards (plain clickable links) so
    ///     the splayed contents are visible without expanding a scroll
    ///     area
    /// Empty piles show "(empty)" centered and skip the dogma / covered
    /// sections.
    /// </summary>
    private Control BuildColorPile(ColorStack stack, CardColor color)
    {
        var bg = ColorFor(color);
        var outer = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(2),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White,
        };

        // Build body first so the banner docks on top of it (WinForms
        // Z-order: first-added takes the remainder after later-docked
        // controls claim theirs).
        outer.Controls.Add(stack.IsEmpty ? BuildEmptyPileBody() : BuildPopulatedPileBody(stack, bg));

        // Banner: color name + card count + splay direction.
        outer.Controls.Add(new Label
        {
            Text = BuildPileHeaderText(color, stack),
            Dock = DockStyle.Top,
            Height = 22,
            BackColor = bg,
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
        });

        return outer;
    }

    private static Control BuildEmptyPileBody() => new Label
    {
        Text = "(empty)",
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleCenter,
        ForeColor = Color.DarkGray,
        Font = new Font("Segoe UI", 9f, FontStyle.Italic),
        BackColor = Color.White,
    };

    private Control BuildPopulatedPileBody(ColorStack stack, Color pileColor)
    {
        var topCard = _cards[stack.Top];

        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(3),
            Margin = new Padding(0),
            BackColor = Color.White,
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        // Keep the top-card render compact (VB6 density) — 78px is
        // just below the CardRenderer's body-collapse threshold so the
        // tile shows header + icon strip only. The covered list
        // expands to fill whatever vertical space is left after the
        // top card, which scales naturally with window size.
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 78)); // top card (compact mini-tile)
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // covered cards list

        var topCtl = new CardControl
        {
            Card = topCard,
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            Tag = topCard.Id,
        };
        topCtl.Click += (_, _) => ShowCardInViewer(topCard.Id);
        body.Controls.Add(topCtl, 0, 0);

        // Row 1 — covered cards (below the top). Compact link-labels
        // so a splayed pile shows its full contents at a glance.
        var covered = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Color.White,
            Padding = new Padding(0),
            Margin = new Padding(0),
        };
        if (stack.Cards.Count == 1)
        {
            covered.Controls.Add(new Label
            {
                Text = "(no covered cards)",
                AutoSize = true,
                ForeColor = Color.Silver,
                Font = new Font("Segoe UI", 8f, FontStyle.Italic),
                Margin = new Padding(2, 2, 0, 0),
            });
        }
        else
        {
            for (int i = 1; i < stack.Cards.Count; i++)
            {
                var card = _cards[stack.Cards[i]];
                int captureId = card.Id;
                var link = new LinkLabel
                {
                    Text = $"  · [{card.Age}] {card.Title}",
                    AutoSize = true,
                    LinkColor = pileColor,
                    VisitedLinkColor = pileColor,
                    ActiveLinkColor = pileColor,
                    Font = new Font("Segoe UI", 8.5f),
                    Margin = new Padding(0, 1, 0, 1),
                };
                link.LinkClicked += (_, _) => ShowCardInViewer(captureId);
                covered.Controls.Add(link);
            }
        }
        body.Controls.Add(covered, 0, 1);

        return body;
    }

    private static string BuildPileHeaderText(CardColor color, ColorStack stack)
    {
        if (stack.IsEmpty) return color.ToString();
        if (stack.Splay == Splay.None) return $"{color} ({stack.Count})";
        return $"{color} ({stack.Count}) · splay {stack.Splay.ToString().ToLower()}";
    }

    // ---------- Card tile helper ----------

    /// <summary>
    /// One hand-row tile — a dynamically-rendered mini card. At the
    /// hand's 90px row height the tile lands in the small-card regime
    /// where the renderer shows just the header + icon strip, which is
    /// enough to identify the card without consuming the whole row.
    /// Clicking the tile jumps the card viewer.
    /// </summary>
    private CardControl BuildCardTile(Card c, bool isTop = false)
    {
        var ctl = new CardControl
        {
            Card = c,
            Width = 160,
            Height = 78,
            Margin = new Padding(3),
            Tag = c.Id,
        };
        ctl.Click += (_, _) => ShowCardInViewer(c.Id);
        return ctl;
    }

    /// <summary>Set the card-viewer picker to show a specific card.</summary>
    private void ShowCardInViewer(int cardId)
    {
        for (int i = 0; i < _cardViewerPicker.Items.Count; i++)
            if (_cardViewerPicker.Items[i] is CardEntry e && e.Card.Id == cardId)
            {
                _cardViewerPicker.SelectedIndex = i;
                return;
            }
    }

    /// <summary>
    /// Factory for a per-player board panel. The internals are rebuilt
    /// from scratch on every <see cref="RenderBoardInto"/>, so this just
    /// returns a bare shell.
    /// </summary>
    private static TableLayoutPanel NewBoardPanel()
    {
        var tlp = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 0,
        };
        tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return tlp;
    }

    /// <summary>Display color for a card's color — dark enough to read on white.</summary>
    private static Color ColorFor(CardColor c) => c switch
    {
        CardColor.Red => Color.Firebrick,
        CardColor.Yellow => Color.DarkGoldenrod,
        CardColor.Blue => Color.RoyalBlue,
        CardColor.Green => Color.ForestGreen,
        CardColor.Purple => Color.Purple,
        _ => Color.DimGray,
    };

    private static string FormatAchievements(PlayerState p)
    {
        var parts = new List<string>();
        if (p.AgeAchievements.Count > 0) parts.Add(string.Join(",", p.AgeAchievements.Select(a => $"age{a}")));
        parts.AddRange(p.SpecialAchievements);
        return parts.Count == 0 ? "(none)" : string.Join(" ", parts);
    }

    private void PopulateActionControls(IReadOnlyList<PlayerAction> legal, PlayerState? self)
    {
        DisableAllActions();
        if (self == null || legal.Count == 0) return;

        // Achieve
        var achieves = legal.OfType<AchieveAction>().Select(a => new AchieveChoice(a.Age)).ToArray();
        _achieveCombo.Items.Clear();
        foreach (var a in achieves) _achieveCombo.Items.Add(a);
        if (achieves.Length > 0) { _achieveCombo.SelectedIndex = 0; _achieveCombo.Enabled = true; _achieveBtn.Enabled = true; }

        // Draw
        if (legal.Any(a => a is DrawAction)) _drawBtn.Enabled = true;

        // Meld
        var melds = legal.OfType<MeldAction>()
            .Select(m => new MeldChoice(m.CardId, _cards[m.CardId].Title, _cards[m.CardId].Age, _cards[m.CardId].Color))
            .ToArray();
        _meldCombo.Items.Clear();
        foreach (var m in melds) _meldCombo.Items.Add(m);
        if (melds.Length > 0) { _meldCombo.SelectedIndex = 0; _meldCombo.Enabled = true; _meldBtn.Enabled = true; }

        // Dogma
        var dogmas = legal.OfType<DogmaAction>()
            .Select(d => new DogmaChoice(d.Color, self.Stack(d.Color).IsEmpty ? "" : _cards[self.Stack(d.Color).Top].Title))
            .ToArray();
        _dogmaCombo.Items.Clear();
        foreach (var d in dogmas) _dogmaCombo.Items.Add(d);
        if (dogmas.Length > 0) { _dogmaCombo.SelectedIndex = 0; _dogmaCombo.Enabled = true; _dogmaBtn.Enabled = true; }
    }

    private void DisableAllActions()
    {
        _achieveCombo.Enabled = _meldCombo.Enabled = _dogmaCombo.Enabled = false;
        _achieveBtn.Enabled = _drawBtn.Enabled = _meldBtn.Enabled = _dogmaBtn.Enabled = false;
    }

    private void AppendLog(string line)
    {
        if (InvokeRequired) { BeginInvoke(() => AppendLog(line)); return; }
        _logBox.AppendText(line + Environment.NewLine);
        _logBox.SelectionStart = _logBox.TextLength;
        _logBox.ScrollToCaret();
    }

    private void CompleteAction(PlayerAction action)
    {
        var tcs = _pendingAction;
        _pendingAction = null;
        if (tcs == null) return;
        DisableAllActions();
        AppendLog($"P{_g.ActivePlayer} chose {DescribeAction(action)}.");
        tcs.TrySetResult(action);
    }

    private string DescribeAction(PlayerAction a) => a switch
    {
        AchieveAction ach => $"Achieve age {ach.Age}",
        DrawAction => "Draw",
        MeldAction m => $"Meld {_cards[m.CardId].Title}",
        DogmaAction d => $"Dogma ({d.Color})",
        _ => a.ToString() ?? "?",
    };

    // ---------- IUserPromptSink ----------

    public int PromptInitialMeld(GameState g, PlayerState self)
    {
        var token = _shutdown.Token;
        var result = UiInvokeFunc(() =>
        {
            using var dlg = new HandCardPromptForm(
                title: $"P{self.Index}: Choose your starting meld",
                prompt: "Pick one card from your hand to meld.",
                cards: _cards,
                eligibleIds: self.Hand,
                allowNone: false,
                cancellation: token);
            dlg.ShowDialog(this);
            return dlg.ChosenCardId ?? self.Hand[0];
        });
        // If a New Game canceled us while the dialog was up, discard
        // the (default) result and bail out of the worker.
        token.ThrowIfCancellationRequested();
        return result;
    }

    public PlayerAction PromptAction(GameState g, PlayerState self, IReadOnlyList<PlayerAction> legal)
    {
        if (_shutdown.IsCancellationRequested) throw new OperationCanceledException();
        if (legal.Count == 0) throw new InvalidOperationException("No legal actions.");

        var tcs = new TaskCompletionSource<PlayerAction>();
        _pendingAction = tcs;
        UiInvoke(() => RenderState(legal, self));
        return tcs.Task.GetAwaiter().GetResult();
    }

    public int? PromptHandCard(GameState g, PlayerState self, SelectHandCardRequest req)
    {
        var token = _shutdown.Token;
        var result = UiInvokeFunc(() =>
        {
            using var dlg = new HandCardPromptForm(
                title: $"P{self.Index}: {req.Prompt}",
                prompt: req.Prompt,
                cards: _cards,
                eligibleIds: req.EligibleCardIds,
                allowNone: req.AllowNone,
                cancellation: token);
            dlg.ShowDialog(this);
            return dlg.ChosenCardId;
        });
        token.ThrowIfCancellationRequested();
        return result;
    }

    public IReadOnlyList<int> PromptHandCardSubset(GameState g, PlayerState self, SelectHandCardSubsetRequest req)
    {
        var token = _shutdown.Token;
        var result = UiInvokeFunc<IReadOnlyList<int>>(() =>
        {
            using var dlg = new SubsetPromptForm(
                title: $"P{self.Index}: {req.Prompt}",
                prompt: req.Prompt,
                cards: _cards,
                eligibleIds: req.EligibleCardIds,
                min: req.MinCount,
                max: req.MaxCount,
                cancellation: token);
            dlg.ShowDialog(this);
            return dlg.Chosen;
        });
        token.ThrowIfCancellationRequested();
        return result;
    }

    public bool PromptYesNo(GameState g, PlayerState self, YesNoChoiceRequest req)
    {
        // MessageBox.Show has no token hook — if the user is mid-Y/N when
        // a New Game fires, they'll see one extra prompt. The post-show
        // cancellation check discards their answer so nothing it pushes
        // into the engine will matter. Replace with a custom form if
        // this ever gets annoying in practice.
        var token = _shutdown.Token;
        var result = UiInvokeFunc(() =>
        {
            var r = MessageBox.Show(this, req.Prompt, $"P{self.Index}: Yes/No",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            return r == DialogResult.Yes;
        });
        token.ThrowIfCancellationRequested();
        return result;
    }

    public int? PromptScoreCard(GameState g, PlayerState self, SelectScoreCardRequest req)
    {
        // WinForms path is being retired in favor of the WPF UI (which has
        // a proper ScoreCardPanel). Age-2 cards that use this prompt
        // (Mapmaking) aren't reachable from the WinForms front-end.
        throw new NotImplementedException(
            "PromptScoreCard not implemented in the WinForms UI (use WPF).");
    }

    public IReadOnlyList<int> PromptScoreCardSubset(GameState g, PlayerState self, SelectScoreCardSubsetRequest req)
    {
        throw new NotImplementedException(
            "PromptScoreCardSubset not implemented in the WinForms UI (use WPF).");
    }

    public IReadOnlyList<int> PromptCardOrder(GameState g, PlayerState self, SelectCardOrderRequest req)
    {
        throw new NotImplementedException(
            "PromptCardOrder not implemented in the WinForms UI (use WPF).");
    }

    public IReadOnlyList<int> PromptStackOrder(GameState g, PlayerState self, SelectStackOrderRequest req)
    {
        throw new NotImplementedException("PromptStackOrder not implemented in the WinForms UI (use WPF).");
    }

    public int? PromptValue(GameState g, PlayerState self, SelectValueRequest req)
    {
        throw new NotImplementedException("PromptValue not implemented in the WinForms UI (use WPF).");
    }

    public CardColor? PromptColor(GameState g, PlayerState self, SelectColorRequest req)
    {
        var token = _shutdown.Token;
        var result = UiInvokeFunc(() =>
        {
            using var dlg = new ColorPromptForm(
                title: $"P{self.Index}: {req.Prompt}",
                prompt: req.Prompt,
                eligible: req.EligibleColors,
                cancellation: token);
            dlg.ShowDialog(this);
            return dlg.ChosenColor;
        });
        token.ThrowIfCancellationRequested();
        return result;
    }

    // ---------- Thread marshalling helpers ----------

    /// <summary>
    /// Synchronous fire-and-return Invoke that swallows ObjectDisposed
    /// (form closing) and converts to OperationCanceledException so the
    /// worker can bail cleanly.
    /// </summary>
    private void UiInvoke(Action a)
    {
        if (IsDisposed) throw new OperationCanceledException();
        try { Invoke(a); }
        catch (ObjectDisposedException) { throw new OperationCanceledException(); }
        catch (InvalidOperationException) when (IsDisposed) { throw new OperationCanceledException(); }
    }

    private T UiInvokeFunc<T>(Func<T> f)
    {
        if (IsDisposed) throw new OperationCanceledException();
        try { return (T)Invoke(f)!; }
        catch (ObjectDisposedException) { throw new OperationCanceledException(); }
        catch (InvalidOperationException) when (IsDisposed) { throw new OperationCanceledException(); }
    }
}
