using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace RavenneToolsMenu.Core;

public sealed record AppUpdate(Version Version, string Url, long Size, string Notes, DateTime Published);

/// <summary>
/// Vérification des mises à jour via l'API GitHub Releases (dépôt public, sans authentification).
/// La version courante vient de l'assembly (&lt;Version&gt; du .csproj).
/// </summary>
internal static class UpdateChecker
{
    public const string SetupAssetName = "RavenneToolsMenu-Setup.exe";

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd($"RavenneToolsMenu/{CurrentVersion}");
        c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return c;
    }

    public static Version CurrentVersion =>
        typeof(App).Assembly.GetName().Version ?? new Version(1, 0, 0);

    /// <summary>Retourne la dernière version publiée si elle est plus récente que l'actuelle, sinon null.</summary>
    public static async Task<AppUpdate?> CheckAsync(string repo)
    {
        if (string.IsNullOrWhiteSpace(repo) || !repo.Contains('/'))
            throw new InvalidOperationException("Dépôt de mise à jour non configuré (updates.repo dans config.json).");

        using var resp = await Http.GetAsync($"https://api.github.com/repos/{repo}/releases/latest");
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null; // aucune release publiée
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        var tag = root.TryGetProperty("tag_name", out var tagEl) ? tagEl.GetString() : null;
        if (string.IsNullOrEmpty(tag)) return null;
        var version = Version.TryParse(tag.TrimStart('v', 'V'), out var v) ? v : null;
        if (version is null || version <= CurrentVersion) return null;

        string? url = null;
        long size = 0;
        if (root.TryGetProperty("assets", out var assets))
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (name is not null && name.Equals(SetupAssetName, StringComparison.OrdinalIgnoreCase))
                {
                    url = asset.GetProperty("browser_download_url").GetString();
                    if (asset.TryGetProperty("size", out var s)) size = s.GetInt64();
                    break;
                }
            }
        }
        if (string.IsNullOrEmpty(url)) return null; // release sans installateur

        var notes = root.TryGetProperty("body", out var body) ? body.GetString() : "";
        DateTime published = root.TryGetProperty("published_at", out var pub) &&
                             DateTime.TryParse(pub.GetString(), null, System.Globalization.DateTimeStyles.RoundtripKind, out var d)
            ? d : DateTime.UtcNow;

        return new AppUpdate(version, url, size, CleanNotes(notes), published);
    }

    /// <summary>Télécharge l'installateur avec progression (0–100) vers dest.</summary>
    public static async Task DownloadAsync(AppUpdate update, string dest, IProgress<int> progress,
        CancellationToken ct = default)
    {
        using var resp = await Http.GetAsync(update.Url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        long total = update.Size > 0 ? update.Size : resp.Content.Headers.ContentLength ?? 0;

        await using var src = await resp.Content.ReadAsStreamAsync(ct);
        await using var dst = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16, useAsync: true);

        var buffer = new byte[1 << 16];
        long read = 0;
        int n;
        while ((n = await src.ReadAsync(buffer, ct)) > 0)
        {
            await dst.WriteAsync(buffer.AsMemory(0, n), ct);
            read += n;
            if (total > 0) progress.Report((int)(read * 100 / total));
        }
        progress.Report(100);
    }

    private static string CleanNotes(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return "";
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var l = lines[i].Trim();
            if (l.StartsWith("#")) lines[i] = l.TrimStart('#', ' ');
            else if (l.StartsWith("<!--") || l.StartsWith("[!")) lines[i] = "";
        }
        var text = string.Join('\n', lines).Replace("**", "").Replace("`", "").Trim();
        return text.Length > 1500 ? text[..1500] + "\n…" : text;
    }
}
