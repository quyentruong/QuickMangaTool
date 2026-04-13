interface IBatchLogger : IRunLogger, IDisposable
{
    void WriteHeader(DateTime timestamp, bool dryRun, string feature);
}
