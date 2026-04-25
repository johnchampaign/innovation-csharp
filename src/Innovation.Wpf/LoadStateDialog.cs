using System.Windows;
using System.Windows.Controls;

namespace Innovation.Wpf;

/// <summary>
/// Modal input box: paste a base64 state code emitted by
/// <see cref="Innovation.Core.GameStateCodec"/>. Seats are inherited from
/// the caller (load preserves human/AI seat assignments).
/// </summary>
public sealed class LoadStateDialog : Window
{
    private readonly TextBox _box = null!;

    public string? Code { get; private set; }

    public LoadStateDialog()
    {
        Title = "Load state";
        Width = 560;
        Height = 220;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;

        var panel = new DockPanel { Margin = new Thickness(10) };

        var label = new TextBlock
        {
            Text = "Paste a state code (base64):",
            Margin = new Thickness(0, 0, 0, 6),
        };
        DockPanel.SetDock(label, Dock.Top);
        panel.Children.Add(label);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 6, 0, 0),
        };
        DockPanel.SetDock(buttons, Dock.Bottom);
        var ok = new Button { Content = "Load", Width = 80, Margin = new Thickness(0, 0, 6, 0), IsDefault = true };
        var cancel = new Button { Content = "Cancel", Width = 80, IsCancel = true };
        ok.Click += (_, _) => { Code = _box.Text; DialogResult = true; };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);

        _box = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            FontSize = 11,
        };
        panel.Children.Add(_box);

        Content = panel;
    }
}
