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
        var genreToTags   = args.Contains("--genre-to-tags")   || args.Contains("-g");
        var detectUpscale = args.Contains("--detect-upscale")  || args.Contains("-u");

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
        // of running a no-op. Valid feature flags are --genre-to-tags and --detect-upscale.
        if (directoryArg is not null && !(genreToTags || detectUpscale || clearCache))
        {
            PrintHelp(output);
            return null;
        }

        var directory = directoryArg ?? Directory.GetCurrentDirectory();

        return new AppOptions(directory, dryRun, genreToTags, detectUpscale, parallelism, clearCache);
    }

    private static void PrintHelp(TextWriter output)
    {
        output.WriteLine("Usage: QuickMangaTool [path] [options]");
        output.WriteLine();
        output.WriteLine("Options:");
        output.WriteLine("  --help, -h       Show this help and exit");
        output.WriteLine("  --dry-run, -n    Run in dry-run mode (no changes). This is the default");
        output.WriteLine("  --confirm, -y    Apply changes to .cbz files (must be provided to modify archives)");
        output.WriteLine("  --genre-to-tags, -g  Apply Genre -> Tags transformation to XML metadata inside archives");
        output.WriteLine("  --detect-upscale, -u  Analyze images inside .cbz and report which archives need upscaling");
        output.WriteLine("  --parallel=N       Set parallel worker count for detection (default = tuned for disk)");
        output.WriteLine("  --clear-cache, -c  Remove the cached list of previously-clean archives and exit");
        output.WriteLine();
        output.WriteLine("Examples:");
        output.WriteLine("  QuickMangaTool.exe            Run dry-run in current directory (default)");
        output.WriteLine("  QuickMangaTool.exe C:\\manga -n  Dry-run in C:\\manga");
        output.WriteLine("  QuickMangaTool.exe C:\\manga -y  Actually apply changes in C:\\manga");
    }
}
