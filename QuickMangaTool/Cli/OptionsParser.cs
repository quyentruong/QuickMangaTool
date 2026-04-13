static class OptionsParser
{
    /// <summary>
    /// Parses <paramref name="args"/> into <see cref="AppOptions"/>.
    /// Returns <see langword="null"/> when help was printed and the caller should exit with 0.
    /// </summary>
    public static AppOptions? Parse(string[] args, TextWriter output)
    {
        args ??= [];

        if (args.Contains("--help") || args.Contains("-h"))
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

        var directory = args.FirstOrDefault(a => !a.StartsWith('-')) ?? Directory.GetCurrentDirectory();

        return new AppOptions(directory, dryRun, genreToTags, detectUpscale, parallelism);
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
        output.WriteLine();
        output.WriteLine("Examples:");
        output.WriteLine("  QuickMangaTool.exe            Run dry-run in current directory (default)");
        output.WriteLine("  QuickMangaTool.exe C:\\manga -n  Dry-run in C:\\manga");
        output.WriteLine("  QuickMangaTool.exe C:\\manga -y  Actually apply changes in C:\\manga");
    }
}
