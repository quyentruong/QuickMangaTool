using System.IO.Compression;
using System.Numerics;

/// <summary>
/// Scans .cbz archives for duplicate images by comparing per-entry pHash values
/// against hashes stored in duplicates.db.
/// </summary>
class DuplicateScanCommand : IAppCommand
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tif", ".tiff", ".webp"
    };

    private readonly bool _dryRun;
    private readonly int _parallelism;
    private readonly int _phashThreshold;

    public DuplicateScanCommand(bool dryRun, int parallelism, int phashThreshold)
    {
        _dryRun = dryRun;
        _parallelism = parallelism;
        _phashThreshold = Math.Max(0, phashThreshold);
    }

    public int Execute(IReadOnlyList<string> cbzFiles, IRunLogger logger)
    {
        var dbPath = Path.Combine(AppContext.BaseDirectory, "duplicates.db");
        if (!File.Exists(dbPath))
        {
            Console.WriteLine($"Duplicate database not found: {dbPath}");
            Console.WriteLine("Run --add-duplicates first to create and populate the database.");
            return 0;
        }

        var dbHashes = SQLiteDuplicateStore.LoadAllHashes(dbPath);
        if (dbHashes.Length == 0)
        {
            Console.WriteLine("Duplicate database is empty. Add hashes first with --add-duplicates.");
            return 0;
        }

        Console.WriteLine($"Scanning {cbzFiles.Count} archive(s) with pHash threshold {_phashThreshold}...");

        int workers = WorkerCount.Resolve(_parallelism);
        var scanResults = new List<ArchiveScanResult>();

        Parallel.ForEach(cbzFiles, new ParallelOptions { MaxDegreeOfParallelism = workers }, cbzPath =>
        {
            try
            {
                var matches = ScanArchive(cbzPath, dbHashes, _phashThreshold);
                lock (scanResults)
                {
                    scanResults.Add(new ArchiveScanResult(cbzPath, matches, null));
                }
            }
            catch (Exception ex)
            {
                lock (scanResults)
                {
                    scanResults.Add(new ArchiveScanResult(cbzPath, [], ex.Message));
                }
            }
        });

        var ordered = scanResults.OrderBy(r => r.CbzPath, StringComparer.OrdinalIgnoreCase).ToList();

        foreach (var errorResult in ordered.Where(r => r.Error is not null))
        {
            logger.WriteEntry(new ProcessResult(errorResult.CbzPath, ProcessStatus.Error, errorResult.Error));
        }

        var affectedArchives = ordered
            .Where(r => r.Error is null && r.Matches.Count > 0)
            .ToList();

        int totalMatches = affectedArchives.Sum(r => r.Matches.Count);

        if (affectedArchives.Count == 0)
        {
            Console.WriteLine("No duplicate images found.");
            logger.WriteSummary(cbzFiles.Count, 0, 0);
            return 0;
        }

        PrintSummary(affectedArchives, totalMatches);

        if (_dryRun)
        {
            foreach (var result in affectedArchives)
            {
                logger.WriteEntry(new ProcessResult(result.CbzPath, ProcessStatus.WouldUpdate, Detail: BuildDetail(result.Matches)));
            }

            logger.WriteSummary(cbzFiles.Count, 0, affectedArchives.Count);
            return 0;
        }

        Console.Write($"Delete {totalMatches} duplicate image(s) across {affectedArchives.Count} archive(s)? [y/N]: ");
        var answer = Console.ReadLine();
        bool confirmed = string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(answer, "yes", StringComparison.OrdinalIgnoreCase);

        if (!confirmed)
        {
            Console.WriteLine("Deletion canceled.");
            foreach (var result in affectedArchives)
            {
                logger.WriteEntry(new ProcessResult(result.CbzPath, ProcessStatus.WouldUpdate, Detail: BuildDetail(result.Matches)));
            }

            logger.WriteSummary(cbzFiles.Count, 0, affectedArchives.Count);
            return 0;
        }

        int updated = 0;

        foreach (var result in affectedArchives)
        {
            try
            {
                int deleted = DeleteMatchesWithBackup(result.CbzPath, result.Matches);
                logger.WriteEntry(new ProcessResult(result.CbzPath, deleted > 0 ? ProcessStatus.Updated : ProcessStatus.NoChange, Detail: $"Deleted entries: {deleted}"));
                if (deleted > 0)
                    updated++;
            }
            catch (Exception ex)
            {
                logger.WriteEntry(new ProcessResult(result.CbzPath, ProcessStatus.Error, ex.Message));
            }
        }

        logger.WriteSummary(cbzFiles.Count, updated, 0);
        Console.WriteLine($"Deleted duplicates in {updated} archive(s).");

        return 0;
    }

    private static List<DuplicateMatch> ScanArchive(string cbzPath, ulong[] dbHashes, int threshold)
    {
        var matches = new List<DuplicateMatch>();

        var imageHashes = GetArchiveImageHashes(cbzPath);
        foreach (var imageHash in imageHashes)
        {
            int bestDistance = FindBestDistance(imageHash.Hash, dbHashes, threshold);
            if (bestDistance >= 0)
            {
                matches.Add(new DuplicateMatch(imageHash.EntryName, imageHash.Hash, bestDistance));
            }
        }

        return matches;
    }

    private static List<CachedImageHash> GetArchiveImageHashes(string cbzPath)
    {
        if (DuplicatePhashCache.TryGetArchiveHashes(cbzPath, out var cachedHashes))
            return cachedHashes;

        var computedHashes = new List<CachedImageHash>();

        using var zip = ZipFile.OpenRead(cbzPath);
        foreach (var entry in zip.Entries)
        {
            if (!IsSupportedImage(entry.FullName) || entry.Length <= 0)
                continue;

            using var stream = entry.Open();
            var hash = ImagePhash.ComputeHash(stream);
            computedHashes.Add(new CachedImageHash(entry.FullName, hash));
        }

        DuplicatePhashCache.UpsertArchiveHashes(cbzPath, computedHashes);
        return computedHashes;
    }

    private static int FindBestDistance(ulong hash, ulong[] dbHashes, int threshold)
    {
        int best = int.MaxValue;

        foreach (var dbHash in dbHashes)
        {
            int distance = BitOperations.PopCount(hash ^ dbHash);
            if (distance < best)
                best = distance;

            if (best == 0)
                break;
        }

        return best <= threshold ? best : -1;
    }

    private static int DeleteMatchesWithBackup(string cbzPath, IReadOnlyList<DuplicateMatch> matches)
    {
        var oldPath = Path.Combine(
            Path.GetDirectoryName(cbzPath) ?? string.Empty,
            Path.GetFileNameWithoutExtension(cbzPath) + ".old" + Path.GetExtension(cbzPath));

        if (!File.Exists(oldPath))
        {
            File.Move(cbzPath, oldPath);
            File.Copy(oldPath, cbzPath);
        }

        var deleteNames = new HashSet<string>(matches.Select(m => m.EntryName), StringComparer.OrdinalIgnoreCase);
        int deleted = 0;

        using var zip = ZipFile.Open(cbzPath, ZipArchiveMode.Update);
        var targets = zip.Entries.Where(e => deleteNames.Contains(e.FullName)).ToList();
        foreach (var entry in targets)
        {
            entry.Delete();
            deleted++;
        }

        return deleted;
    }

    private static bool IsSupportedImage(string path) => SupportedExtensions.Contains(Path.GetExtension(path));

    private static void PrintSummary(IReadOnlyList<ArchiveScanResult> affectedArchives, int totalMatches)
    {
        Console.WriteLine($"Found {totalMatches} duplicate image(s) across {affectedArchives.Count} archive(s):");

        foreach (var result in affectedArchives)
        {
            Console.WriteLine($"- {result.CbzPath}");
            foreach (var match in result.Matches)
            {
                Console.WriteLine($"  - {match.EntryName} (distance: {match.Distance})");
            }
        }
    }

    private static string BuildDetail(IReadOnlyList<DuplicateMatch> matches)
    {
        var lines = matches.Select(match => $"{match.EntryName} (distance: {match.Distance})");
        return string.Join(Environment.NewLine, lines);
    }

    private sealed record DuplicateMatch(string EntryName, ulong Hash, int Distance);

    private sealed record ArchiveScanResult(string CbzPath, List<DuplicateMatch> Matches, string? Error);
}
