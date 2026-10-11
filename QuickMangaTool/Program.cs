using Spectre.Console;

class Program
{
    static int Main(string[] args)
    {
        var options = OptionsParser.Parse(args, Console.Out);
        if (options is null)
            return 0;

        // Only show the dry-run banner when running batch operations —
        // standalone log commands shouldn't print this message.
        if (options.DryRun && !options.ReadLogs && !options.DeleteLogs && !options.RemoveOldCbz && !options.DetectUpscale && !options.CbrToCbz)
            Console.WriteLine("Dry-run mode: no archives will be modified. Use --confirm or -y to apply changes.");

        if (options.ClearCache)
        {
            SkipCache.Clear();
            DuplicatePhashCache.Clear();
            Console.WriteLine("Caches cleared: ~/.quickmangatool/skipcache.dat, ~/.quickmangatool/duplicate-phash-cache.dat");
            return 0;
        }

        if (!Directory.Exists(options.Directory))
        {
            Console.Error.WriteLine($"Directory not found: {options.Directory}");
            return 1;
        }

        var command = BuildCommand(options);

        // If the selected command is a standalone log command, execute it without
        // requiring discovery of .cbz files.
        if (options.ReadLogs || options.DeleteLogs)
        {
            return command.Execute(Array.Empty<string>(), new NoopRunLogger());
        }

        // PDF-to-CBZ discovers PDF inputs itself, so do not block it behind the
        // archive pre-scan that is only relevant for .cbz-based batch commands.
        if (options.PdfToCbz)
        {
            return ExecuteWithLogging(command, options, Array.Empty<string>());
        }

        var cbzFiles = Directory.EnumerateFiles(options.Directory, "*.cbz", SearchOption.AllDirectories)
            .Where(f => !f.EndsWith(".old.cbz", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (cbzFiles.Count == 0)
        {
            Console.WriteLine("No .cbz files found.");
            return 0;
        }

        return ExecuteWithLogging(command, options, cbzFiles);
    }

    private static int ExecuteWithLogging(IAppCommand command, AppOptions options, IReadOnlyList<string> inputFiles)
    {
        var logFileName = $"QuickMangaTool_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log";
        string logFilePath;

        // Prefer writing logs to the application's executable directory. If that fails (e.g. directory is read-only),
        // fall back to the target directory being processed.
        var exeDir = AppContext.BaseDirectory ?? AppDomain.CurrentDomain.BaseDirectory;
        try
        {
            var fullExeDir = Path.GetFullPath(exeDir);
            if (!Directory.Exists(fullExeDir))
                Directory.CreateDirectory(fullExeDir);

            // Quick writable check: create and remove a temp file in the exe dir.
            var tempTest = Path.Combine(fullExeDir, Path.GetRandomFileName());
            using (var fs = File.Create(tempTest)) { }
            File.Delete(tempTest);

            logFilePath = Path.Combine(fullExeDir, logFileName);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: unable to create log in executable directory ({exeDir}): {ex.Message}. Falling back to {options.Directory}");
            logFilePath = Path.Combine(options.Directory, logFileName);
        }

        using var logger = new ColoredFileLogger(logFilePath);
        logger.WriteHeader(DateTime.Now, options.DryRun, BuildFeatureLabel(options));

        int result = command.Execute(inputFiles, logger);
        Console.WriteLine($"Log saved to: {logFilePath}");
        return result;
    }

    private static IAppCommand BuildCommand(AppOptions options)
    {
        if (options.ReadLogs)
            return new ReadLogsCommand(options.Directory);

        if (options.DeleteLogs)
            return new DeleteLogsCommand(options.Directory);

        if (options.DetectUpscale)
            return new UpscaleScanCommand(new UpscaleDetector(new NetVipsQualityAnalyzer()), options.Parallelism);

        if (options.DetectDuplicates)
            return new DuplicateScanCommand(options.DryRun, options.Parallelism, options.PhashThreshold);

        if (options.AddDuplicates)
            return new AddDuplicatesCommand(options.Directory, options.DryRun, options.Parallelism);

        IArchiveImageConverter? imageConverter = options.ImageFormat == ImageFormat.Avif
            ? new AvifArchiveImageConverter(effort: options.Effort ?? 3)
            : options.ImageFormat == ImageFormat.Jxl
                ? new JxlArchiveImageConverter(effort: options.Effort ?? 5)
                : new WebpArchiveImageConverter(effort: options.Effort ?? 6);

        if (options.CompressFolders)
            return new CompressFoldersCommand(options.Directory, imageConverter);

        if (options.PdfToCbz)
            return new ExtractPdfToCbzCommand(options.Directory, imageConverter);

        if (options.CbrToCbz)
            return new ConvertCbrToCbzCommand(options.Directory);

        // Standalone backup removal: delete all .cbz.old files without converting.
        if (options.RemoveOldCbz && !options.ConvertToWebp)
            return new RemoveOldCbzCommand();

        var transformations = new List<IXmlTransformation>();
        if (options.GenreToTags)
            transformations.Add(new GenreToTagsTransformation());

        return new BatchTransformCommand(
            new BatchProcessor(
                new ArchiveProcessor(options.DryRun, transformations, options.ConvertToWebp, options.RemoveOldCbz, options.Parallelism, imageConverter),
                options.Parallelism),
            options.DryRun);
    }

    private static string BuildFeatureLabel(AppOptions options)
    {
        var features = new List<string>();
        if (options.DetectUpscale) features.Add("DetectUpscale");
        if (options.GenreToTags) features.Add("GenreToTags");
        if (options.PdfToCbz) features.Add("PdfToCbz");
        if (options.ConvertToWebp) features.Add(options.RemoveOldCbz ? "ConvertToWebP(RemoveOld)" : "ConvertToWebP(KeepOld)");
        if (options.RemoveOldCbz && !options.ConvertToWebp) features.Add("RemoveOldBackups");
        return features.Count > 0 ? string.Join(", ", features) : "BatchProcessing";
    }
}

