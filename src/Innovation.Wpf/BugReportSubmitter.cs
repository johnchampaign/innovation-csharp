using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Innovation.Wpf;

/// <summary>
/// Files a bug report as a GitHub issue using a service-account token
/// embedded at build time via the BUGREPORT_GITHUB_TOKEN environment
/// variable (see Innovation.Wpf.csproj). The token must have
/// contents:write + issues:write on the target repo.
///
/// Two-step flow: upload the screenshot PNG to a folder in the repo via
/// the Contents API to get a stable raw URL, then create the issue with
/// markdown that embeds that URL. End users don't need a GitHub account.
/// </summary>
public sealed class BugReportSubmitter
{
    private const string ApiBase = "https://api.github.com";
    private const string Repo = "johnchampaign/innovation-csharp";
    private const string ScreenshotFolder = "bug-screenshots";
    private const string DefaultBranch = "main";

    private readonly HttpClient _http;
    private readonly string _token;

    public BugReportSubmitter(string token)
    {
        _token = token;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("InnovationWpf/1.0");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        _http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    }

    /// <summary>
    /// Reads the build-time token, or null if the build didn't supply one
    /// (i.e. source build with no env var set). Used by the dialog to
    /// decide whether the Submit button is even enabled.
    /// </summary>
    public static string? GetEmbeddedToken()
    {
        try
        {
            var attrs = Assembly.GetExecutingAssembly()
                .GetCustomAttributes<AssemblyMetadataAttribute>();
            var meta = attrs.FirstOrDefault(a =>
                string.Equals(a.Key, "BugReportToken", StringComparison.Ordinal));
            var token = meta?.Value;
            return string.IsNullOrWhiteSpace(token) ? null : token;
        }
        catch { return null; }
    }

    /// <summary>
    /// Uploads <paramref name="png"/> to <c>bug-screenshots/</c> in the
    /// target repo and returns its raw-content URL, or null if the upload
    /// failed.
    /// </summary>
    public async Task<string?> UploadScreenshotAsync(byte[] png, string fileName, CancellationToken ct = default)
    {
        // PUT /repos/{owner}/{repo}/contents/{path}
        // Body: { "message": "...", "content": <base64>, "branch": "main" }
        string path = $"{ScreenshotFolder}/{fileName}";
        var url = $"{ApiBase}/repos/{Repo}/contents/{path}";
        var body = new
        {
            message = $"Bug report screenshot: {fileName}",
            content = Convert.ToBase64String(png),
            branch = DefaultBranch,
        };
        var req = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode) return null;

        var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        // The response includes content.download_url — that's the raw URL
        // we can embed in markdown.
        if (doc.RootElement.TryGetProperty("content", out var content)
            && content.TryGetProperty("download_url", out var dl)
            && dl.GetString() is string raw)
        {
            return raw;
        }
        return null;
    }

    /// <summary>
    /// Creates a GitHub issue. Returns the issue's html_url on success,
    /// or null otherwise. Throws on transport failures.
    /// </summary>
    public async Task<string?> CreateIssueAsync(string title, string body,
        string[]? labels = null, CancellationToken ct = default)
    {
        var url = $"{ApiBase}/repos/{Repo}/issues";
        var payload = new
        {
            title,
            body,
            labels = labels ?? Array.Empty<string>(),
        };
        var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };
        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode) return null;

        var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("html_url", out var u) ? u.GetString() : null;
    }

    /// <summary>Encode a WPF BitmapSource as a PNG byte array.</summary>
    public static byte[] EncodePng(BitmapSource bmp)
    {
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using var ms = new MemoryStream();
        enc.Save(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// Uploads <paramref name="logBytes"/> as a UTF-8 text file to
    /// <c>play-logs/</c> in the target repo. Returns the raw-content URL
    /// on success, or null on failure. Same auth as the bug-report path.
    /// </summary>
    public async Task<string?> UploadGameLogAsync(byte[] logBytes, string fileName, string commitMessage, CancellationToken ct = default)
    {
        const string folder = "play-logs";
        string path = $"{folder}/{fileName}";
        var url = $"{ApiBase}/repos/{Repo}/contents/{path}";
        var body = new
        {
            message = commitMessage,
            content = Convert.ToBase64String(logBytes),
            branch = DefaultBranch,
        };
        var req = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode) return null;

        var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("content", out var content)
            && content.TryGetProperty("html_url", out var u)
            && u.GetString() is string raw)
        {
            return raw;
        }
        return null;
    }
}
