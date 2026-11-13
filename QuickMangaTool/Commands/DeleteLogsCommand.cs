class DeleteLogsCommand : IAppCommand
{
    private readonly string _logsDir;
    private readonly bool _confirm;

    public DeleteLogsCommand(bool confirm = false)
        : this(AppPaths.GetLogsDirectory(), confirm)
    {
    }

    public DeleteLogsCommand(string root, bool confirm = false)
    {
        _logsDir = string.IsNullOrWhiteSpace(root) ? AppPaths.GetLogsDirectory() : root;
        _confirm = confirm;
    }

    public int Execute(IReadOnlyList<string> cbzFiles, IRunLogger logger)
    {
        var logFiles = Directory.Exists(_logsDir)
            ? Directory.EnumerateFiles(_logsDir, "*.log", SearchOption.TopDirectoryOnly).ToList()
            : new List<string>();
        if (logFiles.Count == 0)
        {
            Console.WriteLine("No .log files found.");
            return 0;
        }
        Console.WriteLine("Found the following .log files:");
        foreach (var lf in logFiles)
        {
            Console.WriteLine(lf);
        }

        if (!_confirm)
        {
            Console.Write($"Delete these {logFiles.Count} file(s)? (y/N): ");
            var answer = Console.ReadLine();
            if (!string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Aborted. No files deleted.");
                return 0;
            }
        }

        int deleted = 0;
        int failed = 0;
        foreach (var lf in logFiles)
        {
            try
            {
                File.Delete(lf);
                deleted++;
                Console.WriteLine($"Deleted: {lf}");
            }
            catch (Exception ex)
            {
                failed++;
                Console.Error.WriteLine($"Failed to delete {lf}: {ex.Message}");
            }
        }

        Console.WriteLine($"Deleted {deleted} .log file(s)." + (failed > 0 ? $" {failed} failed." : string.Empty));
        return failed > 0 ? 1 : 0;
    }
}
