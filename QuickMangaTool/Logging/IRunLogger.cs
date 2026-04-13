interface IRunLogger : IEntryLogger
{
    void WriteSummary(int total, int updatedCount, int wouldUpdateCount);
}
