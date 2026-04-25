using Innovation.Core;

namespace Innovation.WinForms.Prompts;

/// <summary>
/// Modal for "pick between Min and Max cards from your hand" prompts —
/// Pottery / Masonry style. OK is disabled until the current checked
/// count falls in [min,max].
/// </summary>
internal sealed class SubsetPromptForm : Form
{
    private readonly CheckedListBox _list = new() { Dock = DockStyle.Fill, Font = new Font("Consolas", 10f), CheckOnClick = true };
    private readonly Button _ok = new() { Text = "OK", DialogResult = DialogResult.OK };
    private readonly Label _counter = new() { Dock = DockStyle.Bottom, Height = 22, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0) };

    private readonly int _min;
    private readonly int _max;
    private readonly List<int> _ids;

    public IReadOnlyList<int> Chosen { get; private set; } = Array.Empty<int>();

    public SubsetPromptForm(
        string title, string prompt, IReadOnlyList<Card> cards,
        IReadOnlyList<int> eligibleIds, int min, int max,
        CancellationToken cancellation = default)
    {
        Text = title;
        ClientSize = new Size(440, 400);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        AcceptButton = _ok;

        _min = min;
        _max = max;
        _ids = eligibleIds.ToList();
        foreach (var id in _ids)
            _list.Items.Add($"[{cards[id].Age}] {cards[id].Title} ({cards[id].Color})");

        var header = new Label
        {
            Text = $"{prompt}\n(Pick {(min == max ? $"{min}" : $"{min}-{max}")} card(s).)",
            Dock = DockStyle.Top, Height = 48, Padding = new Padding(8),
        };
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, Height = 40,
            FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6),
        };
        buttons.Controls.Add(_ok);

        Controls.Add(_list);
        Controls.Add(_counter);
        Controls.Add(buttons);
        Controls.Add(header);

        _list.ItemCheck += (_, _) =>
        {
            // ItemCheck fires *before* CheckedIndices updates — defer by one tick.
            BeginInvoke(UpdateCounter);
        };
        _ok.Click += (_, _) =>
        {
            Chosen = _list.CheckedIndices.Cast<int>().Select(i => _ids[i]).ToList();
        };

        UpdateCounter();
        WireCancellation(cancellation);
    }

    /// <summary>
    /// Auto-close if a mid-session New Game cancels the worker while
    /// this dialog is up. Caller is expected to re-check the token
    /// after <c>ShowDialog</c>.
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

    private void UpdateCounter()
    {
        int n = _list.CheckedIndices.Count;
        _counter.Text = $"Checked: {n} / need {_min}-{_max}";
        _ok.Enabled = n >= _min && n <= _max;
    }
}
