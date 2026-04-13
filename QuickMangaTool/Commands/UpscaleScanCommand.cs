using Spectre.Console;
using System.Collections.Concurrent;
using System.Text;

class UpscaleScanCommand : IAppCommand
{
    private readonly IUpscaleDetector _detector;
    private readonly int _parallelism;

    public UpscaleScanCommand(IUpscaleDetector detector, int parallelism)
    {
        _detector = detector;
        _parallelism = parallelism;
    }

    public int Execute(IReadOnlyList<string> cbzFiles, IRunLogger logger)
    {
        Console.WriteLine($"Analyzing {cbzFiles.Count} .cbz files for upscale needs...");

        int workers = WorkerCount.Resolve(_parallelism);

        using var results = new BlockingCollection<(string Path, UpscaleScanResult Result, string? Error)>();

        var producer = Task.Run(() =>
        {
            var po = new ParallelOptions { MaxDegreeOfParallelism = workers };
            Parallel.ForEach(cbzFiles, po, cbz =>
            {
                try
                {
                    if (SkipCache.ShouldSkip(cbz))
                    {
                        results.Add((cbz, UpscaleScanResult.Clean, null));
                    }
                    else
                    {
                        var r = _detector.Scan(cbz);
                        results.Add((cbz, r, null));
                    }
                }
                catch (Exception ex)
                {
                    results.Add((cbz, UpscaleScanResult.Clean, ex.Message));
                }
            });
            results.CompleteAdding();
        });

        int needs = 0;

        ProgressFactory.Create().Start(ctx =>
        {
            var task = ctx.AddTask("Scanning .cbz files", maxValue: cbzFiles.Count);
            const int descriptionWidth = 100;

            foreach (var r in results.GetConsumingEnumerable())
            {
                bool needsUpscale = r.Result.NeedsUpscale;

                string label = r.Error is not null
                    ? "Error"
                    : needsUpscale
                        ? "Would update"
                        : "No change";

                task.Description = Markup.Escape(ProgressDescriptionFormatter.Format(label, r.Path, descriptionWidth));

                if (r.Error is not null)
                    AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(r.Path)} - {Markup.Escape(r.Error)}");

                string? detail = needsUpscale ? FormatIssues(r.Result.Issues) : null;

                ProcessResult pr = r.Error is not null
                    ? new ProcessResult(r.Path, ProcessStatus.Error, r.Error)
                    : needsUpscale
                        ? new ProcessResult(r.Path, ProcessStatus.WouldUpdate, Detail: detail)
                        : new ProcessResult(r.Path, ProcessStatus.NoChange);

                if (needsUpscale) needs++;

                logger.WriteEntry(pr);
                // If the scan determined there was no upscale needed, remember it so we can skip next time
                if (r.Error is null && !needsUpscale)
                    SkipCache.MarkNoChange(r.Path);
                task.Increment(1);
            }
        });

        producer.Wait();
        logger.WriteSummary(cbzFiles.Count, 0, needs);
        Console.WriteLine($"Found {needs} archives that likely need upscaling.");
        return 0;
    }

    /// <summary>
    /// Formats the per-image issue list into a single indented multi-line string
    /// suitable for embedding in a <see cref="ProcessResult.Detail"/> field.
    /// <example>
    /// <code>
    ///     page001.jpg → dimensions (850×1100 px, min 1000×1400), blurry (Laplacian σ² = 32.4, threshold 50.0)
    ///     page003.jpg → noisy (Immerkaer σ = 12.3, threshold 10.0)
    /// </code>
    /// </example>
    /// </summary>
    private static string FormatIssues(IReadOnlyList<UpscaleIssue> issues)
    {
        var sb = new StringBuilder();
        foreach (var issue in issues)
        {
            if (sb.Length > 0)
                sb.AppendLine();
            sb.Append("    ");
            sb.Append(issue.EntryName);
            sb.Append(" (");
            sb.Append(FormatSize(issue.EntrySize));
            sb.Append(")");
            sb.Append(" \u2192 ");
            sb.Append(string.Join(", ", issue.Reasons));
        }
        return sb.ToString();
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        double kb = bytes / 1024.0;
        if (kb < 1024)
            return $"{kb:F1} KB";
        double mb = kb / 1024.0;
        if (mb < 1024)
            return $"{mb:F2} MB";
        double gb = mb / 1024.0;
        return $"{gb:F2} GB";
    }
}
