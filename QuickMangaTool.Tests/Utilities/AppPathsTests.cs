namespace QuickMangaTool.Tests.Utilities;

public class AppPathsTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly Dictionary<string, string?> _originalEnv = new();

    private static readonly string[] TrackedEnvVars =
    [
        AppPaths.ConfigDirEnvVar,
        AppPaths.CacheDirEnvVar,
        AppPaths.LogDirEnvVar,
        "XDG_CONFIG_HOME",
        "XDG_CACHE_HOME",
        "XDG_STATE_HOME"
    ];

    public AppPathsTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "QuickMangaTool_AppPathsTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);

        foreach (var name in TrackedEnvVars)
            _originalEnv[name] = Environment.GetEnvironmentVariable(name);
    }

    public void Dispose()
    {
        foreach (var kv in _originalEnv)
            Environment.SetEnvironmentVariable(kv.Key, kv.Value);

        try
        {
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, recursive: true);
        }
        catch
        {
            // Ignore test cleanup errors
        }
    }

    [Fact]
    public void CustomEnvOverrides_ReturnConfiguredDirectoriesAndCreateThem()
    {
        var customConfig = Path.Combine(_tempRoot, "custom-config");
        var customCache = Path.Combine(_tempRoot, "custom-cache");
        var customLogs = Path.Combine(_tempRoot, "custom-logs");

        Environment.SetEnvironmentVariable(AppPaths.ConfigDirEnvVar, customConfig);
        Environment.SetEnvironmentVariable(AppPaths.CacheDirEnvVar, customCache);
        Environment.SetEnvironmentVariable(AppPaths.LogDirEnvVar, customLogs);

        Assert.Equal(Path.GetFullPath(customConfig), AppPaths.GetConfigDirectory());
        Assert.Equal(Path.GetFullPath(customCache), AppPaths.GetCacheDirectory());
        Assert.Equal(Path.GetFullPath(customLogs), AppPaths.GetLogsDirectory());

        Assert.True(Directory.Exists(customConfig));
        Assert.True(Directory.Exists(customCache));
        Assert.True(Directory.Exists(customLogs));

        Assert.Equal(Path.Combine(Path.GetFullPath(customCache), "skipcache.dat"), AppPaths.GetSkipCacheFilePath());
        Assert.Equal(Path.Combine(Path.GetFullPath(customConfig), "last_directory.txt"), AppPaths.GetLastLocationFilePath());
    }

    [Fact]
    public void DefaultPaths_FollowOsStandardLayout()
    {
        foreach (var name in TrackedEnvVars)
            Environment.SetEnvironmentVariable(name, null);

        if (OperatingSystem.IsWindows())
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            Assert.Equal(Path.GetFullPath(Path.Combine(localAppData, "QuickMangaTool", "Config")), AppPaths.GetConfigDirectory());
            Assert.Equal(Path.GetFullPath(Path.Combine(localAppData, "QuickMangaTool", "Cache")), AppPaths.GetCacheDirectory());
            Assert.Equal(Path.GetFullPath(Path.Combine(localAppData, "QuickMangaTool", "Logs")), AppPaths.GetLogsDirectory());
        }
        else
        {
            var xdgConfig = Path.Combine(_tempRoot, "xdg-config");
            var xdgCache = Path.Combine(_tempRoot, "xdg-cache");
            var xdgState = Path.Combine(_tempRoot, "xdg-state");

            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", xdgConfig);
            Environment.SetEnvironmentVariable("XDG_CACHE_HOME", xdgCache);
            Environment.SetEnvironmentVariable("XDG_STATE_HOME", xdgState);

            Assert.Equal(Path.GetFullPath(Path.Combine(xdgConfig, "quickmangatool")), AppPaths.GetConfigDirectory());
            Assert.Equal(Path.GetFullPath(Path.Combine(xdgCache, "quickmangatool")), AppPaths.GetCacheDirectory());
            Assert.Equal(Path.GetFullPath(Path.Combine(xdgState, "quickmangatool", "logs")), AppPaths.GetLogsDirectory());
        }
    }

    [Fact]
    public void PruneOldLogs_KeepsOnlyNewestMaxLogsFiles()
    {
        var logsDir = Path.Combine(_tempRoot, "logs-prune");
        Directory.CreateDirectory(logsDir);

        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (int i = 1; i <= 5; i++)
        {
            var filePath = Path.Combine(logsDir, $"QuickMangaTool_2026-01-0{i}_00-00-00.log");
            File.WriteAllText(filePath, $"log {i}");
            File.SetLastWriteTimeUtc(filePath, baseTime.AddDays(i));
        }

        // Also place a non-log file to ensure it is untouched
        var otherFile = Path.Combine(logsDir, "readme.txt");
        File.WriteAllText(otherFile, "keep");

        int deleted = AppPaths.PruneOldLogs(maxLogs: 2, logsDirectory: logsDir);

        Assert.Equal(3, deleted);
        Assert.True(File.Exists(otherFile));

        var remainingLogs = Directory.GetFiles(logsDir, "*.log")
            .Select(Path.GetFileName)
            .OrderBy(n => n)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "QuickMangaTool_2026-01-04_00-00-00.log",
                "QuickMangaTool_2026-01-05_00-00-00.log"
            },
            remainingLogs);
    }

    [Fact]
    public void PruneOldLogs_WhenCountWithinLimit_DeletesNothing()
    {
        var logsDir = Path.Combine(_tempRoot, "logs-under-limit");
        Directory.CreateDirectory(logsDir);

        File.WriteAllText(Path.Combine(logsDir, "a.log"), "a");
        File.WriteAllText(Path.Combine(logsDir, "b.log"), "b");

        int deleted = AppPaths.PruneOldLogs(maxLogs: 50, logsDirectory: logsDir);

        Assert.Equal(0, deleted);
        Assert.Equal(2, Directory.GetFiles(logsDir, "*.log").Length);
    }
}
