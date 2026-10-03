using System.Text;

namespace QuickMangaTool.Tests.Cli;

/// <summary>
/// Unit tests for <see cref="OptionsParser"/>.
/// Verifies argument parsing, flag combinations, and help-output behaviour.
/// </summary>
public class OptionsParserTests
{
    // Helper: runs Parse and discards the help output.
    private static AppOptions? Parse(params string[] args)
        => OptionsParser.Parse(args, new StringWriter());

    // Helper: runs Parse and captures the text written to the output writer.
    private static (AppOptions? Options, string Output) ParseWithOutput(params string[] args)
    {
        var sb = new StringBuilder();
        using var writer = new StringWriter(sb);
        var options = OptionsParser.Parse(args, writer);
        return (options, sb.ToString());
    }

    // ── Help / no-op cases ────────────────────────────────────────────────

    [Fact]
    public void NoArgs_PrintsHelpAndReturnsNull()
    {
        var (options, output) = ParseWithOutput();

        Assert.Null(options);
        Assert.Contains("Usage:", output);
    }

    [Fact]
    public void HelpFlag_Long_PrintsHelpAndReturnsNull()
    {
        var (options, output) = ParseWithOutput("--help");

        Assert.Null(options);
        Assert.Contains("Usage:", output);
    }

    [Fact]
    public void HelpFlag_Short_PrintsHelpAndReturnsNull()
    {
        var (options, output) = ParseWithOutput("-h");

        Assert.Null(options);
        Assert.Contains("Usage:", output);
    }

    [Fact]
    public void DirectoryOnly_WithNoFeatureFlags_PrintsHelpAndReturnsNull()
    {
        // A bare directory path with no feature flags is a no-op; help is shown instead.
        var (options, _) = ParseWithOutput(@"C:\manga");

        Assert.Null(options);
    }

    // ── Dry-run / confirm ─────────────────────────────────────────────────

    [Fact]
    public void WithoutConfirm_DefaultsToDryRun()
    {
        var options = Parse("-g");

        Assert.NotNull(options);
        Assert.True(options.DryRun);
    }

    [Fact]
    public void WithConfirmFlag_Long_DisablesDryRun()
    {
        var options = Parse("--confirm", "-g");

        Assert.NotNull(options);
        Assert.False(options.DryRun);
    }

    [Fact]
    public void WithConfirmFlag_Short_DisablesDryRun()
    {
        var options = Parse("-y", "-g");

        Assert.NotNull(options);
        Assert.False(options.DryRun);
    }

    [Fact]
    public void DryRunFlag_Long_ForcesOnEvenWithConfirm()
    {
        // --dry-run wins even when --confirm is also present.
        var options = Parse("--confirm", "--dry-run", "-g");

        Assert.NotNull(options);
        Assert.True(options.DryRun);
    }

    // ── Feature flags ─────────────────────────────────────────────────────

    [Fact]
    public void GenreToTagsFlag_Long_SetsGenreToTags()
    {
        var options = Parse("--genre-to-tags");

        Assert.NotNull(options);
        Assert.True(options.GenreToTags);
    }

    [Fact]
    public void GenreToTagsFlag_Short_SetsGenreToTags()
    {
        var options = Parse("-g");

        Assert.NotNull(options);
        Assert.True(options.GenreToTags);
    }

    [Fact]
    public void DetectUpscaleFlag_Long_SetsDetectUpscale()
    {
        var options = Parse("--detect-upscale");

        Assert.NotNull(options);
        Assert.True(options.DetectUpscale);
    }

    [Fact]
    public void DetectUpscaleFlag_Short_SetsDetectUpscale()
    {
        var options = Parse("-u");

        Assert.NotNull(options);
        Assert.True(options.DetectUpscale);
    }

    [Fact]
    public void AddDuplicatesFlag_Long_SetsAddDuplicates()
    {
        var options = Parse("--add-duplicates");

        Assert.NotNull(options);
        Assert.True(options.AddDuplicates);
    }

    [Fact]
    public void DetectDuplicatesFlag_Long_SetsDetectDuplicates()
    {
        var options = Parse("--detect-duplicates");

        Assert.NotNull(options);
        Assert.True(options.DetectDuplicates);
    }

    [Fact]
    public void ConvertToWebpFlag_Long_SetsConvertToWebp()
    {
        var options = Parse("--convert-to-webp");

        Assert.NotNull(options);
        Assert.True(options.ConvertToWebp);
    }

    [Fact]
    public void ConvertToWebpFlag_Short_SetsConvertToWebp()
    {
        var options = Parse("-w");

        Assert.NotNull(options);
        Assert.True(options.ConvertToWebp);
    }

    [Fact]
    public void CbrToCbzFlag_Long_SetsCbrToCbz()
    {
        var options = Parse("--cbr-to-cbz");

        Assert.NotNull(options);
        Assert.True(options.CbrToCbz);
    }

    [Fact]
    public void RemoveOldCbzFlag_Long_SetsRemoveOldCbz()
    {
        var options = Parse("--remove-old-cbz");

        Assert.NotNull(options);
        Assert.True(options.RemoveOldCbz);
    }

    [Fact]
    public void ClearCacheFlag_Long_SetsClearCache()
    {
        var options = Parse("--clear-cache");

        Assert.NotNull(options);
        Assert.True(options.ClearCache);
    }

    [Fact]
    public void ClearCacheFlag_Short_SetsClearCache()
    {
        var options = Parse("-c");

        Assert.NotNull(options);
        Assert.True(options.ClearCache);
    }

    // ── Parallelism ───────────────────────────────────────────────────────

    [Fact]
    public void ParallelFlag_ParsesValue()
    {
        var options = Parse("--parallel=4", "-g");

        Assert.NotNull(options);
        Assert.Equal(4, options.Parallelism);
    }

    [Fact]
    public void ParallelFlag_InvalidValue_DefaultsToZero()
    {
        var options = Parse("--parallel=abc", "-g");

        Assert.NotNull(options);
        Assert.Equal(0, options.Parallelism);
    }

    [Fact]
    public void PhashThresholdFlag_ParsesValue()
    {
        var options = Parse("--detect-duplicates", "--phash-threshold=12");

        Assert.NotNull(options);
        Assert.Equal(12, options.PhashThreshold);
    }

    [Fact]
    public void PhashThresholdFlag_DefaultsToEight_WhenMissing()
    {
        var options = Parse("--detect-duplicates");

        Assert.NotNull(options);
        Assert.Equal(8, options.PhashThreshold);
    }

    // ── Directory ─────────────────────────────────────────────────────────

    [Fact]
    public void ExplicitDirectory_IsUsed()
    {
        var options = Parse(@"C:\manga", "-g");

        Assert.NotNull(options);
        Assert.Equal(@"C:\manga", options.Directory);
    }

    [Fact]
    public void NoDirectory_DefaultsToCurrentDirectory()
    {
        var options = Parse("-g");

        Assert.NotNull(options);
        Assert.Equal(Directory.GetCurrentDirectory(), options.Directory);
    }
}
