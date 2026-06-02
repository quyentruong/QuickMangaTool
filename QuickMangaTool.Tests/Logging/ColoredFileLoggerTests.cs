using System.Text;

namespace QuickMangaTool.Tests.Logging;

public class ColoredFileLoggerTests : IDisposable
{
    private readonly string _tempDir;

    public ColoredFileLoggerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public void WriteHeader_WritesFeatureAndMode()
    {
        var path = Path.Combine(_tempDir, "log.log");
        using (var logger = new ColoredFileLogger(path))
        {
            logger.WriteHeader(new DateTime(2000,1,2,3,4,5), dryRun: true, feature: "Upscale");
        }

        var content = File.ReadAllText(path, Encoding.UTF8);
        Assert.Contains("QuickMangaTool", content);
        Assert.Contains("Feature: Upscale", content);
        Assert.Contains("Mode: Dry Run", content);
    }

    [Fact]
    public void WriteEntry_NoChange_ShowsNoChangeAndPath()
    {
        var path = Path.Combine(_tempDir, "entry.log");
        using (var logger = new ColoredFileLogger(path))
        {
            var r = new ProcessResult("/tmp/file.cbz", ProcessStatus.NoChange);
            logger.WriteEntry(r);
        }

        var content = File.ReadAllText(path, Encoding.UTF8);
        Assert.Contains("No change", content);
        Assert.Contains("/tmp/file.cbz", content);
    }

    [Fact]
    public void WriteEntry_WithSizes_ShowsSizeDelta()
    {
        var path = Path.Combine(_tempDir, "sizes.log");
        using (var logger = new ColoredFileLogger(path))
        {
            var r = new ProcessResult("/tmp/a.cbz", ProcessStatus.Updated, null, null, OriginalSizeBytes: 2048, NewSizeBytes: 1024);
            logger.WriteEntry(r);
        }

        var content = File.ReadAllText(path, Encoding.UTF8);
        Assert.Contains("Size: 2.0 KB -> 1.0 KB (-1.0 KB)", content);
    }

    [Fact]
    public void WriteEntry_Error_IncludesErrorMessage()
    {
        var path = Path.Combine(_tempDir, "error.log");
        using (var logger = new ColoredFileLogger(path))
        {
            var r = new ProcessResult("/tmp/b.cbz", ProcessStatus.Error, "boom");
            logger.WriteEntry(r);
        }

        var content = File.ReadAllText(path, Encoding.UTF8);
        Assert.Contains("Error", content);
        Assert.Contains("boom", content);
    }

    [Fact]
    public void WriteSummary_WouldUpdate_PrintsWouldUpdateCount()
    {
        var path = Path.Combine(_tempDir, "summary.log");
        using (var logger = new ColoredFileLogger(path))
        {
            logger.WriteSummary(total: 3, updatedCount: 0, wouldUpdateCount: 2);
        }

        var content = File.ReadAllText(path, Encoding.UTF8);
        Assert.Contains("Would update", content);
        Assert.Contains("2", content);
    }

    [Fact]
    public void WriteEntry_WithDetail_WritesEachLine()
    {
        var path = Path.Combine(_tempDir, "detail.log");
        using (var logger = new ColoredFileLogger(path))
        {
            var detail = "first line\nsecond line\r\nthird line";
            var r = new ProcessResult("/tmp/d.cbz", ProcessStatus.Updated, null, detail);
            logger.WriteEntry(r);
        }

        var content = File.ReadAllText(path, Encoding.UTF8);
        Assert.Contains("first line", content);
        Assert.Contains("second line", content);
        Assert.Contains("third line", content);
    }

    [Fact]
    public void WriteEntry_WouldUpdate_ShowsWouldUpdateAndPath()
    {
        var path = Path.Combine(_tempDir, "would.log");
        using (var logger = new ColoredFileLogger(path))
        {
            var r = new ProcessResult("/tmp/w.cbz", ProcessStatus.WouldUpdate);
            logger.WriteEntry(r);
        }

        var content = File.ReadAllText(path, Encoding.UTF8);
        Assert.Contains("Would update", content);
        Assert.Contains("/tmp/w.cbz", content);
    }

    [Fact]
    public void WriteEntry_WithPositiveDelta_ShowsPlusPrefix()
    {
        var path = Path.Combine(_tempDir, "pos.log");
        using (var logger = new ColoredFileLogger(path))
        {
            var r = new ProcessResult("/tmp/p.cbz", ProcessStatus.Updated, null, null, OriginalSizeBytes: 1024, NewSizeBytes: 2048);
            logger.WriteEntry(r);
        }

        var content = File.ReadAllText(path, Encoding.UTF8);
        Assert.Contains("Size: 1.0 KB -> 2.0 KB (+1.0 KB)", content);
    }

    [Fact]
    public void WriteHeader_ApplyChanges_WritesApplyChanges()
    {
        var path = Path.Combine(_tempDir, "head2.log");
        using (var logger = new ColoredFileLogger(path))
        {
            logger.WriteHeader(DateTime.UtcNow, dryRun: false, feature: "X");
        }

        var content = File.ReadAllText(path, Encoding.UTF8);
        Assert.Contains("Mode: Apply Changes", content);
    }

    [Fact]
    public void WriteSummary_Updated_PrintsUpdatedCount()
    {
        var path = Path.Combine(_tempDir, "summary2.log");
        using (var logger = new ColoredFileLogger(path))
        {
            logger.WriteSummary(total: 2, updatedCount: 1, wouldUpdateCount: 0);
        }

        var content = File.ReadAllText(path, Encoding.UTF8);
        Assert.Contains("Updated", content);
        Assert.Contains("1", content);
    }
}
