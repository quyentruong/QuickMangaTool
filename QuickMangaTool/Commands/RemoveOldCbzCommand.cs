using Spectre.Console;

/// <summary>
/// Standalone command that deletes all <c>.old.cbz</c> backup files left behind
/// by a previous conversion run.
/// </summary>
class RemoveOldCbzCommand : IAppCommand
{
    private readonly string? _root;
    private readonly bool _confirm;

    public RemoveOldCbzCommand(string? root = null, bool confirm = false)
    {
        _root = root;
        _confirm = confirm;
    }

    public int Execute(IReadOnlyList<string> cbzFiles, IRunLogger logger)
    {
        var oldFilesSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(_root) && Directory.Exists(_root))
        {
            foreach (var f in Directory.EnumerateFiles(_root, "*.old.cbz", SearchOption.AllDirectories))
                oldFilesSet.Add(f);

            foreach (var f in Directory.EnumerateFiles(_root, "*.cbz.old", SearchOption.AllDirectories))
                oldFilesSet.Add(f);
        }

        if (cbzFiles != null)
        {
            foreach (var f in cbzFiles)
            {
                var dir = Path.GetDirectoryName(f) ?? string.Empty;
                var baseName = Path.GetFileNameWithoutExtension(f);
                var ext = Path.GetExtension(f);

                var p1 = Path.Combine(dir, baseName + ".old" + ext);
                if (File.Exists(p1)) oldFilesSet.Add(p1);

                var p2 = Path.Combine(dir, Path.GetFileName(f) + ".old");
                if (File.Exists(p2)) oldFilesSet.Add(p2);
            }
        }

        var oldFiles = oldFilesSet.Where(File.Exists).OrderBy(x => x).ToList();

        if (oldFiles.Count == 0)
        {
            Console.WriteLine("No .old.cbz backup files found.");
            return 0;
        }

        Console.WriteLine("Found the following .old.cbz backup files:");
        foreach (var f in oldFiles)
        {
            Console.WriteLine(f);
        }

        if (!_confirm)
        {
            Console.Write($"Delete these {oldFiles.Count} file(s)? (y/N): ");
            var answer = Console.ReadLine();
            if (!string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Aborted. No files deleted.");
                return 0;
            }
        }

        int deleted = 0;
        int failed = 0;

        ProgressFactory.Create().Start(ctx =>
        {
            var task = ctx.AddTask("Removing .old.cbz backups", maxValue: oldFiles.Count);

            foreach (var oldFile in oldFiles)
            {
                ProcessResult result;
                try
                {
                    File.Delete(oldFile);
                    deleted++;
                    result = new ProcessResult(oldFile, ProcessStatus.Updated);
                }
                catch (Exception ex)
                {
                    failed++;
                    result = new ProcessResult(oldFile, ProcessStatus.Error, ex.Message);
                }

                logger.WriteEntry(result);
                task.Description = Markup.Escape(ProgressDescriptionFormatter.Format(
                    result.Status == ProcessStatus.Error ? "Error" : "Deleted",
                    oldFile));
                task.Increment(1);
            }
        });

        logger.WriteSummary(oldFiles.Count, deleted, 0);
        Console.WriteLine($"Removed {deleted} .old.cbz backup file(s)." + (failed > 0 ? $" {failed} failed." : string.Empty));
        return failed > 0 ? 1 : 0;
    }
}
