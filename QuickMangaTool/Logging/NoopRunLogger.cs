class NoopRunLogger : IBatchLogger
{
    public void WriteHeader(DateTime timestamp, bool dryRun, string feature) { }
    public void WriteEntry(ProcessResult result) { }
    public void WriteSummary(int total, int updatedCount, int wouldUpdateCount) { }
    public void Dispose() { }
}
