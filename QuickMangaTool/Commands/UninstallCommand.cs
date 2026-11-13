using System.Diagnostics;
using Spectre.Console;

/// <summary>
/// Interactive-only command that completely uninstalls QuickMangaTool by removing
/// configuration, cache, logs, and the running executable binary.
/// </summary>
class UninstallCommand : IAppCommand
{
    private const string AppFolderName = "quickmangatool";

    private readonly bool _confirm;
    private readonly string _configDir;
    private readonly string _cacheDir;
    private readonly string _logsDir;
    private readonly bool _usesDefaultCache;
    private readonly string? _currentExecutablePath;
    private readonly TextWriter? _output;
    private readonly TextReader? _input;

    public UninstallCommand(
        bool confirm = false,
        string? configDir = null,
        string? cacheDir = null,
        string? logsDir = null,
        string? currentExecutablePath = null,
        TextWriter? output = null,
        TextReader? input = null)
    {
        _confirm = confirm;
        _usesDefaultCache = string.IsNullOrWhiteSpace(cacheDir);
        _configDir = string.IsNullOrWhiteSpace(configDir) ? AppPaths.GetConfigDirectory() : Path.GetFullPath(configDir);
        _cacheDir = _usesDefaultCache ? AppPaths.GetCacheDirectory() : Path.GetFullPath(cacheDir!);
        _logsDir = string.IsNullOrWhiteSpace(logsDir) ? AppPaths.GetLogsDirectory() : Path.GetFullPath(logsDir);
        _currentExecutablePath = currentExecutablePath ?? Environment.ProcessPath;
        _output = output;
        _input = input;
    }

    public int Execute(IReadOnlyList<string> cbzFiles, IRunLogger logger)
    {
        var writer = _output ?? Console.Out;
        var exeDisplay = string.IsNullOrWhiteSpace(_currentExecutablePath) ? "(không xác định)" : _currentExecutablePath;

        writer.WriteLine("=== GỠ CÀI ĐẶT QUICKMANGATOOL (UNINSTALL) ===");
        writer.WriteLine("Các dữ liệu và tệp tin sau đây sẽ bị xóa vĩnh viễn:");
        writer.WriteLine($"  - Config     : {_configDir}");
        writer.WriteLine($"  - Cache      : {_cacheDir}");
        writer.WriteLine($"  - Logs       : {_logsDir}");
        writer.WriteLine($"  - Executable : {exeDisplay}");

        if (!_confirm)
        {
            bool proceed;
            if (_input is not null || Console.IsInputRedirected || _output is not null)
            {
                writer.Write("Xác nhận xóa vĩnh viễn toàn bộ dữ liệu và gỡ cài đặt? (y/N): ");
                var answer = (_input ?? Console.In).ReadLine();
                proceed = string.Equals(answer?.Trim(), "y", StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                proceed = AnsiConsole.Confirm("[bold red]Xác nhận xóa vĩnh viễn toàn bộ dữ liệu và gỡ cài đặt?[/]", false);
            }

            if (!proceed)
            {
                writer.WriteLine("Đã hủy gỡ cài đặt.");
                return 0;
            }
        }

        int failed = 0;

        if (_usesDefaultCache)
            SkipCache.Clear();

        failed += DeleteDirectoryWithEmptyAppParent(_configDir, "Config", writer);
        failed += DeleteDirectoryWithEmptyAppParent(_cacheDir, "Cache", writer);
        failed += DeleteDirectoryWithEmptyAppParent(_logsDir, "Logs", writer);
        failed += DeleteExecutable(writer);

        if (failed == 0)
            writer.WriteLine("Đã gỡ cài đặt QuickMangaTool hoàn tất.");
        else
            writer.WriteLine($"Quá trình gỡ cài đặt hoàn tất với {failed} lỗi.");

        return failed > 0 ? 1 : 0;
    }

    private static int DeleteDirectoryWithEmptyAppParent(string directoryPath, string label, TextWriter writer)
    {
        try
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, recursive: true);
                writer.WriteLine($"Đã xóa {label}: {directoryPath}");
            }
            else
            {
                writer.WriteLine($"Không tìm thấy thư mục {label}: {directoryPath}");
            }

            TryDeleteEmptyAppParentDirectory(directoryPath, writer);
            return 0;
        }
        catch (Exception ex)
        {
            writer.WriteLine($"Lỗi khi xóa {label} ({directoryPath}): {ex.Message}");
            return 1;
        }
    }

    private static void TryDeleteEmptyAppParentDirectory(string childDirectoryPath, TextWriter writer)
    {
        try
        {
            var trimmedChild = childDirectoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var parentDir = Path.GetDirectoryName(trimmedChild);
            if (string.IsNullOrWhiteSpace(parentDir) || !Directory.Exists(parentDir))
                return;

            var parentName = Path.GetFileName(parentDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (!string.Equals(parentName, AppFolderName, StringComparison.OrdinalIgnoreCase))
                return;

            if (!Directory.EnumerateFileSystemEntries(parentDir).Any())
            {
                Directory.Delete(parentDir, recursive: false);
                writer.WriteLine($"Đã xóa thư mục cha rỗng: {parentDir}");
            }
        }
        catch
        {
            // Best-effort cleanup of empty parent folder
        }
    }

    private int DeleteExecutable(TextWriter writer)
    {
        if (!UpdateCommand.IsValidTargetExecutable(_currentExecutablePath, out var invalidReason))
        {
            writer.WriteLine($"Lưu ý: Bỏ qua xóa file thực thi ({invalidReason})");
            return 0;
        }

        var fullTargetPath = Path.GetFullPath(_currentExecutablePath!);

        DeleteCompanionFileIfExists(fullTargetPath + ".old");
        DeleteCompanionFileIfExists(fullTargetPath + ".update.tmp");
        DeleteCompanionFileIfExists(fullTargetPath + ".update.zip");

        try
        {
            File.Delete(fullTargetPath);
            writer.WriteLine($"Đã xóa file thực thi: {fullTargetPath}");
            return 0;
        }
        catch (Exception ex) when (OperatingSystem.IsWindows() && (ex is IOException or UnauthorizedAccessException))
        {
            if (ScheduleWindowsSelfDelete(fullTargetPath, writer))
                return 0;

            writer.WriteLine($"Lỗi khi xóa file thực thi ({fullTargetPath}): {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            writer.WriteLine($"Lỗi khi xóa file thực thi ({fullTargetPath}): {ex.Message}");
            return 1;
        }
    }

    private static void DeleteCompanionFileIfExists(string path)
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

    private static bool ScheduleWindowsSelfDelete(string fullTargetPath, TextWriter writer)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/C ping 127.0.0.1 -n 2 > NUL & del /f /q \"{fullTargetPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process is null)
                return false;

            writer.WriteLine($"Đã lên lịch tự xóa file thực thi sau khi ứng dụng đóng: {fullTargetPath}");
            return true;
        }
        catch
        {
            return false;
        }
    }
}
