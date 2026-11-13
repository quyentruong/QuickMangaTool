using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Globalization;

class ReadLogsCommand : IAppCommand
{
    private readonly string _logsDir;

    public ReadLogsCommand(string? logsDir = null)
        => _logsDir = string.IsNullOrWhiteSpace(logsDir) ? AppPaths.GetLogsDirectory() : logsDir;

    public int Execute(IReadOnlyList<string> cbzFiles, IRunLogger logger)
    {
        var logEntries = new List<(string Path, DateTime? Timestamp)>();

        if (!string.IsNullOrWhiteSpace(_logsDir) && Directory.Exists(_logsDir))
        {
            try
            {
                foreach (var f in Directory.EnumerateFiles(_logsDir, "*.log", SearchOption.TopDirectoryOnly))
                {
                    logEntries.Add((f, ParseTimestampFromFilename(f)));
                }
            }
            catch { /* best-effort */ }
        }

        var logFiles = logEntries
            .OrderBy(x => x.Timestamp ?? DateTime.MinValue)
            .ThenBy(x => x.Path)
            .Select(x => x.Path)
            .ToList();

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
        string? input = Console.ReadLine();
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
        Console.WriteLine($"==== {path} ====\n");

        // If output is redirected (e.g. piped to less), just write raw lines.
        if (Console.IsOutputRedirected)
        {
            foreach (var line in File.ReadLines(path, Encoding.UTF8))
                Console.WriteLine(line);
            Console.WriteLine();
            return;
        }

        // Prefer external pager if configured or available
        var pagerEnv = Environment.GetEnvironmentVariable("PAGER");
        string? pager = null;
        if (!string.IsNullOrWhiteSpace(pagerEnv))
            pager = pagerEnv;
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            pager = "more.com"; // use native more.com which works correctly in PowerShell
        else
            pager = "less"; // common on Unix-like systems

        if (!string.IsNullOrWhiteSpace(pager))
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = pager,
                    Arguments = $"\"{path}\"",
                    UseShellExecute = false,
                    RedirectStandardInput = false,
                    RedirectStandardOutput = false,
                    CreateNoWindow = false
                };
                using var proc = Process.Start(psi);
                proc?.WaitForExit();
                Console.WriteLine();
                return;
            }
            catch
            {
                // fall through to in-app pager
            }
        }

        // Fallback: simple in-app pager (space = next page, Enter = next line, q = quit)
        var enumerator = File.ReadLines(path, Encoding.UTF8).GetEnumerator();
        int pageSize = Math.Max(1, Console.WindowHeight - 2);
        int shown = 0;
        while (enumerator.MoveNext())
        {
            Console.WriteLine(enumerator.Current);
            shown++;
            if (shown % pageSize == 0)
            {
                Console.Write("--More-- (Space=page, Enter=line, q=quit) ");
                var key = Console.ReadKey(intercept: true);
                Console.WriteLine();
                if (key.Key == ConsoleKey.Q)
                    break;

                if (key.Key == ConsoleKey.Enter)
                {
                    // show one more line immediately
                    if (enumerator.MoveNext())
                    {
                        Console.WriteLine(enumerator.Current);
                        shown++;
                    }
                }
                else if (key.Key == ConsoleKey.Spacebar)
                {
                    // start next page; recalc in case window resized
                    pageSize = Math.Max(1, Console.WindowHeight - 2);
                    continue;
                }
                else
                {
                    // any other key: quit
                    break;
                }
            }
        }

        Console.WriteLine();
    }

    private static DateTime? ParseTimestampFromFilename(string path)
    {
        try
        {
            var name = Path.GetFileNameWithoutExtension(path);
            var m = Regex.Match(name, @"\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}");
            if (!m.Success)
                return null;

            if (DateTime.TryParseExact(m.Value, "yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return dt;
        }
        catch
        {
            // ignore parse errors and treat as no timestamp
        }

        return null;
    }
}
