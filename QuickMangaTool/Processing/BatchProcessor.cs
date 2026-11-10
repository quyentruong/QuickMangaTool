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

        ProgressFactory.Create().Start(ctx =>
        {
            int completedCount = 0;
            var overallTask = ctx.AddTask(
                Markup.Escape($"Overall progress [0 / {files.Count} files]"),
                maxValue: files.Count);

            var workerTasks = Enumerable.Range(0, workers)
                .Select(i =>
                {
                    var t = ctx.AddTask($"Worker {i + 1}: Idle", maxValue: 1);
                    t.IsIndeterminate = true;
                    return t;
                })
                .ToArray();

            var slotPool = new ConcurrentQueue<int>(Enumerable.Range(0, workers));

            var producer = Task.Run(() =>
            {
                var po = new ParallelOptions { MaxDegreeOfParallelism = workers };
                Parallel.ForEach(files, po, cbz =>
                {
                    slotPool.TryDequeue(out int slot);
                    var fileName = Path.GetFileName(cbz);
                    workerTasks[slot].Description = Markup.Escape($"Worker {slot + 1}: {fileName}");
                    workerTasks[slot].IsIndeterminate = true;

                    try
                    {
                        var res = _archiveProcessor.Process(cbz, (current, total) =>
                        {
                            workerTasks[slot].IsIndeterminate = false;
                            workerTasks[slot].MaxValue = total;
                            workerTasks[slot].Value = current;
                            workerTasks[slot].Description = Markup.Escape($"Worker {slot + 1}: {fileName} [{current}/{total}]");
                        });
                        results.Add(res);
                    }
                    catch (Exception ex)
                    {
                        results.Add(new ProcessResult(cbz, ProcessStatus.Error, ex.Message));
                    }
                    finally
                    {
                        workerTasks[slot].Description = Markup.Escape($"Worker {slot + 1}: Idle");
                        workerTasks[slot].IsIndeterminate = true;
                        slotPool.Enqueue(slot);
                    }
                });

                results.CompleteAdding();
            });

            foreach (var result in results.GetConsumingEnumerable())
            {
                logger.WriteEntry(result);

                switch (result.Status)
                {
                    case ProcessStatus.Updated: updatedCount++; break;
                    case ProcessStatus.WouldUpdate: wouldUpdateCount++; break;
                }

                completedCount++;
                overallTask.Description = Markup.Escape($"Overall progress [{completedCount} / {files.Count} files]");
                overallTask.Increment(1);
            }

            producer.Wait();
        });

        return (updatedCount, wouldUpdateCount);
    }
}
