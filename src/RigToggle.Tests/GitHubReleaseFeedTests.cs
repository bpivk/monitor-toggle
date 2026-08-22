using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using RigToggle.Core;
using RigToggle.Core.Models;
using Xunit;

namespace RigToggle.Tests;

/// <summary>
/// Proves GitHubReleaseFeed's asset-selection block: resolving both the exe and the
/// D-10/D-11 checksum asset through the T-26-01 scheme/host allow-list, tolerating a
/// missing checksum asset without discarding the release, and every failure-to-null
/// path (missing exe asset, disallowed scheme/host, 404, transport exception,
/// malformed JSON). Needs no network -- a hand-rolled HttpMessageHandler subclass
/// (this repo's first) returns a canned HttpResponseMessage per request and records
/// the requests it saw, so the User-Agent assertion reads off the recorded request
/// rather than assuming it was sent. Async Task test methods throughout, no blocking
/// task operations (xUnit1031).
/// </summary>
public class GitHubReleaseFeedTests
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/bpivk/monitor-toggle/releases/latest";

    // Deliberately includes one extra unmapped property ("draft") to prove the
    // deserializer ignores fields it does not map.
    private const string PayloadWithBothAssets = """
        {
          "tag_name": "v2.2",
          "html_url": "https://github.com/bpivk/monitor-toggle/releases/tag/v2.2",
          "published_at": "2026-08-20T12:00:00Z",
          "prerelease": false,
          "draft": false,
          "assets": [
            { "name": "RigToggle.App.exe", "browser_download_url": "https://objects.githubusercontent.com/asset/RigToggle.App.exe" },
            { "name": "RigToggle.App.exe.sha256", "browser_download_url": "https://objects.githubusercontent.com/asset/RigToggle.App.exe.sha256" }
          ]
        }
        """;

    private const string PayloadWithOnlyExeAsset = """
        {
          "tag_name": "v2.1",
          "html_url": "https://github.com/bpivk/monitor-toggle/releases/tag/v2.1",
          "published_at": "2026-08-01T00:00:00Z",
          "prerelease": false,
          "assets": [
            { "name": "RigToggle.App.exe", "browser_download_url": "https://objects.githubusercontent.com/asset/RigToggle.App.exe" }
          ]
        }
        """;

    private const string PayloadWithNoMatchingAsset = """
        {
          "tag_name": "v2.2",
          "html_url": "https://github.com/bpivk/monitor-toggle/releases/tag/v2.2",
          "published_at": "2026-08-20T12:00:00Z",
          "prerelease": false,
          "assets": [
            { "name": "SomeOtherFile.zip", "browser_download_url": "https://objects.githubusercontent.com/asset/SomeOtherFile.zip" }
          ]
        }
        """;

    private const string PayloadWithHttpExeAsset = """
        {
          "tag_name": "v2.2",
          "html_url": "https://github.com/bpivk/monitor-toggle/releases/tag/v2.2",
          "published_at": "2026-08-20T12:00:00Z",
          "prerelease": false,
          "assets": [
            { "name": "RigToggle.App.exe", "browser_download_url": "http://objects.githubusercontent.com/asset/RigToggle.App.exe" }
          ]
        }
        """;

    private const string PayloadWithOffHostExeAsset = """
        {
          "tag_name": "v2.2",
          "html_url": "https://github.com/bpivk/monitor-toggle/releases/tag/v2.2",
          "published_at": "2026-08-20T12:00:00Z",
          "prerelease": false,
          "assets": [
            { "name": "RigToggle.App.exe", "browser_download_url": "https://evil.example.com/asset/RigToggle.App.exe" }
          ]
        }
        """;

    private const string MalformedJsonPayload = "{ not valid json ";

    [Fact]
    public async Task GetLatestReleaseAsync_BothAssetsPresent_PopulatesAssetAndChecksumUrls()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(PayloadWithBothAssets),
        });
        var feed = new GitHubReleaseFeed(new HttpClient(handler), "bpivk", "monitor-toggle");

        ReleaseInfo? release = await feed.GetLatestReleaseAsync(CancellationToken.None);

        Assert.NotNull(release);
        Assert.Equal("https://objects.githubusercontent.com/asset/RigToggle.App.exe", release!.AssetDownloadUrl);
        Assert.Equal("https://objects.githubusercontent.com/asset/RigToggle.App.exe.sha256", release.ChecksumDownloadUrl);
    }

    [Fact]
    public async Task GetLatestReleaseAsync_OnlyExeAssetPresent_ReturnsNullChecksumUrl()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(PayloadWithOnlyExeAsset),
        });
        var feed = new GitHubReleaseFeed(new HttpClient(handler), "bpivk", "monitor-toggle");

        ReleaseInfo? release = await feed.GetLatestReleaseAsync(CancellationToken.None);

        Assert.NotNull(release);
        Assert.Equal("https://objects.githubusercontent.com/asset/RigToggle.App.exe", release!.AssetDownloadUrl);
        Assert.Null(release.ChecksumDownloadUrl);
    }

    [Fact]
    public async Task GetLatestReleaseAsync_NoExeAsset_ReturnsNull()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(PayloadWithNoMatchingAsset),
        });
        var feed = new GitHubReleaseFeed(new HttpClient(handler), "bpivk", "monitor-toggle");

        ReleaseInfo? release = await feed.GetLatestReleaseAsync(CancellationToken.None);

        Assert.Null(release);
    }

    [Fact]
    public async Task GetLatestReleaseAsync_ExeAssetUsesHttp_ReturnsNull()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(PayloadWithHttpExeAsset),
        });
        var feed = new GitHubReleaseFeed(new HttpClient(handler), "bpivk", "monitor-toggle");

        ReleaseInfo? release = await feed.GetLatestReleaseAsync(CancellationToken.None);

        Assert.Null(release);
    }

    [Fact]
    public async Task GetLatestReleaseAsync_ExeAssetOffAllowedHost_ReturnsNull()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(PayloadWithOffHostExeAsset),
        });
        var feed = new GitHubReleaseFeed(new HttpClient(handler), "bpivk", "monitor-toggle");

        ReleaseInfo? release = await feed.GetLatestReleaseAsync(CancellationToken.None);

        Assert.Null(release);
    }

    [Fact]
    public async Task GetLatestReleaseAsync_NotFoundResponse_ReturnsNull_DoesNotThrow()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var feed = new GitHubReleaseFeed(new HttpClient(handler), "bpivk", "monitor-toggle");

        ReleaseInfo? release = await feed.GetLatestReleaseAsync(CancellationToken.None);

        Assert.Null(release);
    }

    [Fact]
    public async Task GetLatestReleaseAsync_TransportException_ReturnsNull_DoesNotThrow()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("simulated network failure"));
        var feed = new GitHubReleaseFeed(new HttpClient(handler), "bpivk", "monitor-toggle");

        ReleaseInfo? release = await feed.GetLatestReleaseAsync(CancellationToken.None);

        Assert.Null(release);
    }

    [Fact]
    public async Task GetLatestReleaseAsync_MalformedJson_ReturnsNull_DoesNotThrow()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(MalformedJsonPayload),
        });
        var feed = new GitHubReleaseFeed(new HttpClient(handler), "bpivk", "monitor-toggle");

        ReleaseInfo? release = await feed.GetLatestReleaseAsync(CancellationToken.None);

        Assert.Null(release);
    }

    [Fact]
    public async Task GetLatestReleaseAsync_SendsNonEmptyUserAgentHeader()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(PayloadWithBothAssets),
        });
        var feed = new GitHubReleaseFeed(new HttpClient(handler), "bpivk", "monitor-toggle");

        await feed.GetLatestReleaseAsync(CancellationToken.None);

        Assert.Single(handler.Requests);
        Assert.True(handler.Requests[0].Headers.UserAgent.Count > 0);
        Assert.False(string.IsNullOrWhiteSpace(handler.Requests[0].Headers.UserAgent.ToString()));
    }

    [Fact]
    public async Task GetLatestReleaseAsync_RequestsExpectedLatestReleaseUrl()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(PayloadWithBothAssets),
        });
        var feed = new GitHubReleaseFeed(new HttpClient(handler), "bpivk", "monitor-toggle");

        await feed.GetLatestReleaseAsync(CancellationToken.None);

        Assert.Single(handler.Requests);
        Assert.Equal(LatestReleaseUrl, handler.Requests[0].RequestUri!.ToString());
    }

    /// <summary>
    /// Hand-rolled HttpMessageHandler double -- this repo uses no mocking library
    /// (matching Doubles/InMemoryStores.cs's convention). Records every request it
    /// sees and returns whatever the supplied responder function produces (or lets
    /// it throw, for the transport-exception test case).
    /// </summary>
    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        public List<HttpRequestMessage> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_responder(request));
        }
    }
}
