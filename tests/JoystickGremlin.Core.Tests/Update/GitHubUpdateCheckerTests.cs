// SPDX-License-Identifier: GPL-3.0-only

using System.Net;
using JoystickGremlin.Core.Update;
using Microsoft.Extensions.Logging.Abstractions;

namespace JoystickGremlin.Core.Tests.Update;

public sealed class GitHubUpdateCheckerTests
{
    private static readonly Version Current = new(12, 1, 0);

    // ── Version comparison ─────────────────────────────────────────────────

    [Fact]
    public async Task NewerRelease_ReportsUpdateAvailable()
    {
        var checker = CreateChecker(JsonResponse(ReleaseJson("v12.2.0")));

        var result = await checker.CheckForUpdateAsync();

        result.Status.Should().Be(UpdateCheckStatus.UpdateAvailable);
        result.CurrentVersion.Should().Be(new Version(12, 1, 0));
        result.LatestVersion.Should().Be(new Version(12, 2, 0));
    }

    [Fact]
    public async Task SameVersion_ReportsUpToDate()
    {
        var checker = CreateChecker(JsonResponse(ReleaseJson("v12.1.0")));

        var result = await checker.CheckForUpdateAsync();

        result.Status.Should().Be(UpdateCheckStatus.UpToDate);
        result.LatestVersion.Should().Be(new Version(12, 1, 0));
    }

    [Fact]
    public async Task OlderRelease_ReportsUpToDate()
    {
        var checker = CreateChecker(JsonResponse(ReleaseJson("v12.0.0")));

        var result = await checker.CheckForUpdateAsync();

        result.Status.Should().Be(UpdateCheckStatus.UpToDate);
    }

    [Fact]
    public async Task FourPartAssemblyVersion_ComparesEqualToThreePartTag()
    {
        // Assembly versions are stamped as 12.1.0.0 while release tags are v12.1.0.
        var checker = CreateChecker(JsonResponse(ReleaseJson("v12.1.0")), currentVersion: new Version(12, 1, 0, 0));

        var result = await checker.CheckForUpdateAsync();

        result.Status.Should().Be(UpdateCheckStatus.UpToDate);
    }

    // ── Tag parsing ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("v12.2.0", 12, 2, 0)]
    [InlineData("V12.2.0", 12, 2, 0)]
    [InlineData("12.2.0", 12, 2, 0)]
    [InlineData(" v12.2.0 ", 12, 2, 0)]
    [InlineData("v12.2.0-rc.1", 12, 2, 0)]
    [InlineData("v12.2.0+build5", 12, 2, 0)]
    [InlineData("v12.2", 12, 2, 0)]
    public void TryParseVersionTag_AcceptsKnownFormats(string tag, int major, int minor, int build)
    {
        GitHubUpdateChecker.TryParseVersionTag(tag, out var version).Should().BeTrue();
        version.Should().Be(new Version(major, minor, build));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("latest")]
    [InlineData("v12")]
    [InlineData("va.b.c")]
    public void TryParseVersionTag_RejectsMalformedTags(string? tag)
    {
        GitHubUpdateChecker.TryParseVersionTag(tag, out _).Should().BeFalse();
    }

    [Fact]
    public async Task MalformedTag_ReportsFailed()
    {
        var checker = CreateChecker(JsonResponse(ReleaseJson("latest")));

        var result = await checker.CheckForUpdateAsync();

        result.Status.Should().Be(UpdateCheckStatus.Failed);
        result.ErrorMessage.Should().Contain("latest");
    }

    [Fact]
    public async Task MissingTagProperty_ReportsFailed()
    {
        var checker = CreateChecker(JsonResponse("{}"));

        var result = await checker.CheckForUpdateAsync();

        result.Status.Should().Be(UpdateCheckStatus.Failed);
    }

    // ── Release metadata extraction ────────────────────────────────────────

    [Fact]
    public async Task InstallerAsset_SurfacedAsDownloadUrl()
    {
        var checker = CreateChecker(JsonResponse(ReleaseJson("v12.2.0")));

        var result = await checker.CheckForUpdateAsync();

        result.DownloadUrl.Should().Be(
            "https://github.com/DevilDogTG/JoystickGremlinSharp/releases/download/v12.2.0/JoystickGremlinSharp-12.2.0-Setup.msi");
        result.ReleaseUrl.Should().Be("https://github.com/DevilDogTG/JoystickGremlinSharp/releases/tag/v12.2.0");
        result.ReleaseNotes.Should().Contain("Changes");
    }

    [Fact]
    public async Task NoInstallerAsset_LeavesDownloadUrlNull()
    {
        var checker = CreateChecker(JsonResponse(ReleaseJson("v12.2.0", withMsiAsset: false)));

        var result = await checker.CheckForUpdateAsync();

        result.Status.Should().Be(UpdateCheckStatus.UpdateAvailable);
        result.DownloadUrl.Should().BeNull();
    }

    [Fact]
    public async Task MissingHtmlUrl_FallsBackToReleasesPage()
    {
        var checker = CreateChecker(JsonResponse(ReleaseJson("v12.2.0", withHtmlUrl: false)));

        var result = await checker.CheckForUpdateAsync();

        result.ReleaseUrl.Should().Be(GitHubUpdateChecker.ReleasesPageUrl);
    }

    // ── Failure paths ──────────────────────────────────────────────────────

    [Fact]
    public async Task NonSuccessStatusCode_ReportsFailed()
    {
        // 403 is what GitHub returns when the unauthenticated rate limit is exhausted.
        var checker = CreateChecker(new HttpResponseMessage(HttpStatusCode.Forbidden));

        var result = await checker.CheckForUpdateAsync();

        result.Status.Should().Be(UpdateCheckStatus.Failed);
        result.ErrorMessage.Should().Contain("403");
    }

    [Fact]
    public async Task NetworkFailure_ReportsFailed()
    {
        var checker = CreateChecker(new StubHandler(_ => throw new HttpRequestException("connection refused")));

        var result = await checker.CheckForUpdateAsync();

        result.Status.Should().Be(UpdateCheckStatus.Failed);
        result.ErrorMessage.Should().Contain("connection refused");
    }

    [Fact]
    public async Task InvalidJsonBody_ReportsFailed()
    {
        var checker = CreateChecker(JsonResponse("not json"));

        var result = await checker.CheckForUpdateAsync();

        result.Status.Should().Be(UpdateCheckStatus.Failed);
    }

    [Fact]
    public async Task CallerCancellation_Propagates()
    {
        using var cts = new CancellationTokenSource();
        var checker = CreateChecker(new StubHandler(_ =>
        {
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        }));

        var act = () => checker.CheckForUpdateAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ── Request shape ──────────────────────────────────────────────────────

    [Fact]
    public async Task Request_CarriesUserAgentAndAcceptHeaders()
    {
        // GitHub's API rejects requests without a User-Agent.
        var handler = new StubHandler(_ => JsonResponse(ReleaseJson("v12.1.0")));
        var checker = CreateChecker(handler);

        await checker.CheckForUpdateAsync();

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.RequestUri!.ToString().Should().Be(GitHubUpdateChecker.LatestReleaseApiUrl);
        handler.LastRequest.Headers.UserAgent.ToString().Should().Contain("JoystickGremlinSharp");
        handler.LastRequest.Headers.Accept.ToString().Should().Contain("application/vnd.github+json");
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static GitHubUpdateChecker CreateChecker(HttpResponseMessage response, Version? currentVersion = null) =>
        CreateChecker(new StubHandler(_ => response), currentVersion);

    private static GitHubUpdateChecker CreateChecker(StubHandler handler, Version? currentVersion = null) =>
        new(NullLogger<GitHubUpdateChecker>.Instance, handler, currentVersion ?? Current);

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json) };

    private static string ReleaseJson(string tag, bool withMsiAsset = true, bool withHtmlUrl = true)
    {
        var bareVersion = tag.TrimStart('v', 'V');
        var htmlUrl = withHtmlUrl
            ? $"\"html_url\": \"https://github.com/DevilDogTG/JoystickGremlinSharp/releases/tag/{tag}\","
            : string.Empty;
        var assets = withMsiAsset
            ? $$"""
              [
                { "name": "symbols.zip", "browser_download_url": "https://example.test/symbols.zip" },
                {
                  "name": "JoystickGremlinSharp-{{bareVersion}}-Setup.msi",
                  "browser_download_url": "https://github.com/DevilDogTG/JoystickGremlinSharp/releases/download/{{tag}}/JoystickGremlinSharp-{{bareVersion}}-Setup.msi"
                }
              ]
              """
            : "[]";

        return $$"""
            {
              "tag_name": "{{tag}}",
              {{htmlUrl}}
              "body": "## Changes\n- something new",
              "assets": {{assets}}
            }
            """;
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(responder(request));
        }
    }
}
