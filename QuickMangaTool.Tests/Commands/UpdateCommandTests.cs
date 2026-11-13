using System.Net;
using System.Text;
using Moq;

namespace QuickMangaTool.Tests.Commands;

public class UpdateCommandTests : IDisposable
{
    private readonly string _tempDir;

    public UpdateCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "qmt-update-tests-" + Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private static string GetCurrentOsAssetName()
        => OperatingSystem.IsWindows() ? "QuickMangaTool-win-x64.exe" : "QuickMangaTool-linux-x64";

    [Fact]
    public void Execute_WhenAlreadyUpToDate_DoesNotDownloadOrReplace()
    {
        var fakeExePath = Path.Combine(_tempDir, "QuickMangaTool");
        File.WriteAllText(fakeExePath, "old-binary");

        var release = new GitHubReleaseInfo(
            TagName: "v1.2.0",
            Name: "QuickMangaTool v1.2.0",
            HtmlUrl: "https://github.com/quyentruong/QuickMangaTool/releases/tag/v1.2.0",
            PublishedAt: DateTimeOffset.UtcNow,
            Assets: new[]
            {
                new GitHubReleaseAsset(GetCurrentOsAssetName(), "https://example.com/asset", 128)
            });

        var serviceMock = new Mock<IGitHubReleaseService>();
        serviceMock.Setup(s => s.GetLatestReleaseAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(release);

        using var output = new StringWriter();
        var cmd = new UpdateCommand(
            confirm: true,
            releaseService: serviceMock.Object,
            currentExecutablePath: fakeExePath,
            currentVersion: "1.2.0",
            output: output);

        var exitCode = cmd.Execute(Array.Empty<string>(), new NoopRunLogger());

        Assert.Equal(0, exitCode);
        Assert.Contains("mới nhất", output.ToString());
        Assert.Equal("old-binary", File.ReadAllText(fakeExePath));
        serviceMock.Verify(
            s => s.DownloadAssetAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<Action<long, long?>?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void Execute_WhenNewerVersionAvailable_WithConfirm_ReplacesBinaryAndCleansUp()
    {
        var fakeExePath = Path.Combine(_tempDir, OperatingSystem.IsWindows() ? "QuickMangaTool.exe" : "QuickMangaTool");
        File.WriteAllText(fakeExePath, "old-binary-content");

        var newBinaryBytes = Encoding.UTF8.GetBytes("new-binary-content-v2");
        var assetName = GetCurrentOsAssetName();
        var release = new GitHubReleaseInfo(
            TagName: "v2.0.0",
            Name: "QuickMangaTool v2.0.0",
            HtmlUrl: "https://github.com/quyentruong/QuickMangaTool/releases/tag/v2.0.0",
            PublishedAt: DateTimeOffset.UtcNow,
            Assets: new[]
            {
                new GitHubReleaseAsset(assetName, "https://example.com/" + assetName, newBinaryBytes.Length)
            });

        var serviceMock = new Mock<IGitHubReleaseService>();
        serviceMock.Setup(s => s.GetLatestReleaseAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(release);
        serviceMock.Setup(s => s.DownloadAssetAsync(
                "https://example.com/" + assetName,
                It.IsAny<Stream>(),
                It.IsAny<Action<long, long?>?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, Stream, Action<long, long?>?, CancellationToken>(async (_, dest, progress, ct) =>
            {
                await dest.WriteAsync(newBinaryBytes, ct);
                progress?.Invoke(newBinaryBytes.Length, newBinaryBytes.Length);
            });

        using var output = new StringWriter();
        var cmd = new UpdateCommand(
            confirm: true,
            releaseService: serviceMock.Object,
            currentExecutablePath: fakeExePath,
            currentVersion: "1.0.0",
            output: output);

        var exitCode = cmd.Execute(Array.Empty<string>(), new NoopRunLogger());

        Assert.Equal(0, exitCode);
        Assert.Contains("Cập nhật thành công", output.ToString());
        Assert.Equal(newBinaryBytes, File.ReadAllBytes(fakeExePath));
        Assert.False(File.Exists(fakeExePath + ".update.tmp"));
        Assert.False(File.Exists(fakeExePath + ".old"));

        if (!OperatingSystem.IsWindows())
        {
            var mode = File.GetUnixFileMode(fakeExePath);
            Assert.True(mode.HasFlag(UnixFileMode.UserExecute));
        }
    }

    [Fact]
    public void Execute_WhenNoReleaseFound_ReturnsZeroGracefully()
    {
        var fakeExePath = Path.Combine(_tempDir, "QuickMangaTool");
        File.WriteAllText(fakeExePath, "old-binary");

        var serviceMock = new Mock<IGitHubReleaseService>();
        serviceMock.Setup(s => s.GetLatestReleaseAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((GitHubReleaseInfo?)null);

        using var output = new StringWriter();
        var cmd = new UpdateCommand(
            confirm: true,
            releaseService: serviceMock.Object,
            currentExecutablePath: fakeExePath,
            currentVersion: "1.0.0",
            output: output);

        var exitCode = cmd.Execute(Array.Empty<string>(), new NoopRunLogger());

        Assert.Equal(0, exitCode);
        Assert.Contains("Chưa có bản phát hành", output.ToString());
    }

    [Fact]
    public void Execute_WhenRunningUnderDotnetHost_RefusesToOverwriteAndReturnsError()
    {
        var dotnetPath = Path.Combine(_tempDir, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
        File.WriteAllText(dotnetPath, "system-dotnet-host");

        var release = new GitHubReleaseInfo(
            TagName: "v2.0.0",
            Name: "QuickMangaTool v2.0.0",
            HtmlUrl: "https://github.com/quyentruong/QuickMangaTool/releases/tag/v2.0.0",
            PublishedAt: DateTimeOffset.UtcNow,
            Assets: new[]
            {
                new GitHubReleaseAsset(GetCurrentOsAssetName(), "https://example.com/asset", 512)
            });

        var serviceMock = new Mock<IGitHubReleaseService>();
        serviceMock.Setup(s => s.GetLatestReleaseAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(release);

        using var output = new StringWriter();
        var cmd = new UpdateCommand(
            confirm: true,
            releaseService: serviceMock.Object,
            currentExecutablePath: dotnetPath,
            currentVersion: "1.0.0",
            output: output);

        var exitCode = cmd.Execute(Array.Empty<string>(), new NoopRunLogger());

        Assert.Equal(1, exitCode);
        Assert.Contains("dotnet", output.ToString());
        Assert.Equal("system-dotnet-host", File.ReadAllText(dotnetPath));
        serviceMock.Verify(
            s => s.DownloadAssetAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<Action<long, long?>?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GitHubReleaseService_ParsesReleaseJsonCorrectly()
    {
        const string json = """
            {
              "tag_name": "v1.4.2",
              "name": "QuickMangaTool v1.4.2",
              "html_url": "https://github.com/quyentruong/QuickMangaTool/releases/tag/v1.4.2",
              "published_at": "2026-11-12T10:00:00Z",
              "assets": [
                {
                  "name": "QuickMangaTool-linux-x64",
                  "browser_download_url": "https://github.com/quyentruong/QuickMangaTool/releases/download/v1.4.2/QuickMangaTool-linux-x64",
                  "size": 42000000
                },
                {
                  "name": "QuickMangaTool-win-x64.exe",
                  "browser_download_url": "https://github.com/quyentruong/QuickMangaTool/releases/download/v1.4.2/QuickMangaTool-win-x64.exe",
                  "size": 45000000
                }
              ]
            }
            """;

        using var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
        using var httpClient = new HttpClient(handler);
        using var service = new GitHubReleaseService(httpClient);

        var release = await service.GetLatestReleaseAsync();

        Assert.NotNull(release);
        Assert.Equal("v1.4.2", release.TagName);
        Assert.Equal("QuickMangaTool v1.4.2", release.Name);
        Assert.Equal("https://github.com/quyentruong/QuickMangaTool/releases/tag/v1.4.2", release.HtmlUrl);
        Assert.Equal(2, release.Assets.Count);
        Assert.Equal("QuickMangaTool-linux-x64", release.Assets[0].Name);
        Assert.Equal(42000000, release.Assets[0].Size);
        Assert.Equal("QuickMangaTool-win-x64.exe", release.Assets[1].Name);
        Assert.Equal(45000000, release.Assets[1].Size);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
            => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_responder(request));
    }
}
