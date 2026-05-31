static class WorkerCount
{
    /// <summary>
    /// Resolves the effective worker count for I/O-bound work (e.g. XML transforms).
    /// Defaults to 2 (HDD-friendly) when <paramref name="parallelism"/> is 0.
    /// </summary>
    public static int Resolve(int parallelism)
    {
        var maxDefault = Math.Max(1, Math.Min(8, Environment.ProcessorCount * 2));
        int workers = parallelism > 0 ? parallelism : 2;
        return Math.Max(1, Math.Min(maxDefault, workers));
    }

    /// <summary>
    /// Resolves the effective worker count for CPU-bound work (e.g. image encoding).
    /// Defaults to <see cref="Environment.ProcessorCount"/> when <paramref name="parallelism"/> is 0.
    /// </summary>
    public static int ResolveCpuBound(int parallelism)
    {
        int workers = parallelism > 0 ? parallelism : Environment.ProcessorCount;
        return Math.Max(1, workers);
    }
}
