using System.Collections.Concurrent;

/// <summary>
/// Scans image files under a directory, computes pHash values, and stores unique hashes in duplicates.db.
/// </summary>
class AddDuplicatesCommand : IAppCommand
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tif", ".tiff", ".webp"
    };

    private readonly string _rootDirectory;
    private readonly bool _dryRun;
    private readonly int _parallelism;

    public AddDuplicatesCommand(string rootDirectory, bool dryRun, int parallelism)
    {
        _rootDirectory = rootDirectory;
        _dryRun = dryRun;
        _parallelism = parallelism;
    }

    public int Execute(IReadOnlyList<string> cbzFiles, IRunLogger logger)
    {
        if (!Directory.Exists(_rootDirectory))
        {
            Console.Error.WriteLine($"Directory not found: {_rootDirectory}");
            return 1;
        }

        var imageFiles = Directory.EnumerateFiles(_rootDirectory, "*.*", SearchOption.AllDirectories)
            .Where(path => SupportedExtensions.Contains(Path.GetExtension(path)))
            .ToList();

        if (imageFiles.Count == 0)
        {
            Console.WriteLine("No image files found.");
            logger.WriteEntry(new ProcessResult(_rootDirectory, ProcessStatus.NoChange, Detail: "No image files found."));
            return 0;
        }

        Console.WriteLine($"Calculating pHash for {imageFiles.Count} image file(s)...");

        int workers = WorkerCount.ResolveCpuBound(_parallelism);
        var computedHashes = new ConcurrentDictionary<ulong, byte>();
        int failed = 0;

        Parallel.ForEach(imageFiles, new ParallelOptions { MaxDegreeOfParallelism = workers }, imagePath =>
        {
            try
            {
                using var stream = File.OpenRead(imagePath);
                var hash = ImagePhash.ComputeHash(stream);
                computedHashes.TryAdd(hash, 0);
            }
            catch
            {
                Interlocked.Increment(ref failed);
            }
        });

        var dbPath = Path.Combine(AppContext.BaseDirectory, "duplicates.db");
        var existing = SQLiteDuplicateStore.LoadAllHashes(dbPath);
        var existingSet = new HashSet<ulong>(existing);

        var uniqueHashes = computedHashes.Keys.ToList();
        int newHashesCount = uniqueHashes.Count(hash => !existingSet.Contains(hash));

        string detail = $"Image files: {imageFiles.Count}, Unique hashes: {uniqueHashes.Count}, Existing hashes: {existingSet.Count}, New hashes: {newHashesCount}, Failed files: {failed}";

        if (_dryRun)
        {
            Console.WriteLine($"Dry-run: would add {newHashesCount} hash(es) to {dbPath}.");
            logger.WriteEntry(new ProcessResult(_rootDirectory, newHashesCount > 0 ? ProcessStatus.WouldUpdate : ProcessStatus.NoChange, Detail: detail));
            logger.WriteSummary(imageFiles.Count, 0, newHashesCount);
            return 0;
        }

        var hashesToInsert = uniqueHashes.Where(hash => !existingSet.Contains(hash)).ToList();
        int inserted = SQLiteDuplicateStore.AddHashes(dbPath, hashesToInsert);

        Console.WriteLine($"Added {inserted} hash(es) to {dbPath}. Failed files: {failed}.");
        logger.WriteEntry(new ProcessResult(_rootDirectory, inserted > 0 ? ProcessStatus.Updated : ProcessStatus.NoChange, Detail: detail));
        logger.WriteSummary(imageFiles.Count, inserted, 0);

        return 0;
    }
}
