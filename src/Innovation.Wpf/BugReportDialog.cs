using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Innovation.Core;

namespace Innovation.Wpf;

/// <summary>
/// Modal dialog for filing a bug report directly to GitHub via a service
/// token. The user describes what happened; on Submit, the dialog uploads
/// the screenshot to the repo, files an issue with the description + log
/// snippet + state code + screenshot URL, and shows the resulting issue
/// link. Users don't need GitHub accounts.
///
/// Token comes from <see cref="BugReportSubmitter.GetEmbeddedToken"/>;
/// builds without a token get a "submission unavailable" message and the
/// Submit button is disabled.
/// </summary>
public sealed class BugReportDialog : Window
{
    private const int RecentLogLines = 40;

    private readonly TextBox _whatHappened;
    private readonly TextBox _whatExpected;
    private readonly CheckBox _includeLog;
    private readonly CheckBox _includeState;
    private readonly CheckBox _includeScreenshot;
    private readonly TextBlock _statusLine;
    private readonly Button _submitButton;
    private readonly BitmapSource? _screenshot;
    private readonly GameState _state;

    public BugReportDialog(GameState state, BitmapSource? screenshot = null)
    {
        _state = state;
        _screenshot = screenshot;

        Title = "Report a bug";
        Width = 560;
        Height = 560;
        ResizeMode = ResizeMode.CanResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(0xF3, 0xEF, 0xD3));
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 13;

        var root = new DockPanel { Margin = new Thickness(12) };

        var header = new TextBlock
        {
            Text = "Describe the bug. On Submit, the report is filed automatically — "
                 + "you don't need a GitHub account.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10),
        };
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        // Bottom row: status line + buttons.
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
            Content = "Submit",
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
        for (int i = 0; i < 9; i++)
            form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        form.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star);
        form.RowDefinitions[3].Height = new GridLength(1, GridUnitType.Star);

        AddRow(form, 0, "What happened? (required)");
        _whatHappened = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(0, 0, 0, 10),
        };
        Grid.SetRow(_whatHappened, 1);
        form.Children.Add(_whatHappened);

        AddRow(form, 2, "What did you expect to happen? (optional)");
        _whatExpected = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(0, 0, 0, 10),
        };
        Grid.SetRow(_whatExpected, 3);
        form.Children.Add(_whatExpected);

        _includeState = new CheckBox
        {
            Content = "Include the most recent turn-boundary state code (recommended)",
            IsChecked = true,
            Margin = new Thickness(0, 0, 0, 4),
        };
        Grid.SetRow(_includeState, 4);
        form.Children.Add(_includeState);

        _includeLog = new CheckBox
        {
            Content = $"Include the last ~{RecentLogLines} log lines (recommended)",
            IsChecked = true,
            Margin = new Thickness(0, 0, 0, 4),
        };
        Grid.SetRow(_includeLog, 5);
        form.Children.Add(_includeLog);

        _includeScreenshot = new CheckBox
        {
            Content = "Attach a screenshot (auto-uploaded; no action needed from you)",
            IsChecked = _screenshot != null,
            IsEnabled = _screenshot != null,
            Margin = new Thickness(0, 0, 0, 4),
            ToolTip = _screenshot != null
                ? "The screenshot was captured the moment you opened this dialog."
                : "No screenshot captured (rendering failed in this build).",
        };
        Grid.SetRow(_includeScreenshot, 6);
        form.Children.Add(_includeScreenshot);

        var footnote = new TextBlock
        {
            Text = "Reports are filed publicly on GitHub. Your nickname appears in the log "
                 + "and on the screenshot. Don't include sensitive info.",
            FontStyle = FontStyles.Italic,
            Foreground = Brushes.DimGray,
            Margin = new Thickness(0, 4, 0, 8),
            TextWrapping = TextWrapping.Wrap,
        };
        Grid.SetRow(footnote, 7);
        form.Children.Add(footnote);

        root.Children.Add(form);
        Content = root;

        // If no token is baked in, lock the form down with an explanation.
        if (BugReportSubmitter.GetEmbeddedToken() is null)
        {
            _submitButton.IsEnabled = false;
            _statusLine.Text = "Bug reporting unavailable in this build (no service token).";
            _statusLine.Foreground = Brushes.DarkRed;
        }
    }

    private static void AddRow(Grid g, int row, string label)
    {
        var t = new TextBlock
        {
            Text = label,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 2),
        };
        Grid.SetRow(t, row);
        g.Children.Add(t);
    }

    private async Task OnSubmitAsync()
    {
        var what = _whatHappened.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(what))
        {
            MessageBox.Show(this,
                "Please describe what happened before submitting.",
                "Bug report", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string? token = BugReportSubmitter.GetEmbeddedToken();
        if (token is null)
        {
            // Defensive — the constructor already disabled Submit in this case.
            return;
        }

        _submitButton.IsEnabled = false;
        SetStatus("Submitting...", Brushes.DimGray);

        try
        {
            var submitter = new BugReportSubmitter(token);
            string? screenshotUrl = null;

            if (_includeScreenshot.IsChecked == true && _screenshot != null)
            {
                SetStatus("Uploading screenshot...", Brushes.DimGray);
                var png = BugReportSubmitter.EncodePng(_screenshot);
                string filename = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N").Substring(0, 8)}.png";
                screenshotUrl = await submitter.UploadScreenshotAsync(png, filename).ConfigureAwait(true);
                if (screenshotUrl is null)
                {
                    SetStatus("Screenshot upload failed; filing the report without it.", Brushes.DarkOrange);
                }
            }

            SetStatus("Filing the issue...", Brushes.DimGray);
            string title = TruncateForTitle(what);
            string body = BuildBody(what, _whatExpected.Text?.Trim() ?? "",
                _state, _includeState.IsChecked == true, _includeLog.IsChecked == true,
                screenshotUrl);

            string? issueUrl = await submitter.CreateIssueAsync(
                title, body, labels: new[] { "bug", "from-game" }).ConfigureAwait(true);

            if (issueUrl is null)
            {
                SetStatus("Couldn't file the issue. Try again, or contact the developer.", Brushes.DarkRed);
                _submitButton.IsEnabled = true;
                return;
            }

            // Success — show the URL and close the dialog on confirmation.
            _statusLine.Text = "";
            var result = MessageBox.Show(this,
                $"Bug report filed!\n\n{issueUrl}\n\nThe link is on your clipboard if you'd like to follow it.",
                "Bug report submitted", MessageBoxButton.OK, MessageBoxImage.Information);
            try { Clipboard.SetText(issueUrl); } catch { /* clipboard busy — fine */ }
            DialogResult = true;
        }
        catch (Exception ex)
        {
            SetStatus($"Submission failed: {ex.Message}", Brushes.DarkRed);
            _submitButton.IsEnabled = true;
        }
    }

    private void SetStatus(string text, Brush color)
    {
        _statusLine.Text = text;
        _statusLine.Foreground = color;
    }

    private static string TruncateForTitle(string what)
    {
        var firstLine = what.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?.Trim() ?? "Bug report";
        if (firstLine.Length > 80) firstLine = firstLine.Substring(0, 77) + "…";
        return firstLine;
    }

    private static string BuildBody(string what, string expected, GameState state,
                                    bool includeState, bool includeLog,
                                    string? screenshotUrl)
    {
        var sb = new StringBuilder();
        sb.AppendLine("**What happened**");
        sb.AppendLine();
        sb.AppendLine(what);
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(expected))
        {
            sb.AppendLine("**What I expected**");
            sb.AppendLine();
            sb.AppendLine(expected);
            sb.AppendLine();
        }

        if (screenshotUrl is not null)
        {
            sb.AppendLine("**Screenshot**");
            sb.AppendLine();
            sb.AppendLine($"![screenshot]({screenshotUrl})");
            sb.AppendLine();
        }

        sb.AppendLine("**Build**");
        sb.AppendLine();
        sb.AppendLine($"- Version: `{GetVersion()}`");
        sb.AppendLine($"- OS: `{Environment.OSVersion}`");
        sb.AppendLine($"- Players: {state.Players.Length}, turn {state.CurrentTurn}, "
                    + $"phase {state.Phase}, active P{state.ActivePlayer + 1}");
        sb.AppendLine();

        if (includeState)
        {
            try
            {
                string code = GameStateCodec.Encode(state);
                sb.AppendLine("**Current state code**");
                sb.AppendLine();
                sb.AppendLine("```");
                sb.AppendLine(code);
                sb.AppendLine("```");
                sb.AppendLine();
                if (state.Phase == GamePhase.Dogma)
                {
                    sb.AppendLine("> ⚠️ Captured mid-dogma. The codec doesn't preserve "
                                + "in-flight resolution state — loading it won't reproduce "
                                + "the exact moment, only the position.");
                    sb.AppendLine();
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"_(state code unavailable: {ex.Message})_");
                sb.AppendLine();
            }
        }

        if (includeLog)
        {
            string log = ReadTailOfLog(RecentLogLines);
            if (!string.IsNullOrEmpty(log))
            {
                sb.AppendLine($"**Last {RecentLogLines} log lines**");
                sb.AppendLine();
                sb.AppendLine("```");
                sb.AppendLine(log);
                sb.AppendLine("```");
            }
        }

        return sb.ToString();
    }

    private static string ReadTailOfLog(int lines)
    {
        try
        {
            var path = GameLog.CurrentPath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return "";
            var all = File.ReadAllLines(path);
            var tail = all.Skip(Math.Max(0, all.Length - lines));
            return string.Join("\n", tail);
        }
        catch
        {
            return "";
        }
    }

    private static string GetVersion()
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (info?.InformationalVersion is { Length: > 0 } v) return v;
            var ver = asm.GetName().Version;
            return ver?.ToString() ?? "unknown";
        }
        catch { return "unknown"; }
    }
}
