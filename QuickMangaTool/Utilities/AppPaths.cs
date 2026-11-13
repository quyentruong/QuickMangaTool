using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// Centralized OS-standard storage path resolution and log retention management for QuickMangaTool.
/// </summary>
public static class AppPaths
{
    public const string ConfigDirEnvVar = "QUICKMANGATOOL_CONFIG_DIR";
    public const string CacheDirEnvVar = "QUICKMANGATOOL_CACHE_DIR";
    public const string LogDirEnvVar = "QUICKMANGATOOL_LOG_DIR";

    private const string UnixAppFolderName = "quickmangatool";
    private const string WindowsAppFolderName = "QuickMangaTool";

    /// <summary>
    /// Gets the directory used for persistent user configuration files.
    /// </summary>
    public static string GetConfigDirectory()
    {
        var custom = Environment.GetEnvironmentVariable(ConfigDirEnvVar);
        if (!string.IsNullOrWhiteSpace(custom))
            return EnsureDirectory(custom.Trim(), "config");

        string dir;
        if (OperatingSystem.IsWindows())
        {
            dir = Path.Combine(GetWindowsLocalAppData(), WindowsAppFolderName, "Config");
        }
        else
        {
            var xdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            var baseDir = !string.IsNullOrWhiteSpace(xdgConfig)
                ? xdgConfig.Trim()
                : Path.Combine(GetUserHome(), ".config");
            dir = Path.Combine(baseDir, UnixAppFolderName);
        }

        return EnsureDirectory(dir, "config");
    }

    /// <summary>
    /// Gets the directory used for non-essential cached data such as skip cache.
    /// </summary>
    public static string GetCacheDirectory()
    {
        var custom = Environment.GetEnvironmentVariable(CacheDirEnvVar);
        if (!string.IsNullOrWhiteSpace(custom))
            return EnsureDirectory(custom.Trim(), "cache");

        string dir;
        if (OperatingSystem.IsWindows())
        {
            dir = Path.Combine(GetWindowsLocalAppData(), WindowsAppFolderName, "Cache");
        }
        else
        {
            var xdgCache = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
            var baseDir = !string.IsNullOrWhiteSpace(xdgCache)
                ? xdgCache.Trim()
                : Path.Combine(GetUserHome(), ".cache");
            dir = Path.Combine(baseDir, UnixAppFolderName);
        }

        return EnsureDirectory(dir, "cache");
    }

    /// <summary>
    /// Gets the centralized directory used for execution log files.
    /// </summary>
    public static string GetLogsDirectory()
    {
        var custom = Environment.GetEnvironmentVariable(LogDirEnvVar);
        if (!string.IsNullOrWhiteSpace(custom))
            return EnsureDirectory(custom.Trim(), "logs");

        string dir;
        if (OperatingSystem.IsWindows())
        {
            dir = Path.Combine(GetWindowsLocalAppData(), WindowsAppFolderName, "Logs");
        }
        else
        {
            var xdgState = Environment.GetEnvironmentVariable("XDG_STATE_HOME");
            var baseDir = !string.IsNullOrWhiteSpace(xdgState)
                ? xdgState.Trim()
                : Path.Combine(GetUserHome(), ".local", "state");
            dir = Path.Combine(baseDir, UnixAppFolderName, "logs");
        }

        return EnsureDirectory(dir, "logs");
    }

    /// <summary>
    /// Gets the full path to the skip cache file.
    /// </summary>
    public static string GetSkipCacheFilePath()
        => Path.Combine(GetCacheDirectory(), "skipcache.dat");

    /// <summary>
    /// Gets the full path to the last working directory config file.
    /// </summary>
    public static string GetLastLocationFilePath()
        => Path.Combine(GetConfigDirectory(), "last_directory.txt");

    /// <summary>
    /// Deletes older log files beyond the most recent <paramref name="maxLogs"/> entries.
    /// </summary>
    public static int PruneOldLogs(int maxLogs = 50, string? logsDirectory = null)
    {
        if (maxLogs <= 0)
            return 0;

        var dir = string.IsNullOrWhiteSpace(logsDirectory) ? GetLogsDirectory() : logsDirectory;
        if (!Directory.Exists(dir))
            return 0;

        int deleted = 0;
        try
        {
            var filesToDelete = Directory.EnumerateFiles(dir, "*.log", SearchOption.TopDirectoryOnly)
                .OrderByDescending(f =>
                {
                    try { return File.GetLastWriteTimeUtc(f); }
                    catch { return DateTime.MinValue; }
                })
                .ThenByDescending(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .Skip(maxLogs);

            foreach (var file in filesToDelete)
            {
                try
                {
                    File.Delete(file);
                    deleted++;
                }
                catch
                {
                    // Best-effort cleanup
                }
            }
        }
        catch
        {
            // Best-effort cleanup
        }

        return deleted;
    }

    private static string GetUserHome()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
            home = Environment.GetEnvironmentVariable("HOME") ?? string.Empty;

        return string.IsNullOrWhiteSpace(home) ? Path.GetTempPath() : home;
    }

    private static string GetWindowsLocalAppData()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
            localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(localAppData))
            return localAppData;

        var home = GetUserHome();
        return string.Equals(home, Path.GetTempPath(), StringComparison.OrdinalIgnoreCase)
            ? home
            : Path.Combine(home, "AppData", "Local");
    }

    private static string EnsureDirectory(string primaryPath, string fallbackSubdir)
    {
        try
        {
            var fullPath = Path.GetFullPath(primaryPath);
            if (!Directory.Exists(fullPath))
                Directory.CreateDirectory(fullPath);

            return fullPath;
        }
        catch
        {
            var fallback = Path.Combine(Path.GetTempPath(), UnixAppFolderName, fallbackSubdir);
            try
            {
                if (!Directory.Exists(fallback))
                    Directory.CreateDirectory(fallback);
            }
            catch
            {
                // Best-effort directory creation
            }

            return fallback;
        }
    }
}
