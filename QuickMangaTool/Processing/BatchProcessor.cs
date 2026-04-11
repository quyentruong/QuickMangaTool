using Spectre.Console;

class BatchProcessor
{
    private readonly IArchiveProcessor _archiveProcessor;
    private readonly IBatchLogger _logger;

    public BatchProcessor(IArchiveProcessor archiveProcessor, IBatchLogger logger)
    {
        _archiveProcessor = archiveProcessor;
        _logger = logger;
    }

    public (int updated, int wouldUpdate) Process(IReadOnlyList<string> files)
    {
        int updatedCount = 0;
        int wouldUpdateCount = 0;

        AnsiConsole.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(new ProgressColumn[]
            {
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn(),
                new TaskDescriptionColumn(),
            })
            .Start(ctx =>
            {
                var task = ctx.AddTask("Processing .cbz files", maxValue: files.Count);
                const int descriptionWidth = 100;

                foreach (var cbz in files)
                {
                    var result = _archiveProcessor.Process(cbz);
                    _logger.WriteEntry(result);

                    switch (result.Status)
                    {
                        case ProcessStatus.Updated:     updatedCount++;     break;
                        case ProcessStatus.WouldUpdate: wouldUpdateCount++; break;
                    }

                    var label = result.Status switch
                    {
                        ProcessStatus.Updated     => "Updated",
                        ProcessStatus.WouldUpdate => "Would update",
                        ProcessStatus.Error       => "Error",
                        _                         => "No change",
                    };

                    task.Description = Markup.Escape(FormatDescription(label, cbz, descriptionWidth));
                    task.Increment(1);
                }
            });

        return (updatedCount, wouldUpdateCount);
    }

    private static string FormatDescription(string status, string path, int width)
    {
        var prefix = string.IsNullOrEmpty(status) ? string.Empty : status + ": ";
        if (width <= 0) return prefix + path;

        if (prefix.Length >= width)
            return prefix[..width];

        var remaining = width - prefix.Length;
        string displayPath = path.Length <= remaining
            ? path
            : remaining > 3 ? "..." + path[^(remaining - 3)..] : path[^remaining..];

        var result = prefix + displayPath;
        return result.Length < width ? result.PadRight(width) : result;
    }
}
