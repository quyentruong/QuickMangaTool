using System.IO.Compression;

class ArchiveProcessor : IArchiveProcessor
{
    private readonly bool _dryRun;
    private readonly bool _convertImages;
    private readonly bool _removeOldCbz;
    private readonly int _parallelism;
    private readonly IArchiveImageConverter _imageConverter;
    private readonly Action<ZipArchive>? _updateHook;
    private readonly bool _renamePages;

    public ArchiveProcessor(
        bool dryRun,
        bool convertImages,
        bool removeOldCbz,
        int parallelism = 0,
        IArchiveImageConverter? imageConverter = null,
        Action<ZipArchive>? updateHook = null,
        bool renamePages = false)
    {
        _dryRun = dryRun;
        _convertImages = convertImages;
        _removeOldCbz = removeOldCbz;
        _parallelism = parallelism;
        _imageConverter = imageConverter ?? new WebpArchiveImageConverter();
        _updateHook = updateHook;
        _renamePages = renamePages;
    }

    public ProcessResult Process(string cbzPath, Action<int, int>? onImageProgress = null)
    {
        try
        {
            var originalSize = new FileInfo(cbzPath).Length;

            if (!_convertImages && !_renamePages)
                return new ProcessResult(cbzPath, ProcessStatus.NoChange);

            var oldPath = Path.Combine(
                Path.GetDirectoryName(cbzPath) ?? string.Empty,
                Path.GetFileNameWithoutExtension(cbzPath) + ".old" + Path.GetExtension(cbzPath));

            // If a .old.cbz backup already exists, this file was already converted in a previous run — skip immediately.
            if (File.Exists(oldPath))
                return new ProcessResult(cbzPath, ProcessStatus.NoChange);

            // Step 1: Read and encode images once
            var encodeResult = ReadAndEncodeImages(cbzPath, onImageProgress);

            if (encodeResult.FailedImageCount > 0)
            {
                return new ProcessResult(
                    cbzPath,
                    ProcessStatus.Error,
                    $"Failed to convert {encodeResult.FailedImageCount} image(s).",
                    Detail: BuildDetail(encodeResult, originalSize, null));
            }

            if (encodeResult.ConvertedKeptCount == 0 && encodeResult.RenamedCount == 0)
            {
                // No image became smaller and no image needs renaming
                return new ProcessResult(cbzPath, ProcessStatus.NoChange);
            }

            // Step 2: Handle dry run
            if (_dryRun)
            {
                return new ProcessResult(
                    cbzPath,
                    ProcessStatus.WouldUpdate,
                    Detail: BuildDetail(encodeResult, originalSize, null),
                    OriginalSizeBytes: originalSize);
            }

            // Step 3: Apply changes with backup
            File.Move(cbzPath, oldPath);
            File.Copy(oldPath, cbzPath);

            try
            {
                using (var zip = ZipFile.Open(cbzPath, ZipArchiveMode.Update))
                {
                    _updateHook?.Invoke(zip);

                    var planned = encodeResult.PlannedChanges;
                    var resolvedContents = new byte[planned.Length][];

                    // Pre-read entries that are being renamed without re-encoding before deleting any entries
                    for (int i = 0; i < planned.Length; i++)
                    {
                        var change = planned[i];
                        if (change.Content is not null)
                        {
                            resolvedContents[i] = change.Content;
                            continue;
                        }

                        var originalEntry = zip.GetEntry(change.OriginalName)
                            ?? throw new InvalidDataException($"Entry not found in archive: {change.OriginalName}");
                        using var s = originalEntry.Open();
                        resolvedContents[i] = StreamByteReader.Read(s, out _);
                    }

                    // Delete all replaced/renamed entries before creating new ones to prevent name collisions
                    for (int i = 0; i < planned.Length; i++)
                    {
                        zip.GetEntry(planned[i].OriginalName)?.Delete();
                    }

                    for (int i = 0; i < planned.Length; i++)
                    {
                        var newEntry = zip.CreateEntry(planned[i].TargetName, CompressionLevel.Optimal);
                        using var s = newEntry.Open();
                        s.Write(resolvedContents[i], 0, resolvedContents[i].Length);
                    }
                }

                var newSize = new FileInfo(cbzPath).Length;

                if (_removeOldCbz && File.Exists(oldPath))
                    File.Delete(oldPath);

                return new ProcessResult(
                    cbzPath,
                    ProcessStatus.Updated,
                    Detail: BuildDetail(encodeResult, originalSize, newSize),
                    OriginalSizeBytes: originalSize,
                    NewSizeBytes: newSize);
            }
            catch
            {
                if (File.Exists(oldPath))
                {
                    if (File.Exists(cbzPath))
                        File.Delete(cbzPath);
                    File.Copy(oldPath, cbzPath);
                }

                throw;
            }
        }
        catch (Exception ex)
        {
            return new ProcessResult(cbzPath, ProcessStatus.Error, ex.Message);
        }
    }

    private sealed record PlannedEntryChange(string OriginalName, string TargetName, byte[]? Content);

    private sealed record EncodeResult(
        PlannedEntryChange[] PlannedChanges,
        int ConvertedKeptCount,
        int OriginalKeptCount,
        int RenamedCount,
        long SourceBytes,
        long ConvertedBytes,
        int FailedImageCount,
        List<string> Failures,
        int BlackWhiteCount,
        int ColorCount);

    private EncodeResult ReadAndEncodeImages(string cbzPath, Action<int, int>? onImageProgress)
    {
        var allImageNames = new List<string>();
        var rawImages = new List<(string EntryName, byte[] Data)>();

        using (var zip = ZipFile.Open(cbzPath, ZipArchiveMode.Read))
        {
            foreach (var entry in zip.Entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Name))
                    continue;

                bool canConvert = _convertImages && _imageConverter.CanConvert(entry.FullName);
                bool canRename = _renamePages && ImageExtensions.IsSupportedForAnalysis(entry.FullName);
                if (!canConvert && !canRename)
                    continue;

                allImageNames.Add(entry.FullName);

                if (canConvert)
                {
                    using var source = entry.Open();
                    rawImages.Add((entry.FullName, StreamByteReader.Read(source, out _)));
                }
            }
        }

        if (allImageNames.Count == 0)
        {
            return new EncodeResult(
                Array.Empty<PlannedEntryChange>(),
                0, 0, 0, 0, 0, 0, new List<string>(), 0, 0);
        }

        int convertedKeptCount = 0;
        int originalKeptCount = 0;
        long sourceBytes = 0;
        long convertedBytes = 0;
        int failedCount = 0;
        var failures = new List<string>();
        int blackWhiteCount = 0;
        int colorCount = 0;

        var convertedByOriginalName = new Dictionary<string, ConvertedImage>(StringComparer.Ordinal);
        var extensionOverrides = new Dictionary<string, string>(StringComparer.Ordinal);

        if (rawImages.Count > 0)
        {
            int cpuWorkers = WorkerCount.ResolveCpuBound(_parallelism);
            var converted = new (ConvertedImage? Result, string? Error)[rawImages.Count];
            int completedCount = 0;

            Parallel.For(0, rawImages.Count, new ParallelOptions { MaxDegreeOfParallelism = cpuWorkers }, i =>
            {
                var (entryName, data) = rawImages[i];
                try
                {
                    using var ms = new MemoryStream(data);
                    converted[i] = (_imageConverter.Convert(entryName, ms), null);
                }
                catch (Exception ex)
                {
                    converted[i] = (null, $"{entryName}: {ex.Message}");
                }
                finally
                {
                    var done = Interlocked.Increment(ref completedCount);
                    onImageProgress?.Invoke(done, rawImages.Count);
                }
            });

            for (int i = 0; i < converted.Length; i++)
            {
                var (result, error) = converted[i];
                var originalName = rawImages[i].EntryName;

                if (error is not null || result is null)
                {
                    failedCount++;
                    if (failures.Count < 5)
                        failures.Add(error ?? $"{originalName}: Unknown error");
                    continue;
                }

                sourceBytes += result.SourceBytes;
                convertedBytes += result.ConvertedBytes;

                if (result.IsSmaller)
                {
                    convertedKeptCount++;
                    extensionOverrides[originalName] = Path.GetExtension(result.EntryName).ToLowerInvariant();
                }
                else
                {
                    originalKeptCount++;
                }

                if (result.IsBlackAndWhite)
                    blackWhiteCount++;
                else
                    colorCount++;

                convertedByOriginalName[originalName] = result;
            }
        }

        var plannedChanges = new List<PlannedEntryChange>();
        int renamedCount = 0;

        if (failedCount == 0)
        {
            Dictionary<string, string>? renameMap = _renamePages
                ? PageRenamePlanner.BuildRenameMap(allImageNames, extensionOverrides)
                : null;

            foreach (var originalName in allImageNames)
            {
                if (convertedByOriginalName.TryGetValue(originalName, out var result) && result.IsSmaller)
                {
                    var targetName = renameMap is not null ? renameMap[originalName] : result.EntryName;
                    if (_renamePages && !string.Equals(targetName, result.EntryName, StringComparison.Ordinal))
                        renamedCount++;

                    plannedChanges.Add(new PlannedEntryChange(originalName, targetName, result.Content));
                }
                else if (renameMap is not null)
                {
                    var targetName = renameMap[originalName];
                    if (!string.Equals(targetName, originalName, StringComparison.Ordinal))
                    {
                        renamedCount++;
                        plannedChanges.Add(new PlannedEntryChange(originalName, targetName, Content: null));
                    }
                }
            }
        }

        return new EncodeResult(
            plannedChanges.ToArray(),
            convertedKeptCount,
            originalKeptCount,
            renamedCount,
            sourceBytes,
            convertedBytes,
            failedCount,
            failures,
            blackWhiteCount,
            colorCount);
    }

    private static string BuildDetail(EncodeResult encodeResult, long originalSize, long? newSize)
    {
        var lines = new List<string>();
        int totalProcessed = encodeResult.ConvertedKeptCount + encodeResult.OriginalKeptCount;

        if (totalProcessed > 0)
        {
            lines.Add($"Images processed: {totalProcessed}  (Converted kept: {encodeResult.ConvertedKeptCount}  ·  Original kept (smaller): {encodeResult.OriginalKeptCount})");
            lines.Add($"Black & White: {encodeResult.BlackWhiteCount}  ·  Color: {encodeResult.ColorCount}");
        }

        if (encodeResult.RenamedCount > 0)
            lines.Add($"Images renamed: {encodeResult.RenamedCount}");

        if (totalProcessed > 0 || encodeResult.RenamedCount > 0)
        {
            lines.Add($"Original size:    {SizeFormatter.FormatSize(originalSize)}");

            if (newSize is not null)
            {
                var delta = newSize.Value - originalSize;
                lines.Add($"New size:         {SizeFormatter.FormatSize(newSize.Value)}");
                lines.Add($"Size delta:       {(delta < 0 ? "-" : "+")}{SizeFormatter.FormatSize(Math.Abs(delta))}");
            }
        }
        else
        {
            lines.Add("No images were processed.");
        }

        if (encodeResult.FailedImageCount > 0)
        {
            lines.Add($"Failed images: {encodeResult.FailedImageCount}");
            foreach (var failure in encodeResult.Failures)
                lines.Add($"  {failure}");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
