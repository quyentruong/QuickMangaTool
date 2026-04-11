class Program
{
    static int Main(string[] args)
    {
        var options = OptionsParser.Parse(args, Console.Out);
        if (options is null)
            return 0;

        if (options.DryRun)
            Console.WriteLine("Dry-run mode: no archives will be modified. Use --confirm or -y to apply changes.");

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
        logger.WriteHeader(DateTime.Now, options.DryRun);

        IXmlTransformation[] transformations = [new GenreToTagsTransformation()];
        var archiveProcessor = new ArchiveProcessor(options.DryRun, transformations);
        var batchProcessor = new BatchProcessor(archiveProcessor, logger);

        var (updated, wouldUpdate) = batchProcessor.Process(cbzFiles);

        logger.WriteSummary(cbzFiles.Count, updated, wouldUpdate);

        if (options.DryRun)
        {
            Console.WriteLine($"Processed {cbzFiles.Count} .cbz files. Would update: {wouldUpdate}.");
            Console.WriteLine("No changes were made. Re-run with --confirm or -y to apply changes.");
        }
        else
        {
            Console.WriteLine($"Processed {cbzFiles.Count} .cbz files. Updated: {updated}.");
        }
        Console.WriteLine($"Log saved to: {logFilePath}");

        return 0;
    }
}
