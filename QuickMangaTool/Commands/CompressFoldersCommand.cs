using System.IO.Compression;
using Spectre.Console;

/// <summary>
/// Scans a root directory for subfolders and compresses each folder into a .cbz file.
/// Each image inside a folder is sorted by filename and added with a three-digit prefix
/// (001, 002, ...) and an extension that matches the stored bytes (e.g. .webp when
/// the converter chose WebP). CompressionLevel.NoCompression is used for speed since
/// images are already compressed.
/// </summary>
class CompressFoldersCommand : IAppCommand
{
    private readonly string _root;
    private readonly IArchiveImageConverter _converter;
    public CompressFoldersCommand(string root, IArchiveImageConverter? converter = null)
    {
        _root = root;
        _converter = converter ?? new WebpArchiveImageConverter();
    }

    public int Execute(IReadOnlyList<string> cbzFiles, IRunLogger logger)
    {
        var root = _root;
        if (!Directory.Exists(root))
        {
            Console.Error.WriteLine($"Directory not found: {root}");
            return 1;
        }

        var subfolders = Directory.GetDirectories(root).OrderBy(d => d).ToList();
        if (subfolders.Count == 0)
        {
            Console.WriteLine("No subfolders found.");
            return 0;
        }

        int created = 0;
        int skipped = 0;
        int failed = 0;

        ProgressFactory.Create().Start(ctx =>
        {
            var task = ctx.AddTask("Compressing folders to .cbz", maxValue: subfolders.Count);
            const int descriptionWidth = 100;

            foreach (var folder in subfolders)
            {
                var target = Path.Combine(Path.GetDirectoryName(folder) ?? root, Path.GetFileName(folder) + ".cbz");

                if (File.Exists(target))
                {
                    var result = new ProcessResult(target, ProcessStatus.NoChange);
                    logger.WriteEntry(result);
                    task.Description = Markup.Escape(ProgressDescriptionFormatter.Format("Skipped", target, descriptionWidth));
                    task.Increment(1);
                    skipped++;
                    continue;
                }

                try
                {
                    var files = Directory.EnumerateFiles(folder)
                        .Where(f => !string.IsNullOrWhiteSpace(Path.GetFileName(f)))
                        .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    if (files.Count == 0)
                    {
                        var r = new ProcessResult(target, ProcessStatus.NoChange, Detail: "No files found in folder.");
                        logger.WriteEntry(r);
                        task.Increment(1);
                        skipped++;
                        continue;
                    }

                    // Step 1: read all files into memory sequentially (ZipArchive is not thread-safe for reading/writing entries concurrently)
                    var raw = new List<(string FilePath, byte[] Data)>(files.Count);
                    foreach (var f in files)
                    {
                        raw.Add((f, File.ReadAllBytes(f)));
                    }

                    // Step 2: convert images in parallel using the configured converter
                    var converter = _converter;
                    int cpuWorkers = WorkerCount.ResolveCpuBound(0);
                    var converted = new (ConvertedImage? Result, string? Error)[raw.Count];

                    System.Threading.Tasks.Parallel.For(0, raw.Count, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = cpuWorkers }, i =>
                    {
                        try
                        {
                            var fileName = Path.GetFileName(raw[i].FilePath);
                            if (!converter.CanConvert(fileName))
                            {
                                // Return the original bytes when the converter doesn't support this extension
                                converted[i] = (new ConvertedImage(fileName, raw[i].Data, raw[i].Data.Length, 0, IsSmaller: false, IsBlackAndWhite: false), null);
                            }
                            else
                            {
                                using var ms = new MemoryStream(raw[i].Data);
                                converted[i] = (converter.Convert(fileName, ms), null);
                            }
                        }
                        catch (Exception ex)
                        {
                            converted[i] = (null, ex.Message);
                        }
                    });

                    // Step 3: write results into the CBZ sequentially
                    using (var zip = ZipFile.Open(target, ZipArchiveMode.Create))
                    {
                        for (int i = 0; i < converted.Length; i++)
                        {
                            var (result, error) = converted[i];
                            if (error is not null)
                                throw new Exception(error);

                            // Choose extension based on stored bytes: use the converter's chosen entry name extension
                            var chosenExt = Path.GetExtension(result!.EntryName).ToLowerInvariant();
                            var entryName = $"{(i + 1):D3}{chosenExt}";

                            var entry = zip.CreateEntry(entryName, CompressionLevel.NoCompression);
                            using var s = entry.Open();
                            s.Write(result.Content, 0, result.Content.Length);
                        }
                    }
                        int bwCount = 0;
                        int colorCount = 0;
                        foreach (var (res, err) in converted)
                        {
                            if (res is null) continue;
                            if (res.IsBlackAndWhite) bwCount++; else colorCount++;
                        }

                        var detail = $"Black & White: {bwCount}  ·  Color: {colorCount}";
                        var success = new ProcessResult(target, ProcessStatus.Updated, Detail: detail);
                        logger.WriteEntry(success);
                    created++;
                }
                catch (Exception ex)
                {
                    // Clean up partial file
                    try { if (File.Exists(target)) File.Delete(target); } catch { }
                    var errorResult = new ProcessResult(target, ProcessStatus.Error, ex.Message);
                    logger.WriteEntry(errorResult);
                    failed++;
                }

                task.Description = Markup.Escape(ProgressDescriptionFormatter.Format("Processing", folder, descriptionWidth));
                task.Increment(1);
            }
        });

        logger.WriteSummary(subfolders.Count, created, 0);
        Console.WriteLine($"Created {created} .cbz file(s)." + (failed > 0 ? $" {failed} failed." : string.Empty));
        return failed > 0 ? 1 : 0;
    }
}
