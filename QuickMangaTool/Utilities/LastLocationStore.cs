public static class LastLocationStore
{
    private static readonly object Lock = new();
    private static string StoragePath => AppPaths.GetLastLocationFilePath();

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
