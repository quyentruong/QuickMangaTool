using System.IO.Compression;

class ArchiveProcessor : IArchiveProcessor
{
    private readonly bool _dryRun;
    private readonly bool _convertImages;
    private readonly bool _removeOldCbz;
    private readonly int _parallelism;
    private readonly IArchiveImageConverter _imageConverter;
    private readonly Action<ZipArchive>? _updateHook;

    public ArchiveProcessor(
        bool dryRun,
        bool convertImages,
        bool removeOldCbz,
        int parallelism = 0,
        IArchiveImageConverter? imageConverter = null,
        Action<ZipArchive>? updateHook = null)
    {
        _dryRun = dryRun;
        _convertImages = convertImages;
        _removeOldCbz = removeOldCbz;
        _parallelism = parallelism;
        _imageConverter = imageConverter ?? new WebpArchiveImageConverter();
        _updateHook = updateHook;
    }

    public ProcessResult Process(string cbzPath, Action<int, int>? onImageProgress = null)
    {
        try
        {
            var originalSize = new FileInfo(cbzPath).Length;

            if (!_convertImages)
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

            if (encodeResult.ConvertedKeptCount == 0)
            {
                // No image became smaller
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

                    for (int i = 0; i < encodeResult.ConvertedEntries.Length; i++)
                    {
                        var (originalName, converted) = encodeResult.ConvertedEntries[i];
                        if (converted is null || !converted.IsSmaller)
                            continue;

                        var originalEntry = zip.GetEntry(originalName);
                        originalEntry?.Delete();

                        var newEntry = zip.CreateEntry(converted.EntryName, CompressionLevel.Optimal);
                        using var s = newEntry.Open();
                        s.Write(converted.Content, 0, converted.Content.Length);
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

    private sealed record EncodeResult(
        (string OriginalName, ConvertedImage? Converted)[] ConvertedEntries,
        int ConvertedKeptCount,
        int OriginalKeptCount,
        long SourceBytes,
        long ConvertedBytes,
        int FailedImageCount,
        List<string> Failures,
        int BlackWhiteCount,
        int ColorCount);

    private EncodeResult ReadAndEncodeImages(string cbzPath, Action<int, int>? onImageProgress)
    {
        List<(string EntryName, byte[] Data)> rawImages;

        using (var zip = ZipFile.Open(cbzPath, ZipArchiveMode.Read))
        {
            var candidates = zip.Entries
                .Where(e => !string.IsNullOrWhiteSpace(e.Name))
                .Where(e => _imageConverter.CanConvert(e.FullName))
                .ToList();

            rawImages = new List<(string EntryName, byte[] Data)>(candidates.Count);
            foreach (var entry in candidates)
            {
                using var source = entry.Open();
                using var buffer = new MemoryStream();
                source.CopyTo(buffer);
                rawImages.Add((entry.FullName, buffer.ToArray()));
            }
        }

        if (rawImages.Count == 0)
        {
            return new EncodeResult(
                Array.Empty<(string, ConvertedImage?)>(),
                0, 0, 0, 0, 0, new List<string>(), 0, 0);
        }

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

        int convertedKeptCount = 0;
        int originalKeptCount = 0;
        long sourceBytes = 0;
        long convertedBytes = 0;
        int failedCount = 0;
        var failures = new List<string>();
        int blackWhiteCount = 0;
        int colorCount = 0;

        var entries = new (string OriginalName, ConvertedImage? Converted)[rawImages.Count];

        for (int i = 0; i < converted.Length; i++)
        {
            var (result, error) = converted[i];
            var originalName = rawImages[i].EntryName;

            if (error is not null || result is null)
            {
                failedCount++;
                if (failures.Count < 5)
                    failures.Add(error ?? $"{originalName}: Unknown error");
                entries[i] = (originalName, null);
                continue;
            }

            sourceBytes += result.SourceBytes;
            convertedBytes += result.ConvertedBytes;

            if (result.IsSmaller)
                convertedKeptCount++;
            else
                originalKeptCount++;

            if (result.IsBlackAndWhite)
                blackWhiteCount++;
            else
                colorCount++;

            entries[i] = (originalName, result);
        }

        return new EncodeResult(
            entries,
            convertedKeptCount,
            originalKeptCount,
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
