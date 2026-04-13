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

        using var results = new BlockingCollection<ProcessResult>();

        var producer = Task.Run(() =>
        {
            var po = new ParallelOptions { MaxDegreeOfParallelism = workers };
            Parallel.ForEach(files, po, cbz =>
            {
                results.Add(_archiveProcessor.Process(cbz));
            });
            results.CompleteAdding();
        });

        ProgressFactory.Create().Start(ctx =>
        {
            var task = ctx.AddTask("Processing .cbz files", maxValue: files.Count);
            const int descriptionWidth = 100;

            foreach (var result in results.GetConsumingEnumerable())
            {
                logger.WriteEntry(result);

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

                task.Description = Markup.Escape(ProgressDescriptionFormatter.Format(label, result.FilePath, descriptionWidth));
                task.Increment(1);
            }
        });

        producer.Wait();
        return (updatedCount, wouldUpdateCount);
    }
}
