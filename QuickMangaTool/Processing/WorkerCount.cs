static class WorkerCount
{
    /// <summary>
    /// Resolves the effective worker count from a user-specified value.
    /// Defaults to 2 (HDD-friendly) when <paramref name="parallelism"/> is 0,
    /// capped at twice the processor count up to a maximum of 8.
    /// </summary>
    public static int Resolve(int parallelism)
    {
        var maxDefault = Math.Max(1, Math.Min(8, Environment.ProcessorCount * 2));
        int workers = parallelism > 0 ? parallelism : 2;
        return Math.Max(1, Math.Min(maxDefault, workers));
    }
}
