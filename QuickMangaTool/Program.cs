class Program
{
    static int Main(string[] args)
    {
        AppOptions? options = null;

        // If interactive flag passed explicitly, or if run without flags in an interactive console
        if (args.Contains("--interactive") || args.Contains("-i") ||
            (!Console.IsInputRedirected && (args.Length == 0 || (args.Length == 1 && !args[0].StartsWith('-')))))
        {
            var initialDir = args.FirstOrDefault(a => !a.StartsWith('-')) ?? Directory.GetCurrentDirectory();
            options = InteractiveMenu.Run(initialDir);
            if (options is null)
                return 0;
        }
        else
        {
            options = OptionsParser.Parse(args, Console.Out);
            if (options is null)
                return 0;
        }

        if (options.ClearCache)
        {
            SkipCache.Clear();
            Console.WriteLine("Cache cleared: ~/.quickmangatool/skipcache.dat");
            return 0;
        }

        if (options.DryRun && options.ConvertImages)
        {
            Console.WriteLine("Dry-run mode: no archives will be modified. Re-run without --dry-run or -n to apply changes.");
        }

        if (!Directory.Exists(options.Directory))
        {
            Console.Error.WriteLine($"Directory not found: {options.Directory}");
            return 1;
        }

        // Standalone commands that do not produce batch log files
        if (options.ReadLogs)
            return new ReadLogsCommand(options.Directory).Execute(Array.Empty<string>(), new NoopRunLogger());

        if (options.DeleteLogs)
            return new DeleteLogsCommand(options.Directory, options.Confirm).Execute(Array.Empty<string>(), new NoopRunLogger());

        if (options.VipsDiagnostics)
            return new VipsDiagnosticCommand().Execute(Array.Empty<string>(), new NoopRunLogger());

        IArchiveImageConverter imageConverter = options.ImageFormat switch
        {
            ImageFormat.Avif => new AvifArchiveImageConverter(effort: options.Effort ?? 3),
            ImageFormat.Jxl => new JxlArchiveImageConverter(effort: options.Effort ?? 5),
            _ => new WebpArchiveImageConverter(effort: options.Effort ?? 6)
        };

        // Standalone archive creation commands that scan their own inputs
        if (options.PdfToCbz)
        {
            var command = new ExtractPdfToCbzCommand(options.Directory, imageConverter, options.DeleteSource);
            return ExecuteWithLogging(command, options, Array.Empty<string>());
        }

        if (options.CbrToCbz)
        {
            var command = new ConvertCbrToCbzCommand(options.Directory, options.DeleteSource);
            return ExecuteWithLogging(command, options, Array.Empty<string>());
        }

        if (options.CompressFolders)
        {
            var command = new CompressFoldersCommand(options.Directory, imageConverter, options.DeleteSource);
            return ExecuteWithLogging(command, options, Array.Empty<string>());
        }

        if (options.RemoveOldCbz && !options.ConvertImages)
        {
            var command = new RemoveOldCbzCommand(options.Directory, options.Confirm);
            return ExecuteWithLogging(command, options, Array.Empty<string>());
        }

        // Commands requiring existing .cbz archives
        var cbzFiles = Directory.EnumerateFiles(options.Directory, "*.cbz", SearchOption.AllDirectories)
            .Where(f => !f.EndsWith(".old.cbz", StringComparison.OrdinalIgnoreCase) &&
                        !f.EndsWith(".cbz.old", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (cbzFiles.Count == 0)
        {
            Console.WriteLine("No .cbz files found.");
            return 0;
        }

        if (options.DetectUpscale)
        {
            var command = new UpscaleScanCommand(new UpscaleDetector(new NetVipsQualityAnalyzer()), options.Parallelism);
            return ExecuteWithLogging(command, options, cbzFiles);
        }

        if (options.ConvertImages)
        {
            var archiveProcessor = new ArchiveProcessor(
                options.DryRun,
                options.ConvertImages,
                options.RemoveOldCbz,
                options.Parallelism,
                imageConverter);

            var batchProcessor = new BatchProcessor(archiveProcessor, options.Parallelism);
            var command = new BatchTransformCommand(batchProcessor, options.DryRun);
            return ExecuteWithLogging(command, options, cbzFiles);
        }

        return 0;
    }

    private static int ExecuteWithLogging(IAppCommand command, AppOptions options, IReadOnlyList<string> inputFiles)
    {
        var logFileName = $"QuickMangaTool_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log";
        string logFilePath;

        var exeDir = AppContext.BaseDirectory ?? AppDomain.CurrentDomain.BaseDirectory;
        try
        {
            var fullExeDir = Path.GetFullPath(exeDir);
            if (!Directory.Exists(fullExeDir))
                Directory.CreateDirectory(fullExeDir);

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

    private static string BuildFeatureLabel(AppOptions options)
    {
        var features = new List<string>();
        if (options.DetectUpscale) features.Add("Upscale");
        if (options.PdfToCbz) features.Add("PdfToCbz");
        if (options.CbrToCbz) features.Add("CbrToCbz");
        if (options.CompressFolders) features.Add("CompressFolders");
        if (options.ConvertImages) features.Add($"Convert({options.ImageFormat})" + (options.RemoveOldCbz ? "[RemoveOld]" : string.Empty));
        if (options.RemoveOldCbz && !options.ConvertImages) features.Add("RemoveOldBackups");
        return features.Count > 0 ? string.Join(", ", features) : "BatchProcessing";
    }
}
