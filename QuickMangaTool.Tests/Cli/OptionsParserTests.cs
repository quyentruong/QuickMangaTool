using System.Text;

namespace QuickMangaTool.Tests.Cli;

/// <summary>
/// Unit tests for <see cref="OptionsParser"/>.
/// Verifies argument parsing, flag combinations, subcommands, and help-output behaviour.
/// </summary>
public class OptionsParserTests
{
    private static AppOptions? Parse(params string[] args)
        => OptionsParser.Parse(args, new StringWriter());

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
        var (options, _) = ParseWithOutput(@"C:\manga");

        Assert.Null(options);
    }

    // ── Dry-run / confirm / interactive / delete-source ────────────────────

    [Fact]
    public void DryRun_DefaultsToFalse()
    {
        var options = Parse("-w");

        Assert.NotNull(options);
        Assert.False(options.DryRun);
    }

    [Fact]
    public void DryRunFlag_Long_EnablesDryRun()
    {
        var options = Parse("--dry-run", "-w");

        Assert.NotNull(options);
        Assert.True(options.DryRun);
    }

    [Fact]
    public void DryRunFlag_Short_EnablesDryRun()
    {
        var options = Parse("-n", "-w");

        Assert.NotNull(options);
        Assert.True(options.DryRun);
    }

    [Fact]
    public void ConfirmFlag_Long_SetsConfirm()
    {
        var options = Parse("--confirm", "-w");

        Assert.NotNull(options);
        Assert.True(options.Confirm);
    }

    [Fact]
    public void ConfirmFlag_Short_SetsConfirm()
    {
        var options = Parse("-y", "-w");

        Assert.NotNull(options);
        Assert.True(options.Confirm);
    }

    [Fact]
    public void DeleteSourceFlag_SetsDeleteSource()
    {
        var options = Parse("--delete-source", "cbr-to-cbz");

        Assert.NotNull(options);
        Assert.True(options.DeleteSource);
    }

    [Fact]
    public void InteractiveFlag_Long_SetsInteractive()
    {
        var options = Parse("--interactive");

        Assert.NotNull(options);
        Assert.True(options.Interactive);
    }

    [Fact]
    public void InteractiveFlag_Short_SetsInteractive()
    {
        var options = Parse("-i");

        Assert.NotNull(options);
        Assert.True(options.Interactive);
    }

    // ── Commands & Flags ──────────────────────────────────────────────────

    [Fact]
    public void ConvertFlag_Short_SetsConvertImages()
    {
        var options = Parse("-w");

        Assert.NotNull(options);
        Assert.True(options.ConvertImages);
        Assert.True(options.ConvertToWebp);
    }

    [Fact]
    public void ConvertFlag_Long_SetsConvertImages()
    {
        var options = Parse("--convert");

        Assert.NotNull(options);
        Assert.True(options.ConvertImages);
    }

    [Fact]
    public void ConvertSubcommand_SetsConvertImages()
    {
        var options = Parse("convert");

        Assert.NotNull(options);
        Assert.True(options.ConvertImages);
    }

    [Fact]
    public void CompressSubcommand_SetsCompressFolders()
    {
        var options = Parse("compress");

        Assert.NotNull(options);
        Assert.True(options.CompressFolders);
    }

    [Fact]
    public void CompressFlag_SetsCompressFolders()
    {
        var options = Parse("--compress-folders");

        Assert.NotNull(options);
        Assert.True(options.CompressFolders);
    }

    [Fact]
    public void PdfToCbzSubcommand_SetsPdfToCbz()
    {
        var options = Parse("pdf-to-cbz");

        Assert.NotNull(options);
        Assert.True(options.PdfToCbz);
    }

    [Fact]
    public void CbrToCbzSubcommand_SetsCbrToCbz()
    {
        var options = Parse("cbr-to-cbz");

        Assert.NotNull(options);
        Assert.True(options.CbrToCbz);
    }

    [Fact]
    public void DetectUpscaleFlag_Short_SetsDetectUpscale()
    {
        var options = Parse("-u");

        Assert.NotNull(options);
        Assert.True(options.DetectUpscale);
    }

    [Fact]
    public void UpscaleSubcommand_SetsDetectUpscale()
    {
        var options = Parse("upscale");

        Assert.NotNull(options);
        Assert.True(options.DetectUpscale);
    }

    [Fact]
    public void RemoveOldCbzSubcommand_SetsRemoveOldCbz()
    {
        var options = Parse("remove-old");

        Assert.NotNull(options);
        Assert.True(options.RemoveOldCbz);
    }

    [Fact]
    public void ReadLogsSubcommand_SetsReadLogs()
    {
        var options = Parse("read-logs");

        Assert.NotNull(options);
        Assert.True(options.ReadLogs);
    }

    [Fact]
    public void DeleteLogsSubcommand_SetsDeleteLogs()
    {
        var options = Parse("delete-logs");

        Assert.NotNull(options);
        Assert.True(options.DeleteLogs);
    }

    [Fact]
    public void VipsDiagnosticsSubcommand_SetsVipsDiagnostics()
    {
        var options = Parse("diag");

        Assert.NotNull(options);
        Assert.True(options.VipsDiagnostics);
    }

    [Fact]
    public void ClearCacheFlag_Short_SetsClearCache()
    {
        var options = Parse("-c");

        Assert.NotNull(options);
        Assert.True(options.ClearCache);
    }

    [Fact]
    public void UpdateSubcommand_SetsUpdateApp()
    {
        var options = Parse("update");

        Assert.NotNull(options);
        Assert.True(options.UpdateApp);
        Assert.True(OptionsParser.IsKnownSubcommand("update"));
    }

    [Fact]
    public void UpdateFlag_SetsUpdateApp()
    {
        var options = Parse("--update", "-y");

        Assert.NotNull(options);
        Assert.True(options.UpdateApp);
        Assert.True(options.Confirm);
    }

    [Fact]
    public void UninstallArgs_AreNotRecognizedFromCli()
    {
        Assert.False(OptionsParser.IsKnownSubcommand("uninstall"));

        var (subcommandOptions, subcommandOutput) = ParseWithOutput("uninstall");
        Assert.Null(subcommandOptions);
        Assert.Contains("Usage:", subcommandOutput);

        var (flagOptions, flagOutput) = ParseWithOutput("--uninstall");
        Assert.Null(flagOptions);
        Assert.Contains("Usage:", flagOutput);

        var (confirmedFlagOptions, _) = ParseWithOutput("--uninstall", "-y");
        Assert.Null(confirmedFlagOptions);
    }

    // ── ImageFormat & Effort ──────────────────────────────────────────────

    [Fact]
    public void ImageFormatFlag_Avif_SetsImageFormat()
    {
        var options = Parse("--image-format=avif", "-w");

        Assert.NotNull(options);
        Assert.Equal(ImageFormat.Avif, options.ImageFormat);
    }

    [Fact]
    public void ImageFormatFlag_Jxl_SetsImageFormat()
    {
        var options = Parse("--image-format=jxl", "-w");

        Assert.NotNull(options);
        Assert.Equal(ImageFormat.Jxl, options.ImageFormat);
    }

    [Fact]
    public void EffortFlag_ParsesValue()
    {
        var options = Parse("--effort=7", "-w");

        Assert.NotNull(options);
        Assert.Equal(7, options.Effort);
    }

    [Fact]
    public void EffortFlag_InvalidValue_IsIgnored()
    {
        var options = Parse("--effort=abc", "-w");

        Assert.NotNull(options);
        Assert.Null(options.Effort);
    }

    // ── Parallelism ───────────────────────────────────────────────────────

    [Fact]
    public void ParallelFlag_ParsesValue()
    {
        var options = Parse("--parallel=4", "-w");

        Assert.NotNull(options);
        Assert.Equal(4, options.Parallelism);
    }

    [Fact]
    public void ParallelFlag_InvalidValue_DefaultsToZero()
    {
        var options = Parse("--parallel=abc", "-w");

        Assert.NotNull(options);
        Assert.Equal(0, options.Parallelism);
    }

    // ── Directory handling ────────────────────────────────────────────────

    [Fact]
    public void ExplicitDirectory_IsUsedWithSubcommand()
    {
        var options = Parse(@"C:\manga", "convert");

        Assert.NotNull(options);
        Assert.Equal(@"C:\manga", options.Directory);
    }

    [Fact]
    public void NoDirectory_DefaultsToCurrentDirectory()
    {
        var options = Parse("convert");

        Assert.NotNull(options);
        Assert.Equal(Directory.GetCurrentDirectory(), options.Directory);
    }
}
