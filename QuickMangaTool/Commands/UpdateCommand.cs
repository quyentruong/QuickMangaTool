using System.IO.Compression;
using Spectre.Console;

/// <summary>
/// Checks GitHub Releases for a newer version of QuickMangaTool and safely self-updates the running binary.
/// </summary>
class UpdateCommand : IAppCommand
{
    private readonly bool _confirm;
    private readonly IGitHubReleaseService? _releaseService;
    private readonly string? _currentExecutablePath;
    private readonly string? _currentVersion;
    private readonly TextWriter? _output;

    public UpdateCommand(
        bool confirm,
        IGitHubReleaseService? releaseService = null,
        string? currentExecutablePath = null,
        string? currentVersion = null,
        TextWriter? output = null)
    {
        _confirm = confirm;
        _releaseService = releaseService;
        _currentExecutablePath = currentExecutablePath;
        _currentVersion = currentVersion;
        _output = output;
    }

    public int Execute(IReadOnlyList<string> cbzFiles, IRunLogger logger)
    {
        return ExecuteAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    public async Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var writer = _output ?? Console.Out;
        var currentVersion = _currentVersion ?? AppVersionHelper.GetCurrentVersionString();

        var ownsService = _releaseService is null;
        var releaseService = _releaseService ?? new GitHubReleaseService();

        try
        {
            writer.WriteLine($"Phiên bản hiện tại: v{currentVersion}");
            writer.WriteLine("Đang kiểm tra bản cập nhật mới từ GitHub Releases...");

            GitHubReleaseInfo? release;
            try
            {
                release = await releaseService.GetLatestReleaseAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                writer.WriteLine($"Không thể kiểm tra cập nhật từ GitHub: {ex.Message}");
                return 1;
            }

            if (release is null)
            {
                writer.WriteLine("Chưa có bản phát hành (Release) nào trên GitHub hoặc không thể tìm thấy bản phát hành mới nhất.");
                return 0;
            }

            if (!AppVersionHelper.IsNewerVersion(currentVersion, release.TagName))
            {
                writer.WriteLine($"Bạn đang sử dụng phiên bản mới nhất (v{currentVersion}).");
                return 0;
            }

            var expectedNames = AppVersionHelper.GetExpectedAssetNames();
            var matchedAsset = FindMatchingAsset(release.Assets, expectedNames);

            if (matchedAsset is null)
            {
                writer.WriteLine($"Tìm thấy phiên bản mới {release.TagName}, nhưng không có gói cài đặt phù hợp cho hệ điều hành hiện tại.");
                if (release.Assets.Count > 0)
                    writer.WriteLine($"Assets hiện có: {string.Join(", ", release.Assets.Select(a => a.Name))}");
                if (!string.IsNullOrWhiteSpace(release.HtmlUrl))
                    writer.WriteLine($"Vui lòng tải thủ công tại: {release.HtmlUrl}");
                return 1;
            }

            var sizeLabel = matchedAsset.Size > 0 ? SizeFormatter.FormatSize(matchedAsset.Size) : "không rõ dung lượng";
            writer.WriteLine($"Phát hiện phiên bản mới: {release.TagName} ({matchedAsset.Name} - {sizeLabel})");
            if (!string.IsNullOrWhiteSpace(release.HtmlUrl))
                writer.WriteLine($"Chi tiết: {release.HtmlUrl}");

            if (!_confirm && !Console.IsInputRedirected)
            {
                bool proceed = AnsiConsole.Confirm("Bạn có muốn tải về và cập nhật ngay không?", true);
                if (!proceed)
                {
                    writer.WriteLine("Đã hủy cập nhật.");
                    return 0;
                }
            }

            var targetExePath = _currentExecutablePath ?? Environment.ProcessPath;
            if (!IsValidTargetExecutable(targetExePath, out var invalidReason))
            {
                writer.WriteLine(invalidReason);
                return 1;
            }

            var fullTargetPath = Path.GetFullPath(targetExePath!);
            var tempBinaryPath = fullTargetPath + ".update.tmp";
            var isZipAsset = matchedAsset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
            var downloadPath = isZipAsset ? fullTargetPath + ".update.zip" : tempBinaryPath;

            try
            {
                CleanupTempFile(tempBinaryPath);
                if (isZipAsset)
                    CleanupTempFile(downloadPath);

                await DownloadWithOptionalProgressAsync(releaseService, matchedAsset, downloadPath, writer, cancellationToken);

                if (isZipAsset)
                {
                    ExtractExecutableFromZip(downloadPath, tempBinaryPath);
                    CleanupTempFile(downloadPath);
                }

                var fileInfo = new FileInfo(tempBinaryPath);
                if (!fileInfo.Exists || fileInfo.Length == 0)
                {
                    writer.WriteLine("File tải về bị rỗng hoặc không tồn tại. Đã hủy cập nhật.");
                    CleanupTempFile(tempBinaryPath);
                    return 1;
                }

                if (!OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(
                        tempBinaryPath,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                        UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
                }

                ReplaceExecutableSafely(fullTargetPath, tempBinaryPath);

                writer.WriteLine($"Cập nhật thành công lên phiên bản {release.TagName}! Vui lòng khởi động lại ứng dụng để áp dụng.");
                return 0;
            }
            catch (Exception ex)
            {
                CleanupTempFile(tempBinaryPath);
                if (isZipAsset)
                    CleanupTempFile(downloadPath);

                writer.WriteLine($"Lỗi trong quá trình cập nhật: {ex.Message}");
                return 1;
            }
        }
        finally
        {
            if (ownsService)
                releaseService.Dispose();
        }
    }

    private static GitHubReleaseAsset? FindMatchingAsset(
        IReadOnlyList<GitHubReleaseAsset> assets,
        IReadOnlyList<string> expectedNames)
    {
        foreach (var expected in expectedNames)
        {
            var match = assets.FirstOrDefault(a => string.Equals(a.Name, expected, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                return match;
        }

        return null;
    }

    internal static bool IsValidTargetExecutable(string? executablePath, out string reason)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            reason = "Không thể xác định đường dẫn file thực thi hiện tại để tự cập nhật.";
            return false;
        }

        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(executablePath);
        if (string.Equals(fileNameWithoutExt, "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            reason = "Ứng dụng đang chạy qua tiến trình `dotnet` (môi trường dev/DLL). Không thể ghi đè file thực thi `dotnet` của hệ thống; vui lòng dùng bản binary đã publish để tự cập nhật.";
            return false;
        }

        if (!File.Exists(executablePath))
        {
            reason = $"Không tìm thấy file thực thi mục tiêu: {executablePath}";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private async Task DownloadWithOptionalProgressAsync(
        IGitHubReleaseService releaseService,
        GitHubReleaseAsset asset,
        string destinationPath,
        TextWriter writer,
        CancellationToken cancellationToken)
    {
        bool useSpectreProgress = _output is null && !Console.IsOutputRedirected;

        if (!useSpectreProgress)
        {
            writer.WriteLine($"Đang tải {asset.Name}...");
            await using var fs = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            await releaseService.DownloadAssetAsync(asset.DownloadUrl, fs, null, cancellationToken);
            return;
        }

        await AnsiConsole.Progress()
            .AutoClear(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new DownloadedColumn(),
                new TransferSpeedColumn())
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask($"[cyan]Đang tải {Markup.Escape(asset.Name)}[/]", maxValue: asset.Size > 0 ? asset.Size : 100);
                await using var fs = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
                await releaseService.DownloadAssetAsync(
                    asset.DownloadUrl,
                    fs,
                    (read, total) =>
                    {
                        var effectiveTotal = total ?? asset.Size;
                        if (effectiveTotal > 0)
                        {
                            task.MaxValue = effectiveTotal;
                            task.Value = Math.Min(read, effectiveTotal);
                        }
                        else
                        {
                            task.IsIndeterminate = true;
                        }
                    },
                    cancellationToken);

                if (!task.IsIndeterminate)
                    task.Value = task.MaxValue;
            });
    }

    private static void ExtractExecutableFromZip(string zipPath, string destinationBinaryPath)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        var entry = archive.Entries
            .Where(e => !string.IsNullOrEmpty(e.Name))
            .OrderByDescending(e =>
                e.Name.Equals("QuickMangaTool.exe", StringComparison.OrdinalIgnoreCase) ||
                e.Name.Equals("QuickMangaTool", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(e => e.Length)
            .FirstOrDefault()
            ?? throw new InvalidDataException("Gói nén .zip không chứa file thực thi hợp lệ.");

        using var entryStream = entry.Open();
        using var outputStream = new FileStream(destinationBinaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920);
        entryStream.CopyTo(outputStream);
        outputStream.Flush();
    }

    internal static void ReplaceExecutableSafely(string targetExePath, string tempBinaryPath)
    {
        var backupPath = targetExePath + ".old";

        try
        {
            if (File.Exists(backupPath))
                File.Delete(backupPath);
        }
        catch
        {
            // Ignore if old backup cannot be deleted prior to overwrite
        }

        File.Move(targetExePath, backupPath, overwrite: true);

        try
        {
            File.Move(tempBinaryPath, targetExePath, overwrite: true);
        }
        catch
        {
            // Roll back original executable if moving the new binary fails
            if (!File.Exists(targetExePath) && File.Exists(backupPath))
            {
                try
                {
                    File.Move(backupPath, targetExePath, overwrite: true);
                }
                catch
                {
                    // Best-effort rollback
                }
            }

            throw;
        }

        try
        {
            if (File.Exists(backupPath))
                File.Delete(backupPath);
        }
        catch
        {
            // On Windows, the running .old executable remains locked until process exit;
            // AppVersionHelper.CleanupOldExecutableIfPresent removes it on next startup.
        }
    }

    private static void CleanupTempFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup
        }
    }
}
