public static class LastLocationStore
{
    private static readonly object Lock = new();
    private static readonly string StoragePath;

    static LastLocationStore()
    {
        string dir;
        var custom = Environment.GetEnvironmentVariable("QUICKMANGATOOL_CACHE_DIR");
        if (!string.IsNullOrWhiteSpace(custom))
        {
            dir = custom;
        }
        else
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            dir = Path.Combine(string.IsNullOrWhiteSpace(home) ? Path.GetTempPath() : home, ".quickmangatool");
        }

        try
        {
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
        catch
        {
            dir = Path.Combine(Path.GetTempPath(), ".quickmangatool");
            try
            {
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
            }
            catch { /* best-effort */ }
        }

        StoragePath = Path.Combine(dir, "last_directory.txt");
    }

    public static string? GetLastDirectory()
    {
        lock (Lock)
        {
            try
            {
                if (!File.Exists(StoragePath))
                    return null;

                var text = File.ReadAllText(StoragePath).Trim();
                if (!string.IsNullOrWhiteSpace(text) && Directory.Exists(text))
                {
                    return Path.GetFullPath(text);
                }
            }
            catch
            {
                // Safe fallback if read fails
            }

            return null;
        }
    }

    public static void SaveLastDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            return;

        lock (Lock)
        {
            try
            {
                var full = Path.GetFullPath(directory);
                if (Directory.Exists(full))
                {
                    var parentDir = Path.GetDirectoryName(StoragePath);
                    if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                        Directory.CreateDirectory(parentDir);

                    File.WriteAllText(StoragePath, full);
                }
            }
            catch
            {
                // Best-effort persistence
            }
        }
    }

    public static void ClearLastDirectory()
    {
        lock (Lock)
        {
            try
            {
                if (File.Exists(StoragePath))
                    File.Delete(StoragePath);
            }
            catch
            {
                // Best-effort cleanup
            }
        }
    }
}
