using System.Buffers;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

public sealed record GitHubReleaseAsset(string Name, string DownloadUrl, long Size);

public sealed record GitHubReleaseInfo(
    string TagName,
    string Name,
    string HtmlUrl,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<GitHubReleaseAsset> Assets);

public interface IGitHubReleaseService : IDisposable
{
    Task<GitHubReleaseInfo?> GetLatestReleaseAsync(CancellationToken cancellationToken = default);

    Task DownloadAssetAsync(
        string downloadUrl,
        Stream destination,
        Action<long, long?>? onProgress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Queries GitHub Releases API using trim-safe <see cref="JsonDocument"/> parsing and streams asset downloads.
/// </summary>
public sealed class GitHubReleaseService : IGitHubReleaseService
{
    public const string DefaultRepo = "quyentruong/QuickMangaTool";
    private const int DownloadBufferSize = 81920;

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly string _repo;

    public GitHubReleaseService(HttpClient? httpClient = null, string repo = DefaultRepo)
    {
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        _repo = string.IsNullOrWhiteSpace(repo) ? DefaultRepo : repo;
    }

    public async Task<GitHubReleaseInfo?> GetLatestReleaseAsync(CancellationToken cancellationToken = default)
    {
        var url = $"https://api.github.com/repos/{_repo}/releases/latest";
        using var request = CreateRequest(HttpMethod.Get, url, acceptJson: true);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await ParseReleaseJsonAsync(stream, cancellationToken);
    }

    internal static async Task<GitHubReleaseInfo?> ParseReleaseJsonAsync(Stream jsonStream, CancellationToken cancellationToken = default)
    {
        using var document = await JsonDocument.ParseAsync(jsonStream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            return null;

        var tagName = GetStringProperty(root, "tag_name");
        if (string.IsNullOrWhiteSpace(tagName))
            return null;

        var name = GetStringProperty(root, "name") ?? tagName;
        var htmlUrl = GetStringProperty(root, "html_url") ?? string.Empty;

        DateTimeOffset? publishedAt = null;
        if (root.TryGetProperty("published_at", out var pubElem) &&
            pubElem.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(pubElem.GetString(), out var parsedDate))
        {
            publishedAt = parsedDate;
        }

        var assets = new List<GitHubReleaseAsset>();
        if (root.TryGetProperty("assets", out var assetsElem) && assetsElem.ValueKind == JsonValueKind.Array)
        {
            foreach (var assetElem in assetsElem.EnumerateArray())
            {
                if (assetElem.ValueKind != JsonValueKind.Object)
                    continue;

                var assetName = GetStringProperty(assetElem, "name");
                var downloadUrl = GetStringProperty(assetElem, "browser_download_url");
                long size = 0;
                if (assetElem.TryGetProperty("size", out var sizeElem) && sizeElem.ValueKind == JsonValueKind.Number)
                    sizeElem.TryGetInt64(out size);

                if (!string.IsNullOrWhiteSpace(assetName) && !string.IsNullOrWhiteSpace(downloadUrl))
                    assets.Add(new GitHubReleaseAsset(assetName, downloadUrl, size));
            }
        }

        return new GitHubReleaseInfo(tagName, name, htmlUrl, publishedAt, assets);
    }

    public async Task DownloadAssetAsync(
        string downloadUrl,
        Stream destination,
        Action<long, long?>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(downloadUrl);
        ArgumentNullException.ThrowIfNull(destination);

        using var request = CreateRequest(HttpMethod.Get, downloadUrl, acceptJson: false);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);

        var buffer = ArrayPool<byte>.Shared.Rent(DownloadBufferSize);
        long totalRead = 0;
        try
        {
            int bytesRead;
            while ((bytesRead = await source.ReadAsync(buffer.AsMemory(0, DownloadBufferSize), cancellationToken)) > 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                totalRead += bytesRead;
                onProgress?.Invoke(totalRead, totalBytes);
            }

            await destination.FlushAsync(cancellationToken);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, bool acceptJson)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("QuickMangaTool", AppVersionHelper.GetCurrentVersionString()));
        if (acceptJson)
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        return request;
    }

    private static string? GetStringProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
            return prop.GetString();

        return null;
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
            _httpClient.Dispose();
    }
}
