using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

class ArchiveProcessor : IArchiveProcessor
{
    private readonly bool _dryRun;
    private readonly IReadOnlyList<IXmlTransformation> _transformations;
    private readonly bool _convertToWebp;
    private readonly bool _removeOldCbz;
    private readonly int _parallelism;
    private readonly IArchiveImageConverter _imageConverter;
    private readonly Action<ZipArchive>? _updateHook;

    public ArchiveProcessor(
        bool dryRun,
        IReadOnlyList<IXmlTransformation> transformations,
        bool convertToWebp,
        bool removeOldCbz,
        int parallelism = 0,
        IArchiveImageConverter? imageConverter = null,
        Action<ZipArchive>? updateHook = null)
    {
        _dryRun = dryRun;
        _transformations = transformations;
        _convertToWebp = convertToWebp;
        _removeOldCbz = removeOldCbz;
        _parallelism = parallelism;
        _imageConverter = imageConverter ?? new WebpArchiveImageConverter();
        _updateHook = updateHook;
    }

    public ProcessResult Process(string cbzPath)
    {
        try
        {
            var originalSize = new FileInfo(cbzPath).Length;

            if (_dryRun)
                return ProcessDryRun(cbzPath, originalSize);

            if (_convertToWebp)
                return ProcessWithBackup(cbzPath, originalSize);

            return ProcessInPlace(cbzPath, originalSize);
        }
        catch (Exception ex)
        {
            return new ProcessResult(cbzPath, ProcessStatus.Error, ex.Message);
        }
    }

    private ProcessResult ProcessDryRun(string cbzPath, long originalSize)
    {
        using var zip = ZipFile.Open(cbzPath, ZipArchiveMode.Read);
        var xmlChanged = ApplyXmlTransformations(zip, persistChanges: false);
        var webpResult = ConvertImagesToWebp(zip, persistChanges: false);

        var changed = xmlChanged || webpResult.WebpKeptCount > 0;
        if (!changed)
        {
            if (webpResult.FailedImageCount > 0)
            {
                return new ProcessResult(
                    cbzPath,
                    ProcessStatus.Error,
                    $"Failed to convert {webpResult.FailedImageCount} image(s).",
                    Detail: BuildDetail(webpResult, originalSize, null));
            }

            return new ProcessResult(cbzPath, ProcessStatus.NoChange);
        }

        return new ProcessResult(
            cbzPath,
            ProcessStatus.WouldUpdate,
            Detail: BuildDetail(webpResult, originalSize, null),
            OriginalSizeBytes: originalSize);
    }

    private ProcessResult ProcessInPlace(string cbzPath, long originalSize)
    {
        bool changed;
        (int WebpKeptCount, int OriginalKeptCount, long SourceBytes, long ConvertedBytes, int FailedImageCount, List<string> Failures) webpResult;

        using (var zip = ZipFile.Open(cbzPath, ZipArchiveMode.Update))
        {
            var xmlChanged = ApplyXmlTransformations(zip, persistChanges: true);
            webpResult = ConvertImagesToWebp(zip, persistChanges: true);
            changed = xmlChanged || webpResult.WebpKeptCount > 0;
        } // zip disposed and flushed to disk here

        if (!changed)
        {
            if (webpResult.FailedImageCount > 0)
            {
                return new ProcessResult(
                    cbzPath,
                    ProcessStatus.Error,
                    $"Failed to convert {webpResult.FailedImageCount} image(s).",
                    Detail: BuildDetail(webpResult, originalSize, null));
            }

            return new ProcessResult(cbzPath, ProcessStatus.NoChange);
        }

        var newSize = new FileInfo(cbzPath).Length;
        return new ProcessResult(
            cbzPath,
            ProcessStatus.Updated,
            Detail: BuildDetail(webpResult, originalSize, newSize),
            OriginalSizeBytes: originalSize,
            NewSizeBytes: newSize);
    }

    private ProcessResult ProcessWithBackup(string cbzPath, long originalSize)
    {
        var oldPath = Path.Combine(
            Path.GetDirectoryName(cbzPath) ?? string.Empty,
            Path.GetFileNameWithoutExtension(cbzPath) + ".old" + Path.GetExtension(cbzPath));

        // If a .old.cbz backup already exists, this file was already converted in a previous run — skip immediately.
        if (File.Exists(oldPath))
            return new ProcessResult(cbzPath, ProcessStatus.NoChange);

        using (var scanZip = ZipFile.Open(cbzPath, ZipArchiveMode.Read))
        {
            var xmlWouldChange = ApplyXmlTransformations(scanZip, persistChanges: false);
            var webpWouldChange = ConvertImagesToWebp(scanZip, persistChanges: false);
            var wouldChange = xmlWouldChange || webpWouldChange.WebpKeptCount > 0;
            if (!wouldChange)
            {
                if (webpWouldChange.FailedImageCount > 0)
                {
                    return new ProcessResult(
                        cbzPath,
                        ProcessStatus.Error,
                        $"Failed to convert {webpWouldChange.FailedImageCount} image(s).",
                        Detail: BuildDetail(webpWouldChange, originalSize, null));
                }

                return new ProcessResult(cbzPath, ProcessStatus.NoChange);
            }
        }

        File.Move(cbzPath, oldPath);
        File.Copy(oldPath, cbzPath);

        try
        {
            bool changed;
            (int WebpKeptCount, int OriginalKeptCount, long SourceBytes, long ConvertedBytes, int FailedImageCount, List<string> Failures) webpResult;

            using (var zip = ZipFile.Open(cbzPath, ZipArchiveMode.Update))
            {
                // testing hook: allow injected action to run inside the update phase
                _updateHook?.Invoke(zip);

                var xmlChanged = ApplyXmlTransformations(zip, persistChanges: true);
                webpResult = ConvertImagesToWebp(zip, persistChanges: true);
                changed = xmlChanged || webpResult.WebpKeptCount > 0;
            } // zip disposed and flushed to disk here

            if (!changed)
            {
                if (webpResult.FailedImageCount > 0)
                {
                    return new ProcessResult(
                        cbzPath,
                        ProcessStatus.Error,
                        $"Failed to convert {webpResult.FailedImageCount} image(s).",
                        Detail: BuildDetail(webpResult, originalSize, null));
                }

                return new ProcessResult(cbzPath, ProcessStatus.NoChange);
            }

            var newSize = new FileInfo(cbzPath).Length;

            if (_removeOldCbz && File.Exists(oldPath))
                File.Delete(oldPath);

            return new ProcessResult(
                cbzPath,
                ProcessStatus.Updated,
                Detail: BuildDetail(webpResult, originalSize, newSize),
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

    private bool ApplyXmlTransformations(ZipArchive zip, bool persistChanges)
    {
        var comicEntries = zip.Entries
            .Where(e => Path.GetFileName(e.FullName).Equals("ComicInfo.xml", StringComparison.OrdinalIgnoreCase))
            .ToList();

        bool changed = false;
        foreach (var entry in comicEntries)
        {
            string xmlText;
            using (var reader = new StreamReader(entry.Open(), Encoding.UTF8, true))
                xmlText = reader.ReadToEnd();

            var doc = XDocument.Parse(xmlText);

            bool transformApplied = false;
            foreach (var t in _transformations)
                transformApplied |= t.Apply(doc);

            if (!transformApplied)
                continue;

            changed = true;

            if (persistChanges)
            {
                var entryName = entry.FullName;
                entry.Delete();
                using var s = zip.CreateEntry(entryName, CompressionLevel.Optimal).Open();
                doc.Save(s);
            }
        }

        return changed;
    }

    private (int WebpKeptCount, int OriginalKeptCount, long SourceBytes, long ConvertedBytes, int FailedImageCount, List<string> Failures) ConvertImagesToWebp(ZipArchive zip, bool persistChanges)
    {
        if (!_convertToWebp)
            return (0, 0, 0, 0, 0, []);

        var candidates = zip.Entries
            .Where(e => !string.IsNullOrWhiteSpace(e.Name))
            .Where(e => _imageConverter.CanConvert(e.FullName))
            .ToList();

        // Step 1: read all image bytes sequentially — ZipArchive is not thread-safe.
        // CompressedLength is intentionally NOT used here; accessing it after Open() in
        // ZipArchiveMode.Update throws "Length properties are unavailable once an entry
        // has been opened for writing".
        var rawImages = new List<(string EntryName, byte[] Data)>(candidates.Count);
        foreach (var entry in candidates)
        {
            using var source = entry.Open();
            using var buffer = new MemoryStream();
            source.CopyTo(buffer);
            rawImages.Add((entry.FullName, buffer.ToArray()));
        }

        // Step 2: encode all images to WebP in parallel — CPU-bound work.
        // The converter decides per image which format is smaller and returns that one.
        int cpuWorkers = WorkerCount.ResolveCpuBound(_parallelism);
        var converted = new (ConvertedImage? Result, string? Error)[rawImages.Count];

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
        });

        // Step 3: write results back to zip sequentially, then count outcomes.
        // An entry is only replaced when the winning format differs from the original
        // (i.e. WebP won). When the original is smaller, the entry is left untouched.
        int webpKeptCount = 0;
        int originalKeptCount = 0;
        long sourceBytes = 0;
        long convertedBytes = 0;
        int failedCount = 0;
        var failures = new List<string>();

        for (int i = 0; i < converted.Length; i++)
        {
            var (result, error) = converted[i];

            if (error is not null)
            {
                failedCount++;
                if (failures.Count < 5)
                    failures.Add(error);
                continue;
            }

            sourceBytes += result!.SourceBytes;
            convertedBytes += result.ConvertedBytes;

            if (result.WebpWasSmaller)
                webpKeptCount++;
            else
                originalKeptCount++;

            if (!persistChanges)
                continue;

            if (!result.WebpWasSmaller)
                continue; // original is already in the archive, no write needed

            // WebP is smaller: replace the original entry with the WebP one
            var originalEntry = zip.GetEntry(rawImages[i].EntryName);
            originalEntry?.Delete();
            using var output = zip.CreateEntry(result.EntryName, CompressionLevel.Optimal).Open();
            output.Write(result.Content, 0, result.Content.Length);
        }

        return (webpKeptCount, originalKeptCount, sourceBytes, convertedBytes, failedCount, failures);
    }

    private static string BuildDetail((int WebpKeptCount, int OriginalKeptCount, long SourceBytes, long ConvertedBytes, int FailedImageCount, List<string> Failures) webpResult, long originalSize, long? newSize)
    {
        var lines = new List<string>();
        int totalProcessed = webpResult.WebpKeptCount + webpResult.OriginalKeptCount;

        if (totalProcessed > 0)
        {
            lines.Add($"Images processed: {totalProcessed}  (WebP kept: {webpResult.WebpKeptCount}  ·  Original kept (smaller): {webpResult.OriginalKeptCount})");
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

        if (webpResult.FailedImageCount > 0)
        {
            lines.Add($"Failed images: {webpResult.FailedImageCount}");
            foreach (var failure in webpResult.Failures)
                lines.Add($"  {failure}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    
}
