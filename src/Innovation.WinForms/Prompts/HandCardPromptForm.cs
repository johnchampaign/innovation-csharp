using Innovation.Core;

namespace Innovation.WinForms.Prompts;

/// <summary>
/// Modal for "pick one card" prompts — used both for the starting meld
/// (AllowNone=false) and for in-dogma choices like Agriculture
/// (AllowNone=true). Plain ListBox + OK/(Decline) buttons, no card art.
///
/// Accepts an optional <see cref="CancellationToken"/> so that a
/// mid-session New Game (or form close) can auto-dismiss the dialog —
/// the caller is expected to re-check the token after
/// <c>ShowDialog</c> returns and discard any default value.
/// </summary>
internal sealed class HandCardPromptForm : Form
{
    private readonly ListBox _list = new() { Dock = DockStyle.Fill, Font = new Font("Consolas", 10f) };
    private readonly Button _ok = new() { Text = "OK", DialogResult = DialogResult.OK };
    private readonly Button _decline = new() { Text = "Decline", DialogResult = DialogResult.Cancel };

    public int? ChosenCardId { get; private set; }

    public HandCardPromptForm(
        string title, string prompt, IReadOnlyList<Card> cards,
        IReadOnlyList<int> eligibleIds, bool allowNone,
        CancellationToken cancellation = default)
    {
        Text = title;
        ClientSize = new Size(420, 360);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        AcceptButton = _ok;

        foreach (var id in eligibleIds)
            _list.Items.Add(new Entry(id, $"[{cards[id].Age}] {cards[id].Title} ({cards[id].Color})"));
        if (_list.Items.Count > 0) _list.SelectedIndex = 0;

        var header = new Label { Text = prompt, Dock = DockStyle.Top, Height = 40, Padding = new Padding(8), AutoEllipsis = true };
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 40,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(6),
        };
        if (allowNone) buttons.Controls.Add(_decline);
        buttons.Controls.Add(_ok);

        var middle = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        middle.Controls.Add(_list);

        Controls.Add(middle);
        Controls.Add(buttons);
        Controls.Add(header);

        _ok.Click += (_, _) =>
        {
            if (_list.SelectedItem is Entry e) ChosenCardId = e.Id;
        };
        _decline.Click += (_, _) => ChosenCardId = null;

        // If the user X's out without AllowNone, force the top pick —
        // we never want to return a null the engine can't handle.
        FormClosing += (_, _) =>
        {
            if (DialogResult != DialogResult.OK)
            {
                if (allowNone) ChosenCardId = null;
                else if (_list.Items.Count > 0 && _list.Items[0] is Entry e) ChosenCardId = e.Id;
            }
        };

        WireCancellation(cancellation);
    }

    /// <summary>
    /// Tear down this dialog if the supplied token fires. Registering on
    /// <see cref="Control.Load"/> guarantees the window handle exists
    /// before we BeginInvoke; the token-already-cancelled case falls
    /// through to an immediate <see cref="Form.Close"/>.
    /// </summary>
    private void WireCancellation(CancellationToken cancellation)
    {
        if (!cancellation.CanBeCanceled) return;
        CancellationTokenRegistration reg = default;
        Load += (_, _) =>
        {
            reg = cancellation.Register(() =>
            {
                try
                {
                    if (IsHandleCreated && !IsDisposed)
                        BeginInvoke(() => { if (!IsDisposed) Close(); });
                }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
            });
            if (cancellation.IsCancellationRequested) Close();
        };
        FormClosed += (_, _) => reg.Dispose();
    }

    private sealed record Entry(int Id, string Display)
    {
        public override string ToString() => Display;
    }
}
