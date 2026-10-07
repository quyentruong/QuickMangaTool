using Spectre.Console;

/// <summary>
/// Converts PDF files into .cbz archives in two stages: each PDF's embedded images are first
/// extracted, unconverted, into a folder named after the PDF, then <see cref="CompressFoldersCommand"/>
/// is delegated to for the actual webp conversion and archiving. This keeps the conversion/compression
/// logic in a single place shared by both commands.
/// </summary>
class ExtractPdfToCbzCommand : IAppCommand
{
    private readonly string _root;
    private readonly IArchiveImageConverter _converter;
    public ExtractPdfToCbzCommand(string root, IArchiveImageConverter? converter = null)
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

        var pdfs = Directory.EnumerateFiles(root, "*.pdf", SearchOption.TopDirectoryOnly).OrderBy(p => p).ToList();
        if (pdfs.Count == 0)
        {
            Console.WriteLine("No PDF files found.");
            return 0;
        }

        var lockObj = new object();
        var extractedFolders = new List<string>();
        int failed = 0;

        int pdfConcurrency = WorkerCount.ResolveCpuBound(0);

        ProgressFactory.Create().Start(ctx =>
        {
            var task = ctx.AddTask("Extracting PDF images", maxValue: pdfs.Count);
            const int descriptionWidth = 100;

            System.Threading.Tasks.Parallel.ForEach(pdfs, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = pdfConcurrency }, pdfPath =>
            {
                var folderPath = Path.Combine(Path.GetDirectoryName(pdfPath) ?? root, Path.GetFileNameWithoutExtension(pdfPath));
                var targetCbz = folderPath + ".cbz";

                if (File.Exists(targetCbz))
                {
                    lock (lockObj) { logger.WriteEntry(new ProcessResult(targetCbz, ProcessStatus.NoChange)); }
                }
                else
                {
                    try
                    {
                        var images = PdfImageExtractor.ExtractImages(pdfPath);
                        if (images.Count == 0)
                        {
                            lock (lockObj) { logger.WriteEntry(new ProcessResult(targetCbz, ProcessStatus.NoChange, Detail: "No images found in PDF.")); }
                        }
                        else
                        {
                            Directory.CreateDirectory(folderPath);
                            foreach (var (name, data) in images)
                                File.WriteAllBytes(Path.Combine(folderPath, Path.GetFileName(name)), data);

                            lock (lockObj) { extractedFolders.Add(folderPath); }
                        }
                    }
                    catch (Exception ex)
                    {
                        lock (lockObj)
                        {
                            logger.WriteEntry(new ProcessResult(pdfPath, ProcessStatus.Error, ex.Message));
                            failed++;
                        }
                    }
                }

                lock (lockObj)
                {
                    task.Description = Markup.Escape(ProgressDescriptionFormatter.Format("Extracting", pdfPath, descriptionWidth));
                    task.Increment(1);
                }
            });
        });

        if (extractedFolders.Count == 0)
        {
            logger.WriteSummary(pdfs.Count, 0, 0);
            Console.WriteLine("Created 0 .cbz file(s)." + (failed > 0 ? $" {failed} failed." : string.Empty));
            return failed > 0 ? 1 : 0;
        }

        // Delegate the actual image conversion and archiving to CompressFoldersCommand
        var compressExitCode = new CompressFoldersCommand(root, _converter).Execute(cbzFiles, logger);

        // Clean up the intermediate extraction folders once their .cbz has been created
        foreach (var folder in extractedFolders)
        {
            if (File.Exists(folder + ".cbz"))
            {
                try { Directory.Delete(folder, recursive: true); }
                catch { /* leave the folder in place if it can't be removed */ }
            }
        }

        return failed > 0 ? 1 : compressExitCode;
    }
}
