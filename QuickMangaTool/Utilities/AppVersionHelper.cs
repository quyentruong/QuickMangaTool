using System.Reflection;

/// <summary>
/// Helpers for reading, comparing, and cleaning up application version binaries.
/// </summary>
public static class AppVersionHelper
{
    public static string GetCurrentVersionString()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(AppVersionHelper).Assembly;

        var infoVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(infoVersion))
        {
            var cleaned = infoVersion.Split('+', 2)[0].Trim().TrimStart('v', 'V');
            if (!string.IsNullOrWhiteSpace(cleaned))
                return cleaned;
        }

        var assemblyVersion = assembly.GetName().Version;
        if (assemblyVersion is not null)
            return assemblyVersion.ToString(3);

        return "1.0.0";
    }

    public static bool TryParseVersion(string? rawVersion, out Version version)
    {
        version = new Version(0, 0, 0, 0);
        if (string.IsNullOrWhiteSpace(rawVersion))
            return false;

        var normalized = rawVersion.Trim().TrimStart('v', 'V');
        var plusIndex = normalized.IndexOf('+');
        if (plusIndex >= 0)
            normalized = normalized[..plusIndex];

        var dashIndex = normalized.IndexOf('-');
        if (dashIndex >= 0)
            normalized = normalized[..dashIndex];

        if (!Version.TryParse(normalized, out var parsed) || parsed is null)
            return false;

        version = new Version(
            Math.Max(0, parsed.Major),
            Math.Max(0, parsed.Minor),
            Math.Max(0, parsed.Build),
            Math.Max(0, parsed.Revision));

        return true;
    }

    public static bool IsNewerVersion(string currentVersionRaw, string latestTagRaw)
    {
        if (!TryParseVersion(currentVersionRaw, out var current))
            return false;

        if (!TryParseVersion(latestTagRaw, out var latest))
            return false;

        return latest > current;
    }

    public static IReadOnlyList<string> GetExpectedAssetNames()
    {
        if (OperatingSystem.IsWindows())
            return new[] { "QuickMangaTool-win-x64.exe", "QuickMangaTool-win-x64.zip" };

        if (OperatingSystem.IsLinux())
            return new[] { "QuickMangaTool-linux-x64", "QuickMangaTool-linux-x64.zip" };

        return Array.Empty<string>();
    }

    public static void CleanupOldExecutableIfPresent(string? processPath = null)
    {
        var path = processPath ?? Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(path))
            return;

        var oldPath = path + ".old";
        try
        {
            if (File.Exists(oldPath))
                File.Delete(oldPath);
        }
        catch
        {
            // Ignore cleanup failures (e.g., file still locked by OS or permissions)
        }
    }
}
