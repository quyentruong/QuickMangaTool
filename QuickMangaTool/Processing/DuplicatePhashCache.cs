using System.Text;

/// <summary>
/// Persistent cache for pHash values extracted from .cbz archives.
/// Each cache entry is keyed by archive path and last-write timestamp so
/// hashes are reused only when the archive content has not changed.
/// </summary>
static class DuplicatePhashCache
{
    private static readonly object _lock = new();
    private static readonly string _cachePath;
    private static Dictionary<string, ArchiveHashCacheEntry>? _entries;

    static DuplicatePhashCache()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var dir = Path.Combine(home, ".quickmangatool");
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        _cachePath = Path.Combine(dir, "duplicate-phash-cache.dat");
    }

    /// <summary>
    /// Attempts to get cached image hashes for an archive when the file timestamp matches.
    /// </summary>
    public static bool TryGetArchiveHashes(string cbzPath, out List<CachedImageHash> hashes)
    {
        hashes = [];

        try
        {
            EnsureLoaded();

            lock (_lock)
            {
                if (!_entries!.TryGetValue(cbzPath, out var entry))
                    return false;

                if (!File.Exists(cbzPath))
                {
                    _entries.Remove(cbzPath);
                    Save();
                    return false;
                }

                long ticks = File.GetLastWriteTimeUtc(cbzPath).Ticks;
                if (ticks != entry.LastWriteTimeUtcTicks)
                {
                    _entries.Remove(cbzPath);
                    Save();
                    return false;
                }

                hashes = entry.Hashes.ToList();
                return true;
            }
        }
        catch
        {
            return false;
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
                _entries = new Dictionary<string, ArchiveHashCacheEntry>(StringComparer.OrdinalIgnoreCase);
                if (File.Exists(_cachePath))
                    File.Delete(_cachePath);
            }
            catch
            {
                // Best-effort cache cleanup.
            }
        }
    }

    /// <summary>
    /// Stores or updates cached image hashes for an archive.
    /// </summary>
    public static void UpsertArchiveHashes(string cbzPath, IReadOnlyList<CachedImageHash> hashes)
    {
        try
        {
            if (!File.Exists(cbzPath))
                return;

            EnsureLoaded();

            lock (_lock)
            {
                long ticks = File.GetLastWriteTimeUtc(cbzPath).Ticks;
                _entries![cbzPath] = new ArchiveHashCacheEntry(ticks, hashes.ToArray());
                Save();
            }
        }
        catch
        {
            // Best-effort cache persistence.
        }
    }

    private static void EnsureLoaded()
    {
        if (_entries is not null)
            return;

        lock (_lock)
        {
            if (_entries is not null)
                return;

            _entries = new Dictionary<string, ArchiveHashCacheEntry>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(_cachePath))
                return;

            try
            {
                using var fs = File.OpenRead(_cachePath);
                using var reader = new BinaryReader(fs, Encoding.UTF8, leaveOpen: false);

                int version = reader.ReadInt32();
                if (version != 1)
                    return;

                int archiveCount = reader.ReadInt32();
                for (int i = 0; i < archiveCount; i++)
                {
                    string path = reader.ReadString();
                    long ticks = reader.ReadInt64();
                    int hashCount = reader.ReadInt32();
                    var hashes = new CachedImageHash[hashCount];

                    for (int j = 0; j < hashCount; j++)
                    {
                        string entryName = reader.ReadString();
                        ulong hash = reader.ReadUInt64();
                        hashes[j] = new CachedImageHash(entryName, hash);
                    }

                    _entries[path] = new ArchiveHashCacheEntry(ticks, hashes);
                }
            }
            catch
            {
                _entries = new Dictionary<string, ArchiveHashCacheEntry>(StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    private static void Save()
    {
        EnsureLoaded();

        lock (_lock)
        {
            try
            {
                using var fs = File.Create(_cachePath);
                using var writer = new BinaryWriter(fs, Encoding.UTF8, leaveOpen: false);

                writer.Write(1); // format version
                writer.Write(_entries!.Count);

                foreach (var kvp in _entries)
                {
                    writer.Write(kvp.Key);
                    writer.Write(kvp.Value.LastWriteTimeUtcTicks);
                    writer.Write(kvp.Value.Hashes.Length);

                    foreach (var hash in kvp.Value.Hashes)
                    {
                        writer.Write(hash.EntryName);
                        writer.Write(hash.Hash);
                    }
                }

                writer.Flush();
            }
            catch
            {
                // Best-effort cache persistence.
            }
        }
    }

    private sealed record ArchiveHashCacheEntry(long LastWriteTimeUtcTicks, CachedImageHash[] Hashes);
}

/// <summary>
/// Cached pHash of one image entry in a .cbz archive.
/// </summary>
readonly record struct CachedImageHash(string EntryName, ulong Hash);
