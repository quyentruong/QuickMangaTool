using System.Text;

class ReadLogsCommand : IAppCommand
{
    private readonly string _root;

    public ReadLogsCommand(string root) => _root = root;

    public int Execute(IReadOnlyList<string> cbzFiles, IRunLogger logger)
    {
        var logFiles = Directory.EnumerateFiles(_root, "*.log", SearchOption.AllDirectories).OrderBy(f => f).ToList();
        if (logFiles.Count == 0)
        {
            Console.WriteLine("No .log files found.");
            return 0;
        }

        if (logFiles.Count == 1)
        {
            PrintFile(logFiles[0]);
            return 0;
        }

        Console.WriteLine("Found the following log files:");
        for (int i = 0; i < logFiles.Count; i++)
        {
            Console.WriteLine($"  [{i + 1}] {logFiles[i]}");
        }

        Console.WriteLine();
        Console.Write("Select log to read by number, comma-separated list, or 'all': ");
        var input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input))
            return 0;

        input = input.Trim();
        if (input.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var lf in logFiles) PrintFile(lf);
            return 0;
        }

        var parts = input.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        var chosen = new HashSet<int>();
        foreach (var p in parts)
        {
            if (int.TryParse(p.Trim(), out var idx))
            {
                if (idx >= 1 && idx <= logFiles.Count)
                    chosen.Add(idx - 1);
            }
        }

        if (chosen.Count == 0)
        {
            Console.WriteLine("No valid selection made.");
            return 0;
        }

        foreach (var idx in chosen.OrderBy(i => i))
            PrintFile(logFiles[idx]);

        return 0;
    }

    private static void PrintFile(string path)
    {
        Console.WriteLine($"==== {path} ====");
        foreach (var line in File.ReadLines(path, Encoding.UTF8))
            Console.WriteLine(line);
        Console.WriteLine();
    }
}
