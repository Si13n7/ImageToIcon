using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;

namespace ImageToIcon.Services;

/// REST API with atom-feed fallback. GITHUB_TOKEN env var lifts the anonymous
/// rate limit (60/h) to 5000/h.
public static class GitHubClient
{
    private static readonly HttpClient Http;

    static GitHubClient()
    {
        Http = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
        Http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Wget", "1.25"));
        Http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (!string.IsNullOrWhiteSpace(token))
            Http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    /// Atom fallback never carries assets. Pass allowAtomFallback: false when the caller
    /// needs assets, otherwise a transient API failure gets masked with an assetless release.
    public static async Task<GhRelease?> FetchLatestReleaseAsync(
        string repo,
        CancellationToken ct = default,
        bool allowAtomFallback = true)
    {
        try
        {
            using var response = await Http.GetAsync(
                $"https://api.github.com/repos/{repo}/releases/latest", ct);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                return JsonSerializer.Deserialize(json, GitHubJsonContext.Default.GhRelease);
            }
        }
        catch
        {
            // fall through to atom (or return null if the caller opted out)
        }

        if (!allowAtomFallback)
            return null;

        var atom = await FetchAtomReleasesAsync(repo, ct);
        return atom.Count > 0 ? atom[0] : null;
    }

    /// Streams to disk with progress. Returns false on any error.
    public static async Task<bool> DownloadAssetAsync(
        string url,
        string destPath,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        try
        {
            using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? -1;
            await using var input = await response.Content.ReadAsStreamAsync(ct);
            await using var output = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);
            var buffer = new byte[81920];
            long downloaded = 0;
            int read;
            while ((read = await input.ReadAsync(buffer, ct)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), ct);
                downloaded += read;
                if (total > 0)
                    progress?.Report(downloaded / (double)total);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<IReadOnlyList<GhRelease>> FetchAtomReleasesAsync(string repo, CancellationToken ct)
    {
        try
        {
            var xml = await Http.GetStringAsync($"https://github.com/{repo}/releases.atom", ct);
            var doc = XDocument.Parse(xml);
            XNamespace ns = "http://www.w3.org/2005/Atom";
            return doc.Descendants(ns + "entry")
                      .Select(entry => new GhRelease
                      {
                          TagName = ExtractTag(entry.Element(ns + "id")?.Value ?? string.Empty),
                          Name = entry.Element(ns + "title")?.Value ?? string.Empty,
                          PublishedAt = DateTimeOffset.TryParse(entry.Element(ns + "updated")?.Value, out var dt) ? dt : null,
                          Body = entry.Element(ns + "content")?.Value ?? string.Empty,
                          Assets = []
                      })
                      .Where(r => r.TagName.Length > 0)
                      .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static string ExtractTag(string atomId)
    {
        var idx = atomId.LastIndexOf('/');
        return idx >= 0 && idx < atomId.Length - 1 ? atomId[(idx + 1)..] : string.Empty;
    }
}

public sealed class GhAsset
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

    [JsonPropertyName("browser_download_url")]
    public string DownloadUrl { get; set; } = string.Empty;

    [JsonPropertyName("digest")] public string Digest { get; set; } = string.Empty;

    public string Sha256Hex =>
        Digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
            ? Digest["sha256:".Length..]
            : string.Empty;
}

public sealed class GhRelease
{
    [JsonPropertyName("tag_name")] public string TagName { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("published_at")] public DateTimeOffset? PublishedAt { get; set; }
    [JsonPropertyName("body")] public string Body { get; set; } = string.Empty;
    [JsonPropertyName("assets")] public List<GhAsset> Assets { get; set; } = [];
}

[JsonSerializable(typeof(GhRelease))]
[JsonSerializable(typeof(GhAsset))]
internal partial class GitHubJsonContext : JsonSerializerContext;