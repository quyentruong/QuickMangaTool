using System.IO.Compression;
using Moq;

namespace QuickMangaTool.Tests.Processing.Upscale;

/// <summary>
/// Unit tests for <see cref="UpscaleDetector"/>.
/// A real CBZ archive is constructed in-memory for each test so the detector exercises
/// its full ZIP-traversal path. The <see cref="IImageQualityAnalyzer"/> dependency is
/// mocked to control which quality metrics are returned per image entry.
/// </summary>
public class UpscaleDetectorTests
{
    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>Creates an in-memory CBZ (ZIP) archive with one dummy entry per filename.</summary>
    private static string CreateTempCbz(IEnumerable<string> entryNames)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.cbz");
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            foreach (var name in entryNames)
            {
                var entry = zip.CreateEntry(name);
                using var stream = entry.Open();
                // Write a minimal valid byte so the entry is non-empty.
                stream.WriteByte(0xFF);
            }
        }

        return path;
    }

    /// <summary>Good-quality metrics that pass every quality check.</summary>
    private static ImageQualityMetrics GoodMetrics() =>
        new(Width: 1200, Height: 1800, DpiX: 0, DpiY: 0,
            BlurScore: 200.0, NoiseScore: 3.0, EdgeDensity: 0.15);

    /// <summary>Metrics that fail the minimum-width check.</summary>
    private static ImageQualityMetrics NarrowMetrics() =>
        new(Width: 400, Height: 600, DpiX: 0, DpiY: 0,
            BlurScore: 200.0, NoiseScore: 3.0, EdgeDensity: 0.15);

    /// <summary>Metrics that fail the blur check.</summary>
    private static ImageQualityMetrics BlurryMetrics() =>
        new(Width: 1200, Height: 1800, DpiX: 0, DpiY: 0,
            BlurScore: 10.0, NoiseScore: 3.0, EdgeDensity: 0.15);

    /// <summary>Metrics that fail the noise check.</summary>
    /// <remarks>
    /// BlurScore is intentionally kept below SharpnessNoiseIgnoreThreshold (100) so that
    /// the detector does not suppress the noise issue via the "noisy but sharp" bypass rule.
    /// </remarks>
    private static ImageQualityMetrics NoisyMetrics() =>
        new(Width: 1200, Height: 1800, DpiX: 0, DpiY: 0,
            BlurScore: 60.0, NoiseScore: 20.0, EdgeDensity: 0.15);

    /// <summary>Metrics for a sparse page (low edge density) that should be skipped.</summary>
    private static ImageQualityMetrics SparsePageMetrics() =>
        new(Width: 1200, Height: 1800, DpiX: 0, DpiY: 0,
            BlurScore: 10.0, NoiseScore: 3.0, EdgeDensity: 0.01);

    /// <summary>Metrics for an ad banner (very narrow) that should be skipped.</summary>
    private static ImageQualityMetrics AdBannerMetrics() =>
        new(Width: 150, Height: 50, DpiX: 0, DpiY: 0,
            BlurScore: 10.0, NoiseScore: 3.0, EdgeDensity: 0.15);

    // ── Clean archive ─────────────────────────────────────────────────────

    [Fact]
    public void Scan_AllImagesPassChecks_ReturnsClean()
    {
        var cbzPath = CreateTempCbz(["page001.jpg"]);
        try
        {
            var analyzer = new Mock<IImageQualityAnalyzer>();
            analyzer.Setup(a => a.Analyze(It.IsAny<Stream>())).Returns(GoodMetrics());

            var detector = new UpscaleDetector(analyzer.Object);
            var result = detector.Scan(cbzPath);

            Assert.False(result.NeedsUpscale);
            Assert.Empty(result.Issues);
        }
        finally { File.Delete(cbzPath); }
    }

    [Fact]
    public void Scan_ArchiveWithNoImages_ReturnsClean()
    {
        // Non-image entries (e.g. metadata XML) should be ignored.
        var cbzPath = CreateTempCbz(["ComicInfo.xml"]);
        try
        {
            var analyzer = new Mock<IImageQualityAnalyzer>();

            var detector = new UpscaleDetector(analyzer.Object);
            var result = detector.Scan(cbzPath);

            Assert.False(result.NeedsUpscale);
            Assert.Empty(result.Issues);
            // The analyzer must not be called for non-image entries.
            analyzer.Verify(a => a.Analyze(It.IsAny<Stream>()), Times.Never);
        }
        finally { File.Delete(cbzPath); }
    }

    // ── Upscale needed ────────────────────────────────────────────────────

    [Fact]
    public void Scan_BlurryImage_ReportsNeedsUpscale()
    {
        var cbzPath = CreateTempCbz(["page001.jpg"]);
        try
        {
            var analyzer = new Mock<IImageQualityAnalyzer>();
            analyzer.Setup(a => a.Analyze(It.IsAny<Stream>())).Returns(BlurryMetrics());

            var detector = new UpscaleDetector(analyzer.Object);
            var result = detector.Scan(cbzPath);

            Assert.True(result.NeedsUpscale);
            Assert.NotEmpty(result.Issues);
        }
        finally { File.Delete(cbzPath); }
    }

    [Fact]
    public void Scan_NoisyImage_ReportsNeedsUpscale()
    {
        var cbzPath = CreateTempCbz(["page001.png"]);
        try
        {
            var analyzer = new Mock<IImageQualityAnalyzer>();
            analyzer.Setup(a => a.Analyze(It.IsAny<Stream>())).Returns(NoisyMetrics());

            var detector = new UpscaleDetector(analyzer.Object);
            var result = detector.Scan(cbzPath);

            Assert.True(result.NeedsUpscale);
            Assert.NotEmpty(result.Issues);
        }
        finally { File.Delete(cbzPath); }
    }

    [Fact]
    public void Scan_NarrowImage_ReportsNeedsUpscale()
    {
        // An image below MinWidth (800 px) should be flagged.
        var cbzPath = CreateTempCbz(["page001.jpg"]);
        try
        {
            var analyzer = new Mock<IImageQualityAnalyzer>();
            analyzer.Setup(a => a.Analyze(It.IsAny<Stream>())).Returns(NarrowMetrics());

            var detector = new UpscaleDetector(analyzer.Object);
            var result = detector.Scan(cbzPath);

            Assert.True(result.NeedsUpscale);
            Assert.Contains("page001.jpg", result.Issues.Select(i => i.EntryName));
        }
        finally { File.Delete(cbzPath); }
    }

    // ── Skip rules ────────────────────────────────────────────────────────

    [Fact]
    public void Scan_SparsePageBelowEdgeDensity_IsSkippedAndReturnsClean()
    {
        // Chapter-divider pages have near-zero edge density and should not trigger upscale.
        var cbzPath = CreateTempCbz(["page001.jpg"]);
        try
        {
            var analyzer = new Mock<IImageQualityAnalyzer>();
            analyzer.Setup(a => a.Analyze(It.IsAny<Stream>())).Returns(SparsePageMetrics());

            var detector = new UpscaleDetector(analyzer.Object);
            var result = detector.Scan(cbzPath);

            Assert.False(result.NeedsUpscale);
        }
        finally { File.Delete(cbzPath); }
    }

    [Fact]
    public void Scan_AdBannerNarrowImage_IsSkippedAndReturnsClean()
    {
        // Images narrower than MinAdWidth (300 px) are assumed to be ad banners.
        var cbzPath = CreateTempCbz(["banner.jpg"]);
        try
        {
            var analyzer = new Mock<IImageQualityAnalyzer>();
            analyzer.Setup(a => a.Analyze(It.IsAny<Stream>())).Returns(AdBannerMetrics());

            var detector = new UpscaleDetector(analyzer.Object);
            var result = detector.Scan(cbzPath);

            Assert.False(result.NeedsUpscale);
        }
        finally { File.Delete(cbzPath); }
    }

    [Fact]
    public void Scan_HighNoiseButAlsoHighSharpness_IsSkippedAndReturnsClean()
    {
        // High noise + very high Laplacian variance = content-rich page with compression
        // artefacts; the detector should ignore it rather than flagging it.
        var metrics = new ImageQualityMetrics(
            Width: 1200, Height: 1800, DpiX: 0, DpiY: 0,
            BlurScore: 150.0,  // above SharpnessNoiseIgnoreThreshold (100)
            NoiseScore: 20.0,  // above NoiseThreshold (12.5)
            EdgeDensity: 0.15);

        var cbzPath = CreateTempCbz(["page001.jpg"]);
        try
        {
            var analyzer = new Mock<IImageQualityAnalyzer>();
            analyzer.Setup(a => a.Analyze(It.IsAny<Stream>())).Returns(metrics);

            var detector = new UpscaleDetector(analyzer.Object);
            var result = detector.Scan(cbzPath);

            Assert.False(result.NeedsUpscale);
        }
        finally { File.Delete(cbzPath); }
    }

    // ── Analyzer returns null ─────────────────────────────────────────────

    [Fact]
    public void Scan_AnalyzerReturnsNull_EntryIsIgnoredAndReturnsClean()
    {
        // When the analyzer cannot decode an image it returns null; the entry is skipped.
        var cbzPath = CreateTempCbz(["page001.jpg"]);
        try
        {
            var analyzer = new Mock<IImageQualityAnalyzer>();
            analyzer.Setup(a => a.Analyze(It.IsAny<Stream>())).Returns((ImageQualityMetrics?)null);

            var detector = new UpscaleDetector(analyzer.Object);
            var result = detector.Scan(cbzPath);

            Assert.False(result.NeedsUpscale);
        }
        finally { File.Delete(cbzPath); }
    }

    // ── Non-existent archive ──────────────────────────────────────────────

    [Fact]
    public void Scan_NonExistentCbz_ReturnsClean()
    {
        // If the archive cannot be opened, the detector should not throw and returns clean.
        var analyzer = new Mock<IImageQualityAnalyzer>();
        var detector = new UpscaleDetector(analyzer.Object);

        var result = detector.Scan(@"C:\does\not\exist.cbz");

        Assert.False(result.NeedsUpscale);
    }

    // ── Stratified / replacement sampling & outlier tolerance ─────────────

    [Fact]
    public void Scan_SkippedAdOrSparsePages_PullsReplacementCandidates()
    {
        var entries = Enumerable.Range(1, 14).Select(i => $"page{i:D3}.jpg").ToList();
        var cbzPath = CreateTempCbz(entries);
        try
        {
            int callIndex = 0;
            var analyzer = new Mock<IImageQualityAnalyzer>();
            analyzer.Setup(a => a.Analyze(It.IsAny<Stream>())).Returns(() =>
            {
                callIndex++;
                // First 4 candidates are skipped (2 ad banners, 2 sparse pages),
                // followed by 2 blurry pages that should still be detected.
                return callIndex switch
                {
                    1 or 2 => AdBannerMetrics(),
                    3 or 4 => SparsePageMetrics(),
                    5 or 6 => BlurryMetrics(),
                    _ => GoodMetrics()
                };
            });

            var detector = new UpscaleDetector(analyzer.Object);
            var result = detector.Scan(cbzPath);

            Assert.True(result.NeedsUpscale);
            Assert.Equal(2, result.Issues.Count);
            analyzer.Verify(a => a.Analyze(It.IsAny<Stream>()), Times.Exactly(14));
        }
        finally { File.Delete(cbzPath); }
    }

    [Fact]
    public void Scan_OutlierTolerance_SingleBadPageOutOfSixValidPages_ReturnsClean()
    {
        var entries = Enumerable.Range(1, 6).Select(i => $"page{i:D3}.jpg").ToList();
        var cbzPath = CreateTempCbz(entries);
        try
        {
            int callIndex = 0;
            var analyzer = new Mock<IImageQualityAnalyzer>();
            analyzer.Setup(a => a.Analyze(It.IsAny<Stream>())).Returns(() =>
            {
                callIndex++;
                return callIndex == 1 ? BlurryMetrics() : GoodMetrics();
            });

            var detector = new UpscaleDetector(analyzer.Object);
            var result = detector.Scan(cbzPath);

            Assert.False(result.NeedsUpscale);
            Assert.Empty(result.Issues);
        }
        finally { File.Delete(cbzPath); }
    }

    [Fact]
    public void Scan_OutlierTolerance_TwoBadPagesOutOfSixValidPages_ReportsNeedsUpscale()
    {
        var entries = Enumerable.Range(1, 6).Select(i => $"page{i:D3}.jpg").ToList();
        var cbzPath = CreateTempCbz(entries);
        try
        {
            int callIndex = 0;
            var analyzer = new Mock<IImageQualityAnalyzer>();
            analyzer.Setup(a => a.Analyze(It.IsAny<Stream>())).Returns(() =>
            {
                callIndex++;
                return callIndex <= 2 ? BlurryMetrics() : GoodMetrics();
            });

            var detector = new UpscaleDetector(analyzer.Object);
            var result = detector.Scan(cbzPath);

            Assert.True(result.NeedsUpscale);
            Assert.Equal(2, result.Issues.Count);
        }
        finally { File.Delete(cbzPath); }
    }

    [Fact]
    public void Scan_SoftUpscaledEdges_ReportsNeedsUpscale()
    {
        var softUpscaledMetrics = new ImageQualityMetrics(
            Width: 1200,
            Height: 1800,
            DpiX: 0,
            DpiY: 0,
            BlurScore: 120.0,
            NoiseScore: 3.0,
            EdgeDensity: 0.25,
            SharpEdgeRatio: 0.05);

        var cbzPath = CreateTempCbz(["page001.jpg"]);
        try
        {
            var analyzer = new Mock<IImageQualityAnalyzer>();
            analyzer.Setup(a => a.Analyze(It.IsAny<Stream>())).Returns(softUpscaledMetrics);

            var detector = new UpscaleDetector(analyzer.Object);
            var result = detector.Scan(cbzPath);

            Assert.True(result.NeedsUpscale);
            Assert.Single(result.Issues);
            Assert.Contains("soft/upscaled edges", result.Issues[0].Reasons[0]);
        }
        finally { File.Delete(cbzPath); }
    }
}
