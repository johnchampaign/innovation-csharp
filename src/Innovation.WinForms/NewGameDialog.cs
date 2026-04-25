namespace Innovation.WinForms;

/// <summary>
/// Pre-game modal. Pick a controller kind for each seat and a RNG seed.
/// Intentionally plain — the richer "AI Omniscience" slider and the
/// per-player preference toggles from the VB6 original are out of
/// scope for Phase 6.1.
/// </summary>
public sealed class NewGameDialog : Form
{
    private readonly ComboBox _p0Combo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _p1Combo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _seedBox = new() { Minimum = 0, Maximum = int.MaxValue };
    private readonly Button _okButton = new() { Text = "Start", DialogResult = DialogResult.OK };
    private readonly Button _cancelButton = new() { Text = "Cancel", DialogResult = DialogResult.Cancel };

    public NewGameConfig Config { get; private set; } = new();

    public NewGameDialog() : this(initial: null) { }

    /// <summary>
    /// Pre-seed the dialog with an existing config (handy for mid-session
    /// New Game, where the user usually wants to keep most of their
    /// previous choices). Pass null for first-launch defaults.
    /// </summary>
    public NewGameDialog(NewGameConfig? initial)
    {
        Text = "New Game";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(320, 180);
        AcceptButton = _okButton;
        CancelButton = _cancelButton;

        foreach (var kind in Enum.GetValues<SeatKind>())
        {
            _p0Combo.Items.Add(kind);
            _p1Combo.Items.Add(kind);
        }
        _p0Combo.SelectedItem = initial?.Player0 ?? SeatKind.Human;
        _p1Combo.SelectedItem = initial?.Player1 ?? SeatKind.Greedy;
        // Seed: if reopening, offer a fresh seed so "Start" doesn't
        // quietly replay the exact same deck. User can dial it back.
        _seedBox.Value = Math.Abs(Environment.TickCount) % 1_000_000;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(12),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 4; i++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(new Label { Text = "Player 1 (P0):", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 0);
        layout.Controls.Add(_p0Combo, 1, 0);
        layout.Controls.Add(new Label { Text = "Player 2 (P1):", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 1);
        layout.Controls.Add(_p1Combo, 1, 1);
        layout.Controls.Add(new Label { Text = "RNG seed:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 2);
        layout.Controls.Add(_seedBox, 1, 2);

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
        };
        buttons.Controls.Add(_cancelButton);
        buttons.Controls.Add(_okButton);
        layout.Controls.Add(buttons, 1, 3);

        _p0Combo.Dock = DockStyle.Fill;
        _p1Combo.Dock = DockStyle.Fill;
        _seedBox.Dock = DockStyle.Fill;

        Controls.Add(layout);

        _okButton.Click += (_, _) =>
        {
            Config = new NewGameConfig
            {
                Player0 = (SeatKind)_p0Combo.SelectedItem!,
                Player1 = (SeatKind)_p1Combo.SelectedItem!,
                Seed = (int)_seedBox.Value,
            };
        };
    }
}
