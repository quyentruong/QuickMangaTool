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

        var cbzFiles = Directory.EnumerateFiles(options.Directory, "*.cbz", SearchOption.AllDirectories).ToList();
        if (cbzFiles.Count == 0)
        {
            Console.WriteLine("No .cbz files found.");
            return 0;
        }

        var logFilePath = Path.Combine(options.Directory, $"QuickMangaTool_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");

        using var logger = new ColoredFileLogger(logFilePath);
        logger.WriteHeader(DateTime.Now, options.DryRun, BuildFeatureLabel(options));

        var command = BuildCommand(options);
        int result = command.Execute(cbzFiles, logger);
        Console.WriteLine($"Log saved to: {logFilePath}");
        return result;
    }

    private static IAppCommand BuildCommand(AppOptions options)
    {
        if (options.DetectUpscale)
            return new UpscaleScanCommand(new UpscaleDetector(new ImageSharpQualityAnalyzer()), options.Parallelism);

        var transformations = new List<IXmlTransformation>();
        if (options.GenreToTags)
            transformations.Add(new GenreToTagsTransformation());

        return new BatchTransformCommand(
            new BatchProcessor(new ArchiveProcessor(options.DryRun, transformations), options.Parallelism),
            options.DryRun);
    }

    private static string BuildFeatureLabel(AppOptions options)
    {
        var features = new List<string>();
        if (options.DetectUpscale) features.Add("DetectUpscale");
        if (options.GenreToTags) features.Add("GenreToTags");
        return features.Count > 0 ? string.Join(", ", features) : "BatchProcessing";
    }
}

