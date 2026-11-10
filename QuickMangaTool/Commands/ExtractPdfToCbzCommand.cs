using System.IO.Compression;
using Spectre.Console;

/// <summary>
/// Converts PDF files into .cbz archives. Each PDF's embedded images are extracted,
/// converted using the configured <see cref="IArchiveImageConverter"/>, and compressed
/// into a .cbz archive with three-digit ordering (001, 002, ...).
/// </summary>
class ExtractPdfToCbzCommand : IAppCommand
{
    private readonly string _root;
    private readonly IArchiveImageConverter _converter;
    private readonly bool _deleteSource;

    public ExtractPdfToCbzCommand(string root, IArchiveImageConverter? converter = null, bool deleteSource = false)
    {
        _root = root;
        _converter = converter ?? new WebpArchiveImageConverter();
        _deleteSource = deleteSource;
    }

    public int Execute(IReadOnlyList<string> cbzFiles, IRunLogger logger)
    {
        var root = _root;
        if (!Directory.Exists(root))
        {
            Console.Error.WriteLine($"Directory not found: {root}");
            return 1;
        }

        var pdfs = Directory.EnumerateFiles(root, "*.pdf", SearchOption.TopDirectoryOnly).OrderBy(p => p).ToList();
        if (pdfs.Count == 0)
        {
            Console.WriteLine("No PDF files found.");
            return 0;
        }

        int created = 0;
        int skipped = 0;
        int failed = 0;

        ProgressFactory.Create().Start(ctx =>
        {
            var task = ctx.AddTask("Extracting & converting PDFs to .cbz", maxValue: pdfs.Count);

            foreach (var pdfPath in pdfs)
            {
                var pdfName = Path.GetFileName(pdfPath);
                var targetCbz = Path.Combine(Path.GetDirectoryName(pdfPath) ?? root, Path.GetFileNameWithoutExtension(pdfPath) + ".cbz");

                task.Description = Markup.Escape(ProgressDescriptionFormatter.Format("Processing", pdfName));

                if (File.Exists(targetCbz))
                {
                    logger.WriteEntry(new ProcessResult(targetCbz, ProcessStatus.NoChange));
                    task.Description = Markup.Escape(ProgressDescriptionFormatter.Format("Skipped", pdfName));
                    task.Increment(1);
                    skipped++;
                    continue;
                }

                var tempCbz = targetCbz + ".tmp";

                try
                {
                    var extractedImages = PdfImageExtractor.ExtractImages(pdfPath);
                    if (extractedImages.Count == 0)
                    {
                        logger.WriteEntry(new ProcessResult(targetCbz, ProcessStatus.NoChange, Detail: "No images found in PDF."));
                        task.Increment(1);
                        skipped++;
                        continue;
                    }

                    var converter = _converter;
                    int cpuWorkers = WorkerCount.ResolveCpuBound(0);
                    var converted = new (ConvertedImage? Result, string? Error)[extractedImages.Count];

                    Parallel.For(0, extractedImages.Count, new ParallelOptions { MaxDegreeOfParallelism = cpuWorkers }, i =>
                    {
                        try
                        {
                            var (name, bytes) = extractedImages[i];
                            if (!converter.CanConvert(name))
                            {
                                converted[i] = (new ConvertedImage(name, bytes, bytes.Length, 0, IsSmaller: false, IsBlackAndWhite: false), null);
                            }
                            else
                            {
                                using var ms = new MemoryStream(bytes);
                                converted[i] = (converter.Convert(name, ms), null);
                            }
                        }
                        catch (Exception ex)
                        {
                            converted[i] = (null, ex.Message);
                        }
                    });

                    using (var zip = ZipFile.Open(tempCbz, ZipArchiveMode.Create))
                    {
                        for (int i = 0; i < converted.Length; i++)
                        {
                            var (result, error) = converted[i];
                            if (error is not null)
                                throw new Exception(error);

                            var chosenExt = Path.GetExtension(result!.EntryName).ToLowerInvariant();
                            var entryName = $"{(i + 1):D3}{chosenExt}";

                            var entry = zip.CreateEntry(entryName, CompressionLevel.NoCompression);
                            using var s = entry.Open();
                            s.Write(result.Content, 0, result.Content.Length);
                        }
                    }

                    File.Move(tempCbz, targetCbz, overwrite: true);

                    int bwCount = 0;
                    int colorCount = 0;
                    foreach (var (res, _) in converted)
                    {
                        if (res is null) continue;
                        if (res.IsBlackAndWhite) bwCount++; else colorCount++;
                    }

                    var detail = $"Black & White: {bwCount}  ·  Color: {colorCount}";
                    logger.WriteEntry(new ProcessResult(targetCbz, ProcessStatus.Updated, Detail: detail));
                    created++;

                    if (_deleteSource && File.Exists(pdfPath))
                    {
                        try { File.Delete(pdfPath); }
                        catch { /* best-effort cleanup */ }
                    }
                }
                catch (Exception ex)
                {
                    try { if (File.Exists(tempCbz)) File.Delete(tempCbz); } catch { }
                    logger.WriteEntry(new ProcessResult(pdfPath, ProcessStatus.Error, ex.Message));
                    failed++;
                }

                task.Description = Markup.Escape(ProgressDescriptionFormatter.Format("Completed", pdfName));
                task.Increment(1);
            }
        });

        logger.WriteSummary(pdfs.Count, created, 0);
        Console.WriteLine($"Created {created} .cbz file(s)." + (failed > 0 ? $" {failed} failed." : string.Empty));
        return failed > 0 ? 1 : 0;
    }
}
