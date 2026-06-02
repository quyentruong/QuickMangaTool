class DeleteLogsCommand : IAppCommand
{
    private readonly string _root;

    public DeleteLogsCommand(string root) => _root = root;

    public int Execute(IReadOnlyList<string> cbzFiles, IRunLogger logger)
    {
        var logFiles = Directory.EnumerateFiles(_root, "*.log", SearchOption.AllDirectories).ToList();
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

        Console.Write($"Delete these {logFiles.Count} file(s)? (y/N): ");
        var answer = Console.ReadLine();
        if (!string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Aborted. No files deleted.");
            return 0;
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
