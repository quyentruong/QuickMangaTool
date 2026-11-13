using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Simple persistent cache of archives that previously had "No change".
/// Keys: full file path -> last-write-time UTC ticks recorded when cache entry was created.
/// An entry is considered valid only if the file's current LastWriteTimeUtc matches the stored ticks.
/// </summary>
static class SkipCache
{
    private static readonly object _lock = new object();
    private static string? _loadedPath;
    private static Dictionary<string, long>? _entries;
    private static bool _isDirty;
    private static int _pendingWrites;

    private static string CachePath => AppPaths.GetSkipCacheFilePath();

    private static void EnsureLoaded()
    {
        var currentPath = CachePath;
        if (_entries != null && string.Equals(_loadedPath, currentPath, StringComparison.Ordinal))
            return;
        lock (_lock)
        {
            currentPath = CachePath;
            if (_entries != null && string.Equals(_loadedPath, currentPath, StringComparison.Ordinal))
                return;
            _loadedPath = currentPath;
            _isDirty = false;
            _pendingWrites = 0;
            if (File.Exists(currentPath))
            {
                try
                {
                    _entries = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                    using var fs = File.OpenRead(currentPath);
                    using var br = new BinaryReader(fs);
                    while (fs.Position < fs.Length)
                    {
                        var pathLen = br.ReadInt32();
                        var pathBytes = br.ReadBytes(pathLen);
                        var path = System.Text.Encoding.UTF8.GetString(pathBytes);
                        var ticks = br.ReadInt64();
                        _entries[path] = ticks;
                    }
                }
                catch
                {
                    _entries = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                }
            }
            else
            {
                _entries = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    private static void Save()
    {
        EnsureLoaded();
        lock (_lock)
        {
            var currentPath = CachePath;
            var tempPath = currentPath + ".tmp";
            try
            {
                using (var fs = File.Create(tempPath))
                using (var bw = new BinaryWriter(fs))
                {
                    var entries = _entries!;
                    foreach (var kv in entries)
                    {
                        var pathBytes = System.Text.Encoding.UTF8.GetBytes(kv.Key);
                        bw.Write(pathBytes.Length);
                        bw.Write(pathBytes);
                        bw.Write(kv.Value);
                    }
                    bw.Flush();
                }

                File.Move(tempPath, currentPath, overwrite: true);
                _isDirty = false;
                _pendingWrites = 0;
            }
            catch
            {
                try
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
                catch { /* ignore */ }
            }
        }
    }

    public static bool ShouldSkip(string fullPath)
    {
        try
        {
            EnsureLoaded();
            lock (_lock)
            {
                if (!_entries!.TryGetValue(fullPath, out var storedTicks))
                    return false;

                if (!File.Exists(fullPath))
                {
                    _entries.Remove(fullPath);
                    _isDirty = true;
                    return false;
                }

                var current = File.GetLastWriteTimeUtc(fullPath).Ticks;
                if (current == storedTicks)
                    return true;

                _entries.Remove(fullPath);
                _isDirty = true;
                return false;
            }
        }
        catch
        {
            return false;
        }
    }

    public static void MarkNoChange(string fullPath, bool flushImmediately = true)
    {
        try
        {
            EnsureLoaded();
            lock (_lock)
            {
                if (!File.Exists(fullPath))
                    return;
                var ticks = File.GetLastWriteTimeUtc(fullPath).Ticks;
                _entries![fullPath] = ticks;
                _isDirty = true;
                _pendingWrites++;

                if (flushImmediately || _pendingWrites >= 100)
                    Save();
            }
        }
        catch
        {
            // ignore
        }
    }

    /// <summary>
    /// Persists pending cache changes to disk if modified.
    /// </summary>
    public static void Flush()
    {
        lock (_lock)
        {
            if (_isDirty)
                Save();
        }
    }

    /// <summary>
    /// Clears the on-disk cache and in-memory entries.
    /// </summary>
    public static void Clear()
    {
        lock (_lock)
        {
            try
            {
                _entries = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                _isDirty = false;
                _pendingWrites = 0;
                var currentPath = CachePath;
                if (File.Exists(currentPath))
                    File.Delete(currentPath);
            }
            catch
            {
                // ignore failures
            }
        }
    }
}
