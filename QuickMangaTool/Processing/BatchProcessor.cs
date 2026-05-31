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
        //const int descriptionWidth = 100;

        // Channel for completed results; the per-worker ProgressTasks handle in-progress display.
        using var results = new BlockingCollection<ProcessResult>();

        ProgressFactory.Create().Start(ctx =>
        {
            // One overall progress task that advances once per completed file.
            // Description shows a simple [done/total] counter so the label is always accurate.
            int completedCount = 0;
            var overallTask = ctx.AddTask(
                Markup.Escape($"[0 / {files.Count} files]"),
                maxValue: files.Count);

            // One ProgressTask per worker slot — each shows which file that slot is processing.
            // Marked IsIndeterminate so the progress bar animates instead of showing a stuck 0%.
            var workerTasks = Enumerable.Range(0, workers)
                .Select(i =>
                {
                    var t = ctx.AddTask($"Worker {i + 1}: Idle", maxValue: 1);
                    t.IsIndeterminate = true;
                    return t;
                })
                .ToArray();

            // Pool of available worker slot indices — dequeued when a file starts, re-enqueued when it finishes.
            var slotPool = new ConcurrentQueue<int>(Enumerable.Range(0, workers));

            var producer = Task.Run(() =>
            {
                var po = new ParallelOptions { MaxDegreeOfParallelism = workers };
                Parallel.ForEach(files, po, cbz =>
                {
                    // Claim a worker slot and show the file being converted on that slot's row.
                    slotPool.TryDequeue(out int slot);
                    workerTasks[slot].Description =
                        Markup.Escape($"Worker {slot + 1}: Converting: {Path.GetFileName(cbz)}");

                    try
                    {
                        results.Add(_archiveProcessor.Process(cbz));
                    }
                    catch (Exception ex)
                    {
                        results.Add(new ProcessResult(cbz, ProcessStatus.Error, ex.Message));
                    }
                    finally
                    {
                        // Reset the slot's label and return it to the pool for the next file.
                        workerTasks[slot].Description =
                            Markup.Escape($"Worker {slot + 1}: Idle");
                        slotPool.Enqueue(slot);
                    }
                });

                results.CompleteAdding();
            });

            // Consume completed results: log the outcome and advance the overall progress bar.
            foreach (var result in results.GetConsumingEnumerable())
            {
                logger.WriteEntry(result);

                switch (result.Status)
                {
                    case ProcessStatus.Updated: updatedCount++; break;
                    case ProcessStatus.WouldUpdate: wouldUpdateCount++; break;
                }

                completedCount++;
                overallTask.Description = Markup.Escape($"[{completedCount} / {files.Count} files]");
                overallTask.Increment(1);
            }

            producer.Wait();
        });

        return (updatedCount, wouldUpdateCount);
    }
}
