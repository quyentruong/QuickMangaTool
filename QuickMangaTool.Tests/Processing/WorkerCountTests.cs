namespace QuickMangaTool.Tests.Processing;

/// <summary>
/// Unit tests for <see cref="WorkerCount"/>.
/// Verifies that I/O-bound and CPU-bound worker resolution honour the documented defaults
/// and clamping rules.
/// </summary>
public class WorkerCountTests
{
    // ── Resolve (I/O-bound) ───────────────────────────────────────────────

    [Fact]
    public void Resolve_ZeroParallelism_ReturnsDefault2()
    {
        // When parallelism is not specified (0), the I/O default is 2 (HDD-friendly).
        var result = WorkerCount.Resolve(0);

        Assert.Equal(2, result);
    }

    [Fact]
    public void Resolve_PositiveParallelism_ReturnsRequestedValue()
    {
        var result = WorkerCount.Resolve(3);

        Assert.Equal(3, result);
    }

    [Fact]
    public void Resolve_NegativeParallelism_ClampsToOne()
    {
        // Negative values are treated like "unspecified" by the max-bound, but the
        // min-clamp ensures the result is always at least 1.
        var result = WorkerCount.Resolve(-5);

        Assert.True(result >= 1);
    }

    [Fact]
    public void Resolve_ExceedsMaxDefault_ClampsToMaxDefault()
    {
        // Ask for an unreasonably large count; result must not exceed 2 * ProcessorCount (max 8).
        var overLimit = 9999;
        var result = WorkerCount.Resolve(overLimit);
        var expected = Math.Max(1, Math.Min(8, Environment.ProcessorCount * 2));

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Resolve_One_ReturnsOne()
    {
        var result = WorkerCount.Resolve(1);

        Assert.Equal(1, result);
    }

    // ── ResolveCpuBound ───────────────────────────────────────────────────

    [Fact]
    public void ResolveCpuBound_ZeroParallelism_ReturnsProcessorCount()
    {
        // CPU-bound default is Environment.ProcessorCount.
        var result = WorkerCount.ResolveCpuBound(0);

        Assert.Equal(Environment.ProcessorCount, result);
    }

    [Fact]
    public void ResolveCpuBound_PositiveParallelism_ReturnsRequestedValue()
    {
        var result = WorkerCount.ResolveCpuBound(4);

        Assert.Equal(4, result);
    }

    [Fact]
    public void ResolveCpuBound_NegativeParallelism_DefaultsToProcessorCount()
    {
        // Negative parallelism is treated as "unspecified"; the implementation falls back
        // to Environment.ProcessorCount just like a zero value does.
        var result = WorkerCount.ResolveCpuBound(-1);

        Assert.Equal(Environment.ProcessorCount, result);
    }

    [Fact]
    public void ResolveCpuBound_One_ReturnsOne()
    {
        var result = WorkerCount.ResolveCpuBound(1);

        Assert.Equal(1, result);
    }
}
