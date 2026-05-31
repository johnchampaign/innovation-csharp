using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Innovation.Core;

namespace Innovation.Wpf;

/// <summary>
/// Lets the user upload the current play log to the project repo so the
/// developer can analyse games at scale (look for AI weaknesses, edge
/// cases that no single bug report would surface, common patterns in
/// human play). The log uploads as-is; no analysis happens in-app.
/// Uses the same build-time GitHub token as the bug-report dialog.
/// </summary>
public sealed class UploadLogDialog : Window
{
    private readonly TextBox _note;
    private readonly TextBlock _statusLine;
    private readonly Button _submitButton;

    public UploadLogDialog()
    {
        Title = "Upload game log";
        Width = 540;
        Height = 360;
        ResizeMode = ResizeMode.CanResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(0xF3, 0xEF, 0xD3));
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 13;

        var root = new DockPanel { Margin = new Thickness(12) };

        var header = new TextBlock
        {
            Text = "Upload the current game's log so the developer can study how "
                 + "Innovation gets played and improve the AI. The log captures "
                 + "every action in this game; your nickname appears in it. No "
                 + "GitHub account required.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10),
        };
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        // Bottom row: status + buttons.
        var bottom = new Grid();
        bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        DockPanel.SetDock(bottom, Dock.Bottom);

        _statusLine = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brushes.DimGray,
            TextWrapping = TextWrapping.Wrap,
        };
        Grid.SetColumn(_statusLine, 0);
        bottom.Children.Add(_statusLine);

        var btnRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        _submitButton = new Button
        {
            Content = "Upload",
            Width = 100,
            Margin = new Thickness(0, 0, 6, 0),
            Padding = new Thickness(8, 4, 8, 4),
            IsDefault = true,
        };
        _submitButton.Click += async (_, _) => await OnSubmitAsync();
        var cancel = new Button
        {
            Content = "Close",
            Width = 80,
            Padding = new Thickness(8, 4, 8, 4),
            IsCancel = true,
        };
        btnRow.Children.Add(_submitButton);
        btnRow.Children.Add(cancel);
        Grid.SetColumn(btnRow, 1);
        bottom.Children.Add(btnRow);
        root.Children.Add(bottom);

        var form = new Grid();
        for (int i = 0; i < 4; i++)
            form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        form.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star);

        var label = new TextBlock
        {
            Text = "Anything to say about this game? (optional)",
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 2),
        };
        Grid.SetRow(label, 0);
        form.Children.Add(label);

        _note = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(0, 0, 0, 10),
        };
        Grid.SetRow(_note, 1);
        form.Children.Add(_note);

        var footnote = new TextBlock
        {
            Text = "Uploaded logs are public in the project's repo. Don't include sensitive info.",
            FontStyle = FontStyles.Italic,
            Foreground = Brushes.DimGray,
            Margin = new Thickness(0, 4, 0, 8),
            TextWrapping = TextWrapping.Wrap,
        };
        Grid.SetRow(footnote, 2);
        form.Children.Add(footnote);

        root.Children.Add(form);
        Content = root;

        if (BugReportSubmitter.GetEmbeddedToken() is null)
        {
            _submitButton.IsEnabled = false;
            _statusLine.Text = "Upload unavailable in this build (no service token).";
            _statusLine.Foreground = Brushes.DarkRed;
        }
        else if (string.IsNullOrEmpty(GameLog.CurrentPath) || !File.Exists(GameLog.CurrentPath))
        {
            _submitButton.IsEnabled = false;
            _statusLine.Text = "No game log on disk yet.";
            _statusLine.Foreground = Brushes.DarkRed;
        }
    }

    private async Task OnSubmitAsync()
    {
        string? token = BugReportSubmitter.GetEmbeddedToken();
        if (token is null) return;

        _submitButton.IsEnabled = false;
        SetStatus("Uploading...", Brushes.DimGray);

        try
        {
            byte[] logBytes;
            try
            {
                logBytes = await File.ReadAllBytesAsync(GameLog.CurrentPath!).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                SetStatus($"Couldn't read the log file: {ex.Message}", Brushes.DarkRed);
                _submitButton.IsEnabled = true;
                return;
            }

            // Prepend any user note as a header comment block so it travels
            // with the log without requiring a separate file.
            var note = _note.Text?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(note))
            {
                var sb = new StringBuilder();
                sb.AppendLine($"# Player note: {note.Replace("\n", " ")}");
                sb.Append(Encoding.UTF8.GetString(logBytes));
                logBytes = Encoding.UTF8.GetBytes(sb.ToString());
            }

            string ts = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            string rand = Guid.NewGuid().ToString("N").Substring(0, 8);
            string version = GetVersion();
            string filename = $"{ts}-v{version}-{rand}.log";
            string commitMsg = string.IsNullOrWhiteSpace(note)
                ? $"Play log: {filename}"
                : $"Play log: {filename} — {Trim(note, 50)}";

            var submitter = new BugReportSubmitter(token);
            string? logUrl = await submitter.UploadGameLogAsync(
                logBytes, filename, commitMsg).ConfigureAwait(true);

            if (logUrl is null)
            {
                SetStatus("Upload failed. Try again or contact the developer.", Brushes.DarkRed);
                _submitButton.IsEnabled = true;
                return;
            }

            _statusLine.Text = "";
            MessageBox.Show(this,
                $"Log uploaded — thanks!\n\n{logUrl}",
                "Upload complete", MessageBoxButton.OK, MessageBoxImage.Information);
            try { Clipboard.SetText(logUrl); } catch { /* clipboard busy — fine */ }
            DialogResult = true;
        }
        catch (Exception ex)
        {
            SetStatus($"Upload failed: {ex.Message}", Brushes.DarkRed);
            _submitButton.IsEnabled = true;
        }
    }

    private void SetStatus(string text, Brush color)
    {
        _statusLine.Text = text;
        _statusLine.Foreground = color;
    }

    private static string Trim(string s, int max)
        => s.Length <= max ? s : s.Substring(0, max - 1) + "…";

    private static string GetVersion()
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (info?.InformationalVersion is { Length: > 0 } v)
            {
                // Strip git-suffix if present so filenames stay clean.
                int plus = v.IndexOf('+');
                return plus > 0 ? v.Substring(0, plus) : v;
            }
            return asm.GetName().Version?.ToString() ?? "unknown";
        }
        catch { return "unknown"; }
    }
}
