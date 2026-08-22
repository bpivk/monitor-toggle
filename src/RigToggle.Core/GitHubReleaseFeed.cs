using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;

namespace RigToggle.Core;

/// <summary>
/// This is the app's first-ever HttpClient usage -- there is no in-repo analog for
/// the HTTP call shape itself (26-PATTERNS.md "No Analog Found"), so this class
/// carries an unusually thorough doc comment citing .planning/research/STACK.md
/// §1 ("GitHub-Releases Auto-Update") and .planning/research/PITFALLS.md Pitfall 6
/// (Mark-of-the-Web/SmartScreen) even though the download itself happens in
/// RigToggle.Windows.WindowsUpdateApplier, not here -- this type only reads the
/// /releases/latest metadata.
///
/// GET https://api.github.com/repos/{owner}/{repo}/releases/latest is
/// unauthenticated (fine at the 60/hr rate limit for one launch-time check by a
/// single user, STACK.md §1) and already excludes drafts and prereleases
/// server-side (PITFALLS.md Pitfall 3) -- no client-side filtering is needed here.
/// GitHub returns 403 for any request without a User-Agent header, hence the static
/// product header set below.
///
/// T-26-01 (threat register, high severity, mitigate): the selected asset's download
/// URL is rejected -- returning null rather than the URL -- unless its scheme is
/// https AND its host is one of github.com / api.github.com /
/// objects.githubusercontent.com. This class never constructs an http:// request URI
/// and never trusts an asset URL outside that allow-list, regardless of what the API
/// response claims.
/// </summary>
public sealed class GitHubReleaseFeed : IReleaseFeed
{
    private const string TargetAssetName = "RigToggle.App.exe";
    private const string ChecksumAssetName = "RigToggle.App.exe.sha256";

    private static readonly string[] AllowedAssetHosts =
    {
        "github.com",
        "api.github.com",
        "objects.githubusercontent.com",
    };

    private readonly HttpClient _httpClient;
    private readonly string _owner;
    private readonly string _repository;
    private readonly ProductInfoHeaderValue _userAgent;

    public GitHubReleaseFeed(HttpClient httpClient, string owner, string repository)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));

        string runningVersion = typeof(GitHubReleaseFeed).Assembly.GetName().Version?.ToString() ?? "0.0";
        _userAgent = new ProductInfoHeaderValue("RigToggle", runningVersion);
    }

    /// <inheritdoc/>
    public async Task<ReleaseInfo?> GetLatestReleaseAsync(CancellationToken cancellationToken)
    {
        try
        {
            var requestUri = new Uri($"https://api.github.com/repos/{_owner}/{_repository}/releases/latest");
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.UserAgent.Add(_userAgent);

            using HttpResponseMessage response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                Log($"GetLatestReleaseAsync: non-success status {(int)response.StatusCode} ({response.StatusCode}).");
                return null;
            }

            GitHubReleaseDto? dto = await JsonSerializer
                .DeserializeAsync<GitHubReleaseDto>(
                    await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (dto is null || string.IsNullOrWhiteSpace(dto.TagName))
            {
                Log("GetLatestReleaseAsync: response deserialized to null or has no tag_name.");
                return null;
            }

            GitHubAssetDto? asset = dto.Assets?.Find(a => string.Equals(a.Name, TargetAssetName, StringComparison.Ordinal));
            if (asset is null || string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl))
            {
                Log($"GetLatestReleaseAsync: release '{dto.TagName}' has no '{TargetAssetName}' asset.");
                return null;
            }

            if (!IsAllowedAssetUrl(asset.BrowserDownloadUrl))
            {
                Log($"GetLatestReleaseAsync: rejected asset URL '{asset.BrowserDownloadUrl}' (scheme/host not on the T-26-01 allow-list).");
                return null;
            }

            // D-10/D-11 (T-26-01, T-26-08): resolve the sidecar checksum asset through the
            // SAME scheme/host allow-list as the exe asset. Its absence -- every release
            // published before this phase -- does NOT discard the release; a null
            // ChecksumDownloadUrl is passed through so WindowsUpdateApplier can fail closed
            // (an update whose integrity cannot be established must not be applied) rather
            // than this method silently making "no checksum published" indistinguishable
            // from "no release found".
            GitHubAssetDto? checksumAsset = dto.Assets?.Find(a => string.Equals(a.Name, ChecksumAssetName, StringComparison.Ordinal));
            string? checksumDownloadUrl = null;
            if (checksumAsset is not null && !string.IsNullOrWhiteSpace(checksumAsset.BrowserDownloadUrl))
            {
                if (IsAllowedAssetUrl(checksumAsset.BrowserDownloadUrl))
                {
                    checksumDownloadUrl = checksumAsset.BrowserDownloadUrl;
                }
                else
                {
                    Log($"GetLatestReleaseAsync: rejected checksum asset URL '{checksumAsset.BrowserDownloadUrl}' (scheme/host not on the T-26-01 allow-list).");
                }
            }
            else
            {
                Log($"GetLatestReleaseAsync: release '{dto.TagName}' has no '{ChecksumAssetName}' asset -- update cannot be verified.");
            }

            return new ReleaseInfo(
                dto.TagName,
                asset.BrowserDownloadUrl,
                dto.HtmlUrl ?? string.Empty,
                dto.PublishedAt,
                dto.Prerelease,
                dto.Body,
                checksumDownloadUrl);
        }
        catch (HttpRequestException ex)
        {
            Log($"GetLatestReleaseAsync: HttpRequestException: {ex.Message}");
            return null;
        }
        catch (TaskCanceledException ex)
        {
            Log($"GetLatestReleaseAsync: TaskCanceledException: {ex.Message}");
            return null;
        }
        catch (JsonException ex)
        {
            Log($"GetLatestReleaseAsync: JsonException: {ex.Message}");
            return null;
        }
        catch (UriFormatException ex)
        {
            Log($"GetLatestReleaseAsync: UriFormatException: {ex.Message}");
            return null;
        }
    }

    private static bool IsAllowedAssetUrl(string assetUrl)
    {
        if (!Uri.TryCreate(assetUrl, UriKind.Absolute, out Uri? assetUri))
        {
            return false;
        }

        if (!string.Equals(assetUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Array.IndexOf(AllowedAssetHosts, assetUri.Host) >= 0;
    }

    // Best-effort diagnostic logging, matching WindowsAppController's/
    // WindowsAutostartConfigurator's convention -- routed through Trace.WriteLine so
    // RigToggle.App's TextWriterTraceListener persists it to debug.log. Never throws.
    private static void Log(string message)
    {
        try
        {
            Trace.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] GitHubReleaseFeed: {message}");
        }
        catch
        {
            // Logging is diagnostic-only; never let it affect the update check.
        }
    }

    // Small private DTOs mapping only the fields this app needs, via explicit
    // JsonPropertyName attributes rather than a naming policy so the tag_name /
    // html_url / published_at / prerelease / assets[].name /
    // assets[].browser_download_url mapping is unambiguous at the call site.
    private sealed class GitHubReleaseDto
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("published_at")]
        public DateTimeOffset PublishedAt { get; set; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("assets")]
        public List<GitHubAssetDto>? Assets { get; set; }
    }

    private sealed class GitHubAssetDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }
    }
}
