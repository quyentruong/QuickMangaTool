namespace QuickMangaTool.Tests.Commands;

public class UninstallCommandTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _appRoot;
    private readonly string _configDir;
    private readonly string _cacheDir;
    private readonly string _logsDir;
    private readonly string _exePath;

    public UninstallCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "qmt-uninstall-tests-" + Path.GetRandomFileName());
        _appRoot = Path.Combine(_tempDir, "quickmangatool");
        _configDir = Path.Combine(_appRoot, "config");
        _cacheDir = Path.Combine(_appRoot, "cache");
        _logsDir = Path.Combine(_appRoot, "logs");
        _exePath = Path.Combine(_tempDir, OperatingSystem.IsWindows() ? "QuickMangaTool.exe" : "QuickMangaTool");

        Directory.CreateDirectory(_configDir);
        Directory.CreateDirectory(_cacheDir);
        Directory.CreateDirectory(_logsDir);

        File.WriteAllText(Path.Combine(_configDir, "last_directory.txt"), "/some/path");
        File.WriteAllText(Path.Combine(_cacheDir, "skipcache.dat"), "cache-bytes");
        File.WriteAllText(Path.Combine(_logsDir, "QuickMangaTool_2026-01-01_00-00-00.log"), "log line");
        File.WriteAllText(_exePath, "fake-executable");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // Best-effort test cleanup
        }
    }

    [Fact]
    public void Execute_WithConfirmTrue_DeletesConfigCacheLogsAndExecutable()
    {
        using var output = new StringWriter();
        var cmd = new UninstallCommand(
            confirm: true,
            configDir: _configDir,
            cacheDir: _cacheDir,
            logsDir: _logsDir,
            currentExecutablePath: _exePath,
            output: output);

        var exitCode = cmd.Execute(Array.Empty<string>(), new NoopRunLogger());

        Assert.Equal(0, exitCode);
        Assert.False(Directory.Exists(_configDir));
        Assert.False(Directory.Exists(_cacheDir));
        Assert.False(Directory.Exists(_logsDir));
        Assert.False(File.Exists(_exePath));
        Assert.Contains("hoàn tất", output.ToString());
    }

    [Fact]
    public void Execute_WithConfirmFalse_WhenUserDeclines_DeletesNothing()
    {
        using var output = new StringWriter();
        using var input = new StringReader("n\n");
        var cmd = new UninstallCommand(
            confirm: false,
            configDir: _configDir,
            cacheDir: _cacheDir,
            logsDir: _logsDir,
            currentExecutablePath: _exePath,
            output: output,
            input: input);

        var exitCode = cmd.Execute(Array.Empty<string>(), new NoopRunLogger());

        Assert.Equal(0, exitCode);
        Assert.True(Directory.Exists(_configDir));
        Assert.True(Directory.Exists(_cacheDir));
        Assert.True(Directory.Exists(_logsDir));
        Assert.True(File.Exists(_exePath));
        Assert.Contains("Đã hủy gỡ cài đặt", output.ToString());
    }

    [Fact]
    public void Execute_WithConfirmFalse_WhenUserConfirmsWithY_DeletesAll()
    {
        using var output = new StringWriter();
        using var input = new StringReader("y\n");
        var cmd = new UninstallCommand(
            confirm: false,
            configDir: _configDir,
            cacheDir: _cacheDir,
            logsDir: _logsDir,
            currentExecutablePath: _exePath,
            output: output,
            input: input);

        var exitCode = cmd.Execute(Array.Empty<string>(), new NoopRunLogger());

        Assert.Equal(0, exitCode);
        Assert.False(Directory.Exists(_configDir));
        Assert.False(Directory.Exists(_cacheDir));
        Assert.False(Directory.Exists(_logsDir));
        Assert.False(File.Exists(_exePath));
    }

    [Fact]
    public void Execute_WhenRunningUnderDotnetHost_DeletesDataButPreservesDotnetBinary()
    {
        var dotnetPath = Path.Combine(_tempDir, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
        File.WriteAllText(dotnetPath, "system-dotnet-host");

        using var output = new StringWriter();
        var cmd = new UninstallCommand(
            confirm: true,
            configDir: _configDir,
            cacheDir: _cacheDir,
            logsDir: _logsDir,
            currentExecutablePath: dotnetPath,
            output: output);

        var exitCode = cmd.Execute(Array.Empty<string>(), new NoopRunLogger());

        Assert.Equal(0, exitCode);
        Assert.False(Directory.Exists(_configDir));
        Assert.False(Directory.Exists(_cacheDir));
        Assert.False(Directory.Exists(_logsDir));
        Assert.True(File.Exists(dotnetPath));
        Assert.Equal("system-dotnet-host", File.ReadAllText(dotnetPath));
        Assert.Contains("dotnet", output.ToString());
    }

    [Fact]
    public void Execute_AlsoCleansUpLeftoverUpdateFilesAndEmptyParentDirs()
    {
        var oldExe = _exePath + ".old";
        var tmpExe = _exePath + ".update.tmp";
        var zipExe = _exePath + ".update.zip";
        File.WriteAllText(oldExe, "old");
        File.WriteAllText(tmpExe, "tmp");
        File.WriteAllText(zipExe, "zip");

        using var output = new StringWriter();
        var cmd = new UninstallCommand(
            confirm: true,
            configDir: _configDir,
            cacheDir: _cacheDir,
            logsDir: _logsDir,
            currentExecutablePath: _exePath,
            output: output);

        var exitCode = cmd.Execute(Array.Empty<string>(), new NoopRunLogger());

        Assert.Equal(0, exitCode);
        Assert.False(File.Exists(_exePath));
        Assert.False(File.Exists(oldExe));
        Assert.False(File.Exists(tmpExe));
        Assert.False(File.Exists(zipExe));
        Assert.False(Directory.Exists(_appRoot));
    }
}
