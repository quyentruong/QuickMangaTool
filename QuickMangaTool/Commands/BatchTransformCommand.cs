class BatchTransformCommand : IAppCommand
{
    private readonly IBatchProcessor _processor;
    private readonly bool _dryRun;

    public BatchTransformCommand(IBatchProcessor processor, bool dryRun)
    {
        _processor = processor;
        _dryRun = dryRun;
    }

    public int Execute(IReadOnlyList<string> cbzFiles, IRunLogger logger)
    {
        var (updated, wouldUpdate) = _processor.Process(cbzFiles, logger);

        logger.WriteSummary(cbzFiles.Count, updated, wouldUpdate);

        if (_dryRun)
        {
            Console.WriteLine($"Processed {cbzFiles.Count} .cbz files. Would update: {wouldUpdate}.");
            Console.WriteLine("No changes were made. Re-run with --confirm or -y to apply changes.");
        }
        else
        {
            Console.WriteLine($"Processed {cbzFiles.Count} .cbz files. Updated: {updated}.");
        }
        return 0;
    }
}
