static class OptionsParser
{
    /// <summary>
    /// Parses <paramref name="args"/> into <see cref="AppOptions"/>.
    /// Returns <see langword="null"/> when help was printed and the caller should exit with 0.
    /// </summary>
    public static AppOptions? Parse(string[] args, TextWriter output)
    {
        args ??= [];

        // If no args supplied, show help by default.
        if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
        {
            PrintHelp(output);
            return null;
        }

        var confirm  = args.Contains("--confirm")  || args.Contains("-y");
        var dryRun   = !confirm || args.Contains("--dry-run") || args.Contains("-n");
        var addDuplicates = args.Contains("--add-duplicates") || args.Contains("--add-dups") || args.Contains("-a");
        var genreToTags   = args.Contains("--genre-to-tags")   || args.Contains("-g");
        var detectUpscale = args.Contains("--detect-upscale")  || args.Contains("-u");
        var detectDuplicates = args.Contains("--detect-duplicates") || args.Contains("--detect-dups");
        var convertToWebp = args.Contains("--convert-to-webp")  || args.Contains("-w");
        var compressFolders = args.Contains("--compress-folders") || args.Contains("--compress");
        var removeOldCbz = args.Contains("--remove-old-cbz")  || args.Contains("--remove-old");
        var readLogs = args.Contains("--read-logs");
        var deleteLogs = args.Contains("--delete-logs");

        int phashThreshold = 8;
        var tArg = args.FirstOrDefault(a => a.StartsWith("--phash-threshold="));
        if (tArg is not null)
        {
            var parts = tArg.Split('=', 2);
            if (parts.Length == 2 && int.TryParse(parts[1], out var tv) && tv >= 0)
                phashThreshold = tv;
        }

        int parallelism = 0;
        var pArg = args.FirstOrDefault(a => a.StartsWith("--parallel="));
        if (pArg is not null)
        {
            var parts = pArg.Split('=', 2);
            if (parts.Length == 2 && int.TryParse(parts[1], out var pv))
                parallelism = pv;
        }

        var clearCache = args.Contains("--clear-cache") || args.Contains("-c");

        var directoryArg = args.FirstOrDefault(a => !a.StartsWith('-'));

        // If the user supplied only a path but no feature flags, show help instead
        // of running a no-op. Valid feature flags are --genre-to-tags, --detect-upscale,
        // compress-folders, and the standalone log commands.
        if (directoryArg is not null && !(addDuplicates || genreToTags || detectUpscale || detectDuplicates || compressFolders || convertToWebp || removeOldCbz || clearCache || readLogs || deleteLogs))
        {
            PrintHelp(output);
            return null;
        }

        var directory = directoryArg ?? Directory.GetCurrentDirectory();

        return new AppOptions(directory, dryRun, addDuplicates, genreToTags, detectUpscale, detectDuplicates, compressFolders, convertToWebp, removeOldCbz, readLogs, deleteLogs, parallelism, phashThreshold, clearCache);
    }

    private static void PrintHelp(TextWriter output)
    {
        output.WriteLine("Usage: QuickMangaTool [path] [options]");
        output.WriteLine();
        output.WriteLine("Options:");
        output.WriteLine("  --help, -h       Show this help and exit");
        output.WriteLine("  --dry-run, -n    Run in dry-run mode (no changes). This is the default");
        output.WriteLine("  --confirm, -y    Apply changes to .cbz files (must be provided to modify archives)");
        output.WriteLine("  --add-duplicates, --add-dups, -a  Scan image files and add their pHash values to duplicates.db next to the executable");
        output.WriteLine("  --genre-to-tags, -g      Apply Genre -> Tags transformation to XML metadata inside archives");
        output.WriteLine("  --detect-upscale, -u     Analyze images inside .cbz and report which archives need upscaling");
        output.WriteLine("  --detect-duplicates      Scan .cbz archives and detect duplicate images using duplicates.db and pHash similarity");
        output.WriteLine("  --convert-to-webp, -w    Convert image entries inside .cbz archives to .webp");
        output.WriteLine("  --compress-folders, --compress    Compress all subfolders under the given path into individual .cbz files");
        output.WriteLine("  --remove-old-cbz         Remove .old backup files (standalone or combined with --convert-to-webp)");
        output.WriteLine("                             Standalone: deletes leftover .cbz.old files from a previous conversion run");
        output.WriteLine("  --read-logs               Print all .log files found under the given path to the console");
        output.WriteLine("  --delete-logs             Delete all .log files found under the given path");
        output.WriteLine("  --parallel=N             Set parallel worker count for detection (default = tuned for disk)");
        output.WriteLine("  --phash-threshold=N      Maximum Hamming distance for pHash duplicate matching (default: 8)");
        output.WriteLine("  --clear-cache, -c        Remove the cached list of previously-clean archives and exit");
        output.WriteLine();
        output.WriteLine("Examples:");
        output.WriteLine("  QuickMangaTool.exe            Run dry-run in current directory (default)");
        output.WriteLine("  QuickMangaTool.exe C:\\manga -n  Dry-run in C:\\manga");
        output.WriteLine("  QuickMangaTool.exe C:\\manga -y  Actually apply changes in C:\\manga");
        output.WriteLine("  QuickMangaTool.exe C:\\manga -w -n       Dry-run WebP conversion with size diff");
        output.WriteLine("  QuickMangaTool.exe C:\\manga -w -y       Convert images to WebP and keep .old backup");
        output.WriteLine("  QuickMangaTool.exe C:\\manga -w -y --remove-old-cbz  Convert and delete .old backup");
        output.WriteLine("  QuickMangaTool.exe C:\\manga --add-duplicates -y  Add image pHash values to duplicates.db");
        output.WriteLine("  QuickMangaTool.exe C:\\manga --detect-duplicates --phash-threshold=8  Detect duplicates in archives");
        output.WriteLine("  QuickMangaTool.exe C:\\manga --read-logs  Print logs under C:\\manga");
        output.WriteLine("  QuickMangaTool.exe C:\\manga --delete-logs  Delete logs under C:\\manga");
    }
}
