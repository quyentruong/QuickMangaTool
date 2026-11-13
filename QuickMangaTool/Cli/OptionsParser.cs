static class OptionsParser
{
    private static readonly HashSet<string> KnownSubcommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "convert", "compress", "pdf-to-cbz", "cbr-to-cbz", "upscale", "remove-old",
        "read-logs", "delete-logs", "diag", "clear-cache", "update"
    };

    public static bool IsKnownSubcommand(string? arg)
        => !string.IsNullOrWhiteSpace(arg) && KnownSubcommands.Contains(arg);

    /// <summary>
    /// Parses <paramref name="args"/> into <see cref="AppOptions"/>.
    /// Returns <see langword="null"/> when help was printed and the caller should exit with 0.
    /// </summary>
    public static AppOptions? Parse(string[] args, TextWriter output)
    {
        args ??= [];

        if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
        {
            PrintHelp(output);
            return null;
        }

        var interactive = args.Contains("--interactive") || args.Contains("-i");
        var confirm = args.Contains("--confirm") || args.Contains("-y");
        var dryRun = args.Contains("--dry-run") || args.Contains("-n");

        var convertImages = args.Contains("--convert") || args.Contains("--convert-to-webp") || args.Contains("-w") || args.Contains("convert");
        var compressFolders = args.Contains("--compress-folders") || args.Contains("--compress") || args.Contains("compress");
        var pdfToCbz = args.Contains("--pdf-to-cbz") || args.Contains("pdf-to-cbz");
        var cbrToCbz = args.Contains("--cbr-to-cbz") || args.Contains("cbr-to-cbz");
        var detectUpscale = args.Contains("--detect-upscale") || args.Contains("-u") || args.Contains("upscale");
        var removeOldCbz = args.Contains("--remove-old-cbz") || args.Contains("--remove-old") || args.Contains("remove-old");
        var readLogs = args.Contains("--read-logs") || args.Contains("read-logs");
        var deleteLogs = args.Contains("--delete-logs") || args.Contains("delete-logs");
        var vipsDiagnostics = args.Contains("--vips-diagnostics") || args.Contains("diag");
        var clearCache = args.Contains("--clear-cache") || args.Contains("-c") || args.Contains("clear-cache");
        var updateApp = args.Contains("--update") || args.Contains("update");
        var deleteSource = args.Contains("--delete-source");

        int parallelism = 0;
        var pArg = args.FirstOrDefault(a => a.StartsWith("--parallel="));
        if (pArg is not null)
        {
            var parts = pArg.Split('=', 2);
            if (parts.Length == 2 && int.TryParse(parts[1], out var pv) && pv >= 0)
                parallelism = pv;
        }

        int? effort = null;
        var effortArg = args.FirstOrDefault(a => a.StartsWith("--effort="));
        if (effortArg is not null)
        {
            var parts = effortArg.Split('=', 2);
            if (parts.Length == 2 && int.TryParse(parts[1], out var ev) && ev >= 0)
                effort = ev;
        }

        var directoryArg = args.FirstOrDefault(a => !a.StartsWith('-') && !KnownSubcommands.Contains(a));

        var imageFormat = ImageFormat.Webp;
        var fArg = args.FirstOrDefault(a => a.StartsWith("--image-format="));
        if (fArg is not null)
        {
            var parts = fArg.Split('=', 2);
            if (parts.Length == 2)
            {
                var v = parts[1].ToLowerInvariant();
                if (v == "avif") imageFormat = ImageFormat.Avif;
                else if (v == "jxl") imageFormat = ImageFormat.Jxl;
                else imageFormat = ImageFormat.Webp;
            }
        }

        bool hasAction = convertImages || compressFolders || pdfToCbz || cbrToCbz ||
                         detectUpscale || removeOldCbz || readLogs || deleteLogs ||
                         vipsDiagnostics || clearCache || updateApp || interactive;

        if (!hasAction)
        {
            PrintHelp(output);
            return null;
        }

        var directory = directoryArg ?? Directory.GetCurrentDirectory();

        return new AppOptions(
            Directory: directory,
            DryRun: dryRun,
            Confirm: confirm,
            Interactive: interactive,
            DetectUpscale: detectUpscale,
            CompressFolders: compressFolders,
            ConvertImages: convertImages,
            ImageFormat: imageFormat,
            RemoveOldCbz: removeOldCbz,
            DeleteSource: deleteSource,
            ReadLogs: readLogs,
            DeleteLogs: deleteLogs,
            VipsDiagnostics: vipsDiagnostics,
            PdfToCbz: pdfToCbz,
            CbrToCbz: cbrToCbz,
            Parallelism: parallelism,
            Effort: effort,
            ClearCache: clearCache,
            UpdateApp: updateApp);
    }

    private static void PrintHelp(TextWriter output)
    {
        output.WriteLine("Usage: QuickMangaTool [path] [command|options]");
        output.WriteLine();
        output.WriteLine("Commands & Operations:");
        output.WriteLine("  convert, -w, --convert-to-webp    Convert images inside .cbz to WebP/AVIF/JXL (when smaller)");
        output.WriteLine("  compress, --compress-folders      Compress subfolders into individual .cbz archives");
        output.WriteLine("  pdf-to-cbz, --pdf-to-cbz          Extract images from PDF files into .cbz archives");
        output.WriteLine("  cbr-to-cbz, --cbr-to-cbz          Convert .cbr archives to .cbz");
        output.WriteLine("  upscale, -u, --detect-upscale     Analyze .cbz image quality (dimensions, DPI, blur, noise)");
        output.WriteLine("  remove-old, --remove-old-cbz      Delete leftover .old.cbz backup files");
        output.WriteLine("  read-logs, --read-logs            Interactively view run log files from the logs directory");
        output.WriteLine("  delete-logs, --delete-logs        Delete all .log files in the centralized logs directory");
        output.WriteLine("  diag, --vips-diagnostics          Print NetVips configuration, codecs, and storage paths");
        output.WriteLine("  clear-cache, -c, --clear-cache    Clear the skip cache file in the cache directory");
        output.WriteLine("  update, --update                  Check and update QuickMangaTool from GitHub Releases");
        output.WriteLine();
        output.WriteLine("Options:");
        output.WriteLine("  -i, --interactive                 Open interactive menu (default when run without arguments in terminal)");
        output.WriteLine("  -n, --dry-run                     Preview changes without modifying .cbz archives (convert command)");
        output.WriteLine("  -y, --confirm                     Apply deletions or confirm prompts without asking");
        output.WriteLine("  --delete-source                   Delete original file/folder after successful .cbz creation");
        output.WriteLine("  --image-format=webp|avif|jxl      Target image format (default: webp)");
        output.WriteLine("  --effort=N                        Encoder effort level (WebP: 0-6, JXL: 1-9, AVIF: 0-9)");
        output.WriteLine("  --parallel=N                      Number of parallel workers (default: tuned for CPU/IO)");
        output.WriteLine("  -h, --help                        Show this help message");
        output.WriteLine();
        output.WriteLine("Examples:");
        output.WriteLine("  QuickMangaTool                                    Launch interactive menu");
        output.WriteLine("  QuickMangaTool /path/to/manga convert             Convert images to WebP");
        output.WriteLine("  QuickMangaTool /path/to/manga convert --dry-run   Preview WebP conversion size delta");
        output.WriteLine("  QuickMangaTool /path/to/manga convert --image-format=avif --effort=4");
        output.WriteLine("  QuickMangaTool /path/to/manga convert --image-format=jxl --effort=7");
        output.WriteLine("  QuickMangaTool /path/to/folders compress --delete-source");
        output.WriteLine("  QuickMangaTool /path/to/pdfs pdf-to-cbz --delete-source");
        output.WriteLine("  QuickMangaTool /path/to/cbrs cbr-to-cbz --delete-source");
        output.WriteLine("  QuickMangaTool /path/to/manga upscale");
        output.WriteLine("  QuickMangaTool /path/to/manga remove-old -y");
        output.WriteLine("  QuickMangaTool --vips-diagnostics");
    }
}
