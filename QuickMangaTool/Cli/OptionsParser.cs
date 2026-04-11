static class OptionsParser
{
    /// <summary>
    /// Parses <paramref name="args"/> into <see cref="AppOptions"/>.
    /// Returns <see langword="null"/> when help was printed and the caller should exit with 0.
    /// </summary>
    public static AppOptions? Parse(string[] args, TextWriter output)
    {
        if (args != null && (args.Contains("--help") || args.Contains("-h")))
        {
            PrintHelp(output);
            return null;
        }

        var confirm = args != null && (args.Contains("--confirm") || args.Contains("-y"));
        var dryRun = !confirm;

        if (args != null && (args.Contains("--dry-run") || args.Contains("-n")))
            dryRun = true;

        var directory = args != null
            ? args.FirstOrDefault(a => !a.StartsWith('-')) ?? Directory.GetCurrentDirectory()
            : Directory.GetCurrentDirectory();

        return new AppOptions(directory, dryRun);
    }

    private static void PrintHelp(TextWriter output)
    {
        output.WriteLine("Usage: QuickMangaTool [path] [options]");
        output.WriteLine();
        output.WriteLine("Options:");
        output.WriteLine("  --help, -h       Show this help and exit");
        output.WriteLine("  --dry-run, -n    Run in dry-run mode (no changes). This is the default");
        output.WriteLine("  --confirm, -y    Apply changes to .cbz files (must be provided to modify archives)");
        output.WriteLine();
        output.WriteLine("Examples:");
        output.WriteLine("  QuickMangaTool.exe            Run dry-run in current directory (default)");
        output.WriteLine("  QuickMangaTool.exe C:\\manga -n  Dry-run in C:\\manga");
        output.WriteLine("  QuickMangaTool.exe C:\\manga -y  Actually apply changes in C:\\manga");
    }
}
