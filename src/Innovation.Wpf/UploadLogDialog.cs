using System;
using System.Collections.Generic;
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
/// Lets the user upload every accumulated game log to the project repo.
/// Logs come from two places: the active game (read with shared access
/// since GameLog still holds the file open for writing) and the archive
/// folder where finished games are kept indefinitely. An index file in
/// the archive tracks which logs have already been uploaded so repeat
/// clicks only upload new ones.
/// </summary>
public sealed class UploadLogDialog : Window
{
    private const string UploadedIndexFile = "_uploaded.txt";

    private readonly TextBox _note;
    private readonly TextBlock _statusLine;
    private readonly TextBlock _pendingSummary;
    private readonly Button _submitButton;

    public UploadLogDialog()
    {
        Title = "Upload game logs";
        Width = 540;
        Height = 380;
        ResizeMode = ResizeMode.CanResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(0xF3, 0xEF, 0xD3));
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 13;

        var root = new DockPanel { Margin = new Thickness(12) };

        var header = new TextBlock
        {
            Text = "Upload your accumulated game logs so the developer can study "
                 + "how Innovation gets played and improve the AI. Every finished "
                 + "game's log has been archived locally; clicking Upload sends "
                 + "all not-yet-uploaded ones at once. Your nickname appears in "
                 + "each log.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10),
        };
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        // Pending count line.
        _pendingSummary = new TextBlock
        {
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 8),
        };
        DockPanel.SetDock(_pendingSummary, Dock.Top);
        root.Children.Add(_pendingSummary);

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
        for (int i = 0; i < 3; i++)
            form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        form.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star);

        var label = new TextBlock
        {
            Text = "Anything to say about these games? (optional, attached to the commit message)",
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
            _pendingSummary.Text = "";
            return;
        }

        RefreshPendingSummary();
    }

    private void RefreshPendingSummary()
    {
        try
        {
            var pending = EnumeratePendingLogs().ToList();
            if (pending.Count == 0)
            {
                _pendingSummary.Text = "Nothing to upload — all known logs have been submitted.";
                _submitButton.IsEnabled = false;
            }
            else
            {
                _pendingSummary.Text = $"{pending.Count} game log(s) ready to upload.";
                _submitButton.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            _pendingSummary.Text = $"Couldn't enumerate archive: {ex.Message}";
            _submitButton.IsEnabled = false;
        }
    }

    /// <summary>
    /// Yields paths of every log that should be uploaded: archived games
    /// plus the in-progress current log. Excludes any path already listed
    /// in the archive's uploaded-index file.
    /// </summary>
    private static IEnumerable<string> EnumeratePendingLogs()
    {
        var uploaded = ReadUploadedSet();

        // Archived finished games.
        if (Directory.Exists(GameLog.ArchiveDir))
        {
            foreach (var p in Directory.EnumerateFiles(GameLog.ArchiveDir, "*.log")
                                       .OrderBy(p => p))
            {
                var name = Path.GetFileName(p);
                if (!uploaded.Contains(name)) yield return p;
            }
        }

        // In-progress current log: include if it has content and a known
        // path. Use a synthetic name for the uploaded-index entry.
        var current = GameLog.CurrentPath;
        bool includeCurrent = false;
        if (!string.IsNullOrEmpty(current) && File.Exists(current))
        {
            try { includeCurrent = new FileInfo(current).Length > 0; }
            catch { /* file changed under us; skip */ }
        }
        if (includeCurrent) yield return current!;
    }

    private static HashSet<string> ReadUploadedSet()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var path = Path.Combine(GameLog.ArchiveDir, UploadedIndexFile);
            if (!File.Exists(path)) return result;
            foreach (var line in File.ReadAllLines(path))
            {
                var trimmed = line.Trim();
                if (trimmed.Length > 0) result.Add(trimmed);
            }
        }
        catch { /* index unreadable; treat as nothing uploaded */ }
        return result;
    }

    private static void AppendToUploadedIndex(string fileNameInArchive)
    {
        try
        {
            Directory.CreateDirectory(GameLog.ArchiveDir);
            var path = Path.Combine(GameLog.ArchiveDir, UploadedIndexFile);
            File.AppendAllText(path, fileNameInArchive + Environment.NewLine);
        }
        catch { /* best-effort */ }
    }

    /// <summary>
    /// Reads a log file's bytes, allowing concurrent writes from GameLog
    /// (which holds the active log open). Without the explicit share mode,
    /// File.ReadAllBytes throws "being used by another process" on the
    /// current game's log.
    /// </summary>
    private static async Task<byte[]> ReadLogBytesAsync(string path)
    {
        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var ms = new MemoryStream();
        await fs.CopyToAsync(ms).ConfigureAwait(false);
        return ms.ToArray();
    }

    private async Task OnSubmitAsync()
    {
        string? token = BugReportSubmitter.GetEmbeddedToken();
        if (token is null) return;

        var pending = EnumeratePendingLogs().ToList();
        if (pending.Count == 0)
        {
            SetStatus("Nothing to upload.", Brushes.DimGray);
            return;
        }

        _submitButton.IsEnabled = false;
        var note = _note.Text?.Trim() ?? "";

        var submitter = new BugReportSubmitter(token);
        int succeeded = 0;
        int failed = 0;

        for (int i = 0; i < pending.Count; i++)
        {
            var logPath = pending[i];
            SetStatus($"Uploading {i + 1} / {pending.Count}...", Brushes.DimGray);

            byte[] bytes;
            try
            {
                bytes = await ReadLogBytesAsync(logPath).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                SetStatus($"Couldn't read {Path.GetFileName(logPath)}: {ex.Message}",
                    Brushes.DarkOrange);
                failed++;
                continue;
            }

            // For the in-progress current log we still need a stable
            // archive name to record in the uploaded-index. Derive from
            // the file's last-write timestamp so subsequent uploads
            // recognise the same file.
            string archiveName;
            if (logPath.Equals(GameLog.CurrentPath, StringComparison.OrdinalIgnoreCase))
            {
                var stamp = File.GetLastWriteTimeUtc(logPath).ToString("yyyyMMdd-HHmmss");
                archiveName = $"game-{stamp}-current.log";
            }
            else
            {
                archiveName = Path.GetFileName(logPath);
            }

            // Prepend the note (only on the first log of the batch) so
            // it travels with the upload.
            if (i == 0 && !string.IsNullOrWhiteSpace(note))
            {
                var sb = new StringBuilder();
                sb.AppendLine($"# Player note: {note.Replace("\n", " ")}");
                sb.Append(Encoding.UTF8.GetString(bytes));
                bytes = Encoding.UTF8.GetBytes(sb.ToString());
            }

            string version = GetVersion();
            string ts = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            string rand = Guid.NewGuid().ToString("N").Substring(0, 6);
            string remoteName = $"{ts}-v{version}-{rand}-{archiveName}";
            string commitMsg = $"Play log: {remoteName}";

            string? url = null;
            try
            {
                url = await submitter.UploadGameLogAsync(bytes, remoteName, commitMsg)
                    .ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                SetStatus($"Upload error: {ex.Message}", Brushes.DarkOrange);
            }

            if (url is null)
            {
                failed++;
                continue;
            }

            AppendToUploadedIndex(archiveName);
            succeeded++;
        }

        if (failed == 0)
        {
            _statusLine.Text = "";
            MessageBox.Show(this,
                $"Uploaded {succeeded} game log(s). Thanks!",
                "Upload complete", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }
        else
        {
            SetStatus($"{succeeded} uploaded, {failed} failed. Try again later.",
                Brushes.DarkRed);
            RefreshPendingSummary();
            _submitButton.IsEnabled = true;
        }
    }

    private void SetStatus(string text, Brush color)
    {
        _statusLine.Text = text;
        _statusLine.Foreground = color;
    }

    private static string GetVersion()
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (info?.InformationalVersion is { Length: > 0 } v)
            {
                int plus = v.IndexOf('+');
                return plus > 0 ? v.Substring(0, plus) : v;
            }
            return asm.GetName().Version?.ToString() ?? "unknown";
        }
        catch { return "unknown"; }
    }
}
