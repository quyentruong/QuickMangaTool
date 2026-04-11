interface IBatchLogger : IDisposable
{
    void WriteHeader(DateTime timestamp, bool dryRun);
    void WriteEntry(ProcessResult result);
    void WriteSummary(int total, int updatedCount, int wouldUpdateCount);
}
