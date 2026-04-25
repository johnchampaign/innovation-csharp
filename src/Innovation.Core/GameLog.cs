namespace Innovation.Core;

/// <summary>
/// Plain-text append-only log of every state-mutating event in a game.
/// Single active writer at a time. UIs call <see cref="Start"/> when a new
/// game begins; the file gets overwritten so the "last game" is always
/// available for post-mortem debugging.
///
/// All logging is best-effort: any I/O failure is swallowed so a broken
/// log can never break gameplay. AI look-ahead paths should not log —
/// callers must <see cref="Pause"/> before a rollout and <see cref="Resume"/>
/// after.
/// </summary>
public static class GameLog
{
    private static StreamWriter? _w;
    private static int _depth;
    private static int _pauseCount;

    /// <summary>Absolute path of the currently open log file, or null.</summary>
    public static string? CurrentPath { get; private set; }

    /// <summary>Default path in the OS temp folder.</summary>
    public static string DefaultPath =>
        Path.Combine(Path.GetTempPath(), "innovation-last-game.log");

    /// <summary>Path for the previous game's log (kept across one new-game).</summary>
    public static string PreviousPath =>
        Path.Combine(Path.GetTempPath(), "innovation-previous-game.log");

    public static void Start(string? path = null)
    {
        Stop();
        try
        {
            path ??= DefaultPath;
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            // Rotate: rename existing "last game" to "previous game" so the
            // last two games' logs are both available. Only applies when
            // we're opening the default path; custom paths get truncated.
            if (path == DefaultPath && File.Exists(path))
            {
                try
                {
                    if (File.Exists(PreviousPath)) File.Delete(PreviousPath);
                    File.Move(path, PreviousPath);
                }
                catch { /* best-effort; if rotation fails, just truncate. */ }
            }

            _w = new StreamWriter(path, append: false) { AutoFlush = true };
            CurrentPath = path;
            _depth = 0;
            _pauseCount = 0;
            _w.WriteLine($"# Innovation game log — started {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        }
        catch { _w = null; CurrentPath = null; }
    }

    public static void Stop()
    {
        try { _w?.Dispose(); } catch { }
        _w = null;
        CurrentPath = null;
    }

    /// <summary>Suppress logging — use around AI rollouts on cloned state.</summary>
    public static void Pause() => _pauseCount++;
    public static void Resume() { if (_pauseCount > 0) _pauseCount--; }

    /// <summary>
    /// Fires on every <see cref="Log"/> call that isn't paused (AI rollouts
    /// stay silent). The UI uses this to mirror engine-side events like
    /// draws and returns into its scrolling log panel without having to
    /// know which handlers emit what.
    /// </summary>
    public static event Action<string>? OnLine;

    public static void Log(string line)
    {
        if (_pauseCount > 0) return;
        if (_w is not null)
        {
            try { _w.WriteLine(new string(' ', _depth * 2) + line); } catch { }
        }
        try { OnLine?.Invoke(line); } catch { }
    }

    /// <summary>Log and indent subsequent lines until the returned scope is disposed.</summary>
    public static IDisposable Scope(string line)
    {
        Log(line);
        _depth++;
        return new Pop();
    }

    private sealed class Pop : IDisposable
    {
        public void Dispose() { if (_depth > 0) _depth--; }
    }

    /// <summary>"P1" / "P2" label.</summary>
    public static string P(int playerIndex) => $"P{playerIndex + 1}";
    public static string P(PlayerState p) => P(p.Index);

    /// <summary>"A5 Chemistry(Blue)" label for a card id.</summary>
    public static string C(GameState g, int cardId)
    {
        if (cardId < 0 || cardId >= g.Cards.Count) return $"#{cardId}";
        var c = g.Cards[cardId];
        return $"A{c.Age} {c.Title}({c.Color})";
    }
}
