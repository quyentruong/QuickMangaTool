using Spectre.Console;

class Program
{
    static int Main(string[] args)
    {
        var options = OptionsParser.Parse(args, Console.Out);
        if (options is null)
            return 0;

        if (options.DryRun)
            Console.WriteLine("Dry-run mode: no archives will be modified. Use --confirm or -y to apply changes.");

        if (options.ClearCache)
        {
            SkipCache.Clear();
            Console.WriteLine("Cache cleared: ~/.quickmangatool/skipcache.dat");
            return 0;
        }

        if (!Directory.Exists(options.Directory))
        {
            Console.Error.WriteLine($"Directory not found: {options.Directory}");
            return 1;
        }

        var command = BuildCommand(options);

        // If the selected command is a standalone log or folder-compress command, execute it without
        // requiring discovery of .cbz files or creating a log file.
        if (options.ReadLogs || options.DeleteLogs || options.CompressFolders)
        {
            return command.Execute(Array.Empty<string>(), new NoopRunLogger());
        }

        var cbzFiles = Directory.EnumerateFiles(options.Directory, "*.cbz", SearchOption.AllDirectories)
            .Where(f => !f.EndsWith(".old.cbz", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (cbzFiles.Count == 0)
        {
            Console.WriteLine("No .cbz files found.");
            return 0;
        }

        var logFilePath = Path.Combine(options.Directory, $"QuickMangaTool_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");

        using var logger = new ColoredFileLogger(logFilePath);
        logger.WriteHeader(DateTime.Now, options.DryRun, BuildFeatureLabel(options));

        int result = command.Execute(cbzFiles, logger);
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
            return new UpscaleScanCommand(new UpscaleDetector(new ImageSharpQualityAnalyzer()), options.Parallelism);

        if (options.CompressFolders)
            return new CompressFoldersCommand(options.Directory);

        // Standalone backup removal: delete all .cbz.old files without converting.
        if (options.RemoveOldCbz && !options.ConvertToWebp)
            return new RemoveOldCbzCommand();

        var transformations = new List<IXmlTransformation>();
        if (options.GenreToTags)
            transformations.Add(new GenreToTagsTransformation());

        return new BatchTransformCommand(
            new BatchProcessor(
                new ArchiveProcessor(options.DryRun, transformations, options.ConvertToWebp, options.RemoveOldCbz, options.Parallelism),
                options.Parallelism),
            options.DryRun);
    }

    private static string BuildFeatureLabel(AppOptions options)
    {
        var features = new List<string>();
        if (options.DetectUpscale) features.Add("DetectUpscale");
        if (options.GenreToTags) features.Add("GenreToTags");
        if (options.ConvertToWebp) features.Add(options.RemoveOldCbz ? "ConvertToWebP(RemoveOld)" : "ConvertToWebP(KeepOld)");
        if (options.RemoveOldCbz && !options.ConvertToWebp) features.Add("RemoveOldBackups");
        return features.Count > 0 ? string.Join(", ", features) : "BatchProcessing";
    }
}

