using Spectre.Console;
using System.Collections.Concurrent;

class BatchProcessor : IBatchProcessor
{
    private readonly IArchiveProcessor _archiveProcessor;
    private readonly int _parallelism;

    public BatchProcessor(IArchiveProcessor archiveProcessor, int parallelism = 0)
    {
        _archiveProcessor = archiveProcessor;
        _parallelism = parallelism;
    }

    public (int updated, int wouldUpdate) Process(IReadOnlyList<string> files, IEntryLogger logger)
    {
        int updatedCount = 0;
        int wouldUpdateCount = 0;
        int workers = WorkerCount.Resolve(_parallelism);
        const int descriptionWidth = 100;

        // Two channels: one for in-progress file names, one for completed results.
        using var statusUpdates = new BlockingCollection<(string Label, string Path)>();
        using var results = new BlockingCollection<ProcessResult>();

        var producer = Task.Run(() =>
        {
            var po = new ParallelOptions { MaxDegreeOfParallelism = workers };
            Parallel.ForEach(files, po, cbz =>
            {
                // Push a "Converting" status as soon as a worker picks up the file
                statusUpdates.Add(("Converting", cbz));
                try
                {
                    results.Add(_archiveProcessor.Process(cbz));
                }
                catch (Exception ex)
                {
                    results.Add(new ProcessResult(cbz, ProcessStatus.Error, ex.Message));
                }
            });
            statusUpdates.CompleteAdding();
            results.CompleteAdding();
        });

        ProgressFactory.Create().Start(ctx =>
        {
            var task = ctx.AddTask(
                Markup.Escape(ProgressDescriptionFormatter.Format("Waiting", files[0], descriptionWidth)),
                maxValue: files.Count);

            // Drain both channels; status updates move the label forward while
            // result processing advances the progress bar and logs the outcome.
            var statusEnumerator = statusUpdates.GetConsumingEnumerable().GetEnumerator();
            foreach (var result in results.GetConsumingEnumerable())
            {
                // Advance the "Converting: <file>" label from the status channel
                if (statusEnumerator.MoveNext())
                {
                    var (statusLabel, statusPath) = statusEnumerator.Current;
                    task.Description = Markup.Escape(ProgressDescriptionFormatter.Format(statusLabel, statusPath, descriptionWidth));
                }

                logger.WriteEntry(result);

                switch (result.Status)
                {
                    case ProcessStatus.Updated: updatedCount++; break;
                    case ProcessStatus.WouldUpdate: wouldUpdateCount++; break;
                }

                var doneLabel = result.Status switch
                {
                    ProcessStatus.Updated => "Converted",
                    ProcessStatus.WouldUpdate => "Would convert",
                    ProcessStatus.Error => "Error",
                    _ => "No conversion",
                };

                task.Description = Markup.Escape(ProgressDescriptionFormatter.Format(doneLabel, result.FilePath, descriptionWidth));
                task.Increment(1);
            }
        });

        producer.Wait();
        return (updatedCount, wouldUpdateCount);
    }
}
