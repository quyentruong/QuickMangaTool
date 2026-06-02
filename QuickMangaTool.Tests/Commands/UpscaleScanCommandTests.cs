using System.IO.Compression;

namespace QuickMangaTool.Tests.Commands;

[Collection("Spectre.Console")]
public class UpscaleScanCommandTests : IDisposable
{
    public UpscaleScanCommandTests()
    {
        // Ensure skip cache is clear before each test to avoid cross-test pollution.
        SkipCache.Clear();
    }

    public void Dispose()
    {
        SkipCache.Clear();
    }

    private static string CreateTempCbz(IEnumerable<string> entryNames)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.cbz");
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            foreach (var name in entryNames)
            {
                var entry = zip.CreateEntry(name);
                using var stream = entry.Open();
                stream.WriteByte(0xFF);
            }
        }
        return path;
    }

    private class TestRunLogger : IRunLogger
    {
        public readonly List<ProcessResult> Entries = new();
        public (int total, int updated, int wouldUpdate)? Summary;

        public void WriteEntry(ProcessResult result)
        {
            Entries.Add(result);
        }

        public void WriteSummary(int total, int updatedCount, int wouldUpdateCount)
        {
            Summary = (total, updatedCount, wouldUpdateCount);
        }
    }

    [Fact]
    public void Execute_AllClean_MarksNoChange_And_LogsNoChange()
    {
        var cbz = CreateTempCbz(new[] { "page001.jpg" });
        try
        {
            var detector = new Mock<IUpscaleDetector>();
            detector.Setup(d => d.Scan(It.IsAny<string>())).Returns(UpscaleScanResult.Clean);

            var logger = new TestRunLogger();

            var sut = new UpscaleScanCommand(detector.Object, parallelism: 1);
            var ret = sut.Execute(new[] { cbz }, logger);

            Assert.Equal(0, ret);
            Assert.Single(logger.Entries);
            Assert.Equal(ProcessStatus.NoChange, logger.Entries[0].Status);
            Assert.NotNull(logger.Summary);
            Assert.Equal(1, logger.Summary?.total);
            Assert.Equal(0, logger.Summary?.wouldUpdate);

            // The clean result should cause the path to be recorded in the skip cache.
            Assert.True(SkipCache.ShouldSkip(cbz));
        }
        finally { File.Delete(cbz); }
    }

    [Fact]
    public void Execute_NeedsUpscale_LogsWouldUpdate_And_DoesNotMarkNoChange()
    {
        var cbz = CreateTempCbz(new[] { "page001.jpg" });
        try
        {
            var issue = new UpscaleIssue("page001.jpg", 1, new[] { "dimensions (400×600 px, min width 800)" });
            var scanResult = new UpscaleScanResult(true, new[] { issue });

            var detector = new Mock<IUpscaleDetector>();
            detector.Setup(d => d.Scan(It.IsAny<string>())).Returns(scanResult);

            var logger = new TestRunLogger();

            var sut = new UpscaleScanCommand(detector.Object, parallelism: 1);
            var ret = sut.Execute(new[] { cbz }, logger);

            Assert.Equal(0, ret);
            Assert.Single(logger.Entries);
            var pr = logger.Entries[0];
            Assert.Equal(ProcessStatus.WouldUpdate, pr.Status);
            Assert.NotNull(pr.Detail);
            Assert.Contains("page001.jpg", pr.Detail);
            Assert.Contains("1 B", pr.Detail);

            // Because an upscale was needed, the skip cache must NOT mark the file as skipped.
            Assert.False(SkipCache.ShouldSkip(cbz));
            Assert.NotNull(logger.Summary);
            Assert.Equal(1, logger.Summary?.total);
            Assert.Equal(1, logger.Summary?.wouldUpdate);
        }
        finally { File.Delete(cbz); }
    }

    [Fact]
    public void Execute_ErrorDuringScan_LogsError()
    {
        var cbz = CreateTempCbz(new[] { "page001.jpg" });
        try
        {
            var detector = new Mock<IUpscaleDetector>();
            detector.Setup(d => d.Scan(It.IsAny<string>())).Throws(new InvalidOperationException("boom"));

            var logger = new TestRunLogger();

            var sut = new UpscaleScanCommand(detector.Object, parallelism: 1);
            var ret = sut.Execute(new[] { cbz }, logger);

            Assert.Equal(0, ret);
            Assert.Single(logger.Entries);
            Assert.Equal(ProcessStatus.Error, logger.Entries[0].Status);
            Assert.NotNull(logger.Entries[0].ErrorMessage);
            Assert.Contains("boom", logger.Entries[0].ErrorMessage);
            Assert.NotNull(logger.Summary);
            Assert.Equal(1, logger.Summary?.total);
            Assert.Equal(0, logger.Summary?.wouldUpdate);
        }
        finally { File.Delete(cbz); }
    }

    [Fact]
    public void Execute_SkippedByCache_DoesNotCallDetector()
    {
        var cbz = CreateTempCbz(new[] { "page001.jpg" });
        try
        {
            // Pre-mark the file as no-change in the cache so the command should skip scanning it.
            SkipCache.MarkNoChange(cbz);

            var detector = new Mock<IUpscaleDetector>();
            var logger = new TestRunLogger();

            var sut = new UpscaleScanCommand(detector.Object, parallelism: 1);
            var ret = sut.Execute(new[] { cbz }, logger);

            Assert.Equal(0, ret);
            // Detector.Scan must not be called when cache indicates skip.
            detector.Verify(d => d.Scan(It.IsAny<string>()), Times.Never);
            Assert.Single(logger.Entries);
            Assert.Equal(ProcessStatus.NoChange, logger.Entries[0].Status);
        }
        finally { File.Delete(cbz); }
    }
}
