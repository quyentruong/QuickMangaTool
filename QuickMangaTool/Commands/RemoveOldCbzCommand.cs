using Spectre.Console;

/// <summary>
/// Standalone command that deletes all <c>.old.cbz</c> backup files left behind
/// by a previous <c>--convert-to-webp</c> run.
/// </summary>
class RemoveOldCbzCommand : IAppCommand
{
    public int Execute(IReadOnlyList<string> cbzFiles, IRunLogger logger)
    {
        // Derive .old.cbz paths from the discovered .cbz files so the search root is reused
        var oldFiles = cbzFiles
            .Select(f => Path.Combine(
                Path.GetDirectoryName(f) ?? string.Empty,
                Path.GetFileNameWithoutExtension(f) + ".old" + Path.GetExtension(f)))
            .Where(File.Exists)
            .ToList();

        if (oldFiles.Count == 0)
        {
            Console.WriteLine("No .cbz.old backup files found.");
            return 0;
        }

        int deleted = 0;
        int failed = 0;

        ProgressFactory.Create().Start(ctx =>
        {
            var task = ctx.AddTask("Removing .old.cbz backups", maxValue: oldFiles.Count);
            const int descriptionWidth = 100;

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
                    result.FilePath,
                    descriptionWidth));
                task.Increment(1);
            }
        });

        logger.WriteSummary(oldFiles.Count, deleted, 0);
        Console.WriteLine($"Removed {deleted} .old.cbz backup file(s)." + (failed > 0 ? $" {failed} failed." : string.Empty));
        return failed > 0 ? 1 : 0;
    }
}
