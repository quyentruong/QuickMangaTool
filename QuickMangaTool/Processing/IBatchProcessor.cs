interface IBatchProcessor
{
    (int updated, int wouldUpdate) Process(IReadOnlyList<string> files, IEntryLogger logger);
}
