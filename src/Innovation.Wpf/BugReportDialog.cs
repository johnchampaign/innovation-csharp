using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Innovation.Core;

namespace Innovation.Wpf;

/// <summary>
/// Modal dialog that helps the user file a bug as a GitHub issue. The user
/// fills in what happened and (optionally) what they expected; the dialog
/// constructs an issue URL with their description, the most recent state-code
/// snapshot, the trailing log lines, and the build version, then opens the
/// pre-filled new-issue page in the default browser. The user reviews and
/// submits via their own GitHub account — no tokens or backends.
/// </summary>
public sealed class BugReportDialog : Window
{
    private const string IssueUrlBase =
        "https://github.com/johnchampaign/innovation-csharp/issues/new";

    /// <summary>How many trailing log lines to attach by default.</summary>
    private const int RecentLogLines = 40;

    /// <summary>Cap the body length so GitHub's URL-prefill works.</summary>
    private const int MaxBodyLength = 6000;

    private readonly TextBox _whatHappened;
    private readonly TextBox _whatExpected;
    private readonly CheckBox _includeLog;
    private readonly CheckBox _includeState;

    public BugReportDialog(GameState state)
    {
        Title = "Report a bug";
        Width = 560;
        Height = 540;
        ResizeMode = ResizeMode.CanResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(0xF3, 0xEF, 0xD3));
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 13;

        var root = new DockPanel { Margin = new Thickness(12) };

        // Header explaining what happens on submit.
        var header = new TextBlock
        {
            Text = "Submit will open a pre-filled issue page in your browser. "
                 + "Review it (you can edit anything), then click GitHub's "
                 + "“Submit new issue” button to file it. You'll need to be "
                 + "signed in to GitHub.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10),
        };
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        // Bottom buttons.
        var btnRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        var ok = new Button
        {
            Content = "Submit",
            Width = 100,
            Margin = new Thickness(0, 0, 6, 0),
            Padding = new Thickness(8, 4, 8, 4),
            IsDefault = true,
        };
        ok.Click += (_, _) => OnSubmit(state);
        var cancel = new Button
        {
            Content = "Cancel",
            Width = 80,
            Padding = new Thickness(8, 4, 8, 4),
            IsCancel = true,
        };
        btnRow.Children.Add(ok);
        btnRow.Children.Add(cancel);
        DockPanel.SetDock(btnRow, Dock.Bottom);
        root.Children.Add(btnRow);

        // Form content fills the rest.
        var form = new Grid();
        for (int i = 0; i < 8; i++)
            form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        form.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star);
        form.RowDefinitions[3].Height = new GridLength(1, GridUnitType.Star);
        DockPanel.SetDock(form, Dock.Top);

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
            Content = "Include the most recent turn-boundary state code (recommended — easier to reproduce)",
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

        var footnote = new TextBlock
        {
            Text = "Your nickname appears in the log. Don't include sensitive info.",
            FontStyle = FontStyles.Italic,
            Foreground = Brushes.DimGray,
            Margin = new Thickness(0, 4, 0, 8),
        };
        Grid.SetRow(footnote, 6);
        form.Children.Add(footnote);

        root.Children.Add(form);
        Content = root;
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

    private void OnSubmit(GameState state)
    {
        var what = _whatHappened.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(what))
        {
            MessageBox.Show(this,
                "Please describe what happened before submitting.",
                "Bug report", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string title = TruncateForTitle(what);
        string body = BuildBody(what, _whatExpected.Text?.Trim() ?? "",
            state, _includeState.IsChecked == true, _includeLog.IsChecked == true);

        // Construct the GitHub new-issue URL. Title and body are URL-encoded.
        // GitHub also accepts a "labels" parameter pre-tagging the issue.
        string url = $"{IssueUrlBase}?title={WebUtility.UrlEncode(title)}"
                   + $"&body={WebUtility.UrlEncode(body)}"
                   + $"&labels=bug,from-game";

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                $"Couldn't open the browser:\n{ex.Message}\n\nYou can paste this URL manually:\n{url}",
                "Bug report", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static string TruncateForTitle(string what)
    {
        // Use the first non-empty line, capped at ~80 chars, no trailing
        // punctuation cleanup beyond a basic ellipsis.
        var firstLine = what.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?.Trim() ?? "Bug report";
        if (firstLine.Length > 80) firstLine = firstLine.Substring(0, 77) + "…";
        return firstLine;
    }

    private static string BuildBody(string what, string expected, GameState state,
                                    bool includeState, bool includeLog)
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
                    sb.AppendLine("> ⚠️ This snapshot was taken mid-dogma. The codec doesn't "
                                + "preserve in-flight resolution state, so loading it won't "
                                + "reproduce the exact moment — but it pins down the position.");
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

        var body = sb.ToString();
        if (body.Length > MaxBodyLength)
        {
            // Truncate but keep the front (description + state code).
            body = body.Substring(0, MaxBodyLength - 100)
                 + $"\n\n... (truncated {body.Length - (MaxBodyLength - 100)} chars to fit GitHub's URL-prefill limit)";
        }
        return body;
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
