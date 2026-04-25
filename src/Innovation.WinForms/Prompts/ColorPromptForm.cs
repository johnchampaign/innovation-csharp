using Innovation.Core;

namespace Innovation.WinForms.Prompts;

/// <summary>
/// Modal for "choose one of these colors". One button per eligible
/// color; cancel returns null (the engine handles it when AllowNone is
/// implicit in the prompt).
/// </summary>
internal sealed class ColorPromptForm : Form
{
    public CardColor? ChosenColor { get; private set; }

    public ColorPromptForm(
        string title, string prompt, IReadOnlyList<CardColor> eligible,
        CancellationToken cancellation = default)
    {
        Text = title;
        ClientSize = new Size(340, 160);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;

        var header = new Label { Text = prompt, Dock = DockStyle.Top, Height = 40, Padding = new Padding(8) };
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        foreach (var c in eligible)
        {
            var btn = new Button { Text = c.ToString(), Width = 80, Height = 32, BackColor = ColorFor(c), ForeColor = Color.White };
            btn.Click += (_, _) => { ChosenColor = c; DialogResult = DialogResult.OK; };
            flow.Controls.Add(btn);
        }

        Controls.Add(flow);
        Controls.Add(header);

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

    private static Color ColorFor(CardColor c) => c switch
    {
        CardColor.Red => Color.Firebrick,
        CardColor.Yellow => Color.DarkGoldenrod,
        CardColor.Blue => Color.RoyalBlue,
        CardColor.Green => Color.ForestGreen,
        CardColor.Purple => Color.Purple,
        _ => Color.DimGray,
    };
}
