using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

public class ImageSharpQualityAnalyzerTests
{
    [Fact]
    public void Analyze_ReturnsNull_ForInvalidStream()
    {
        var analyzer = new ImageSharpQualityAnalyzer();
        using var ms = new MemoryStream(new byte[] { 1, 2, 3 });
        var metrics = analyzer.Analyze(ms);
        Assert.Null(metrics);
    }

    [Fact]
    public void Analyze_UniformImage_ReturnsZeroMetrics()
    {
        using var img = new Image<L8>(50, 50);
        img.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < img.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < img.Width; x++)
                    row[x] = new L8(128);
            }
        });
        img.Metadata.HorizontalResolution = 300;
        img.Metadata.VerticalResolution = 300;
        img.Metadata.ResolutionUnits = SixLabors.ImageSharp.Metadata.PixelResolutionUnit.PixelsPerInch;

        using var ms = new MemoryStream();
        img.SaveAsPng(ms);
        ms.Position = 0;

        var analyzer = new ImageSharpQualityAnalyzer();
        var metrics = analyzer.Analyze(ms);

        Assert.NotNull(metrics);
        Assert.InRange(metrics.Value.BlurScore, 0, 0.1);
        Assert.InRange(metrics.Value.NoiseScore, 0, 0.1);
        Assert.InRange(metrics.Value.EdgeDensity, 0, 0.001);
    }

    [Fact]
    public void Analyze_HalfBlackHalfWhite_ProducesEdge()
    {
        int w = 64, h = 64;
        using var img = new Image<L8>(w, h);
        img.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < h; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < w; x++)
                    row[x] = new L8((byte)(x < w / 2 ? 0 : 255));
            }
        });

        using var ms = new MemoryStream();
        img.SaveAsPng(ms);
        ms.Position = 0;

        var analyzer = new ImageSharpQualityAnalyzer();
        var metrics = analyzer.Analyze(ms);

        Assert.NotNull(metrics);
        Assert.True(metrics.Value.EdgeDensity > 0.01);
        Assert.True(metrics.Value.BlurScore > 10);
        Assert.InRange(metrics.Value.NoiseScore, 0, 20);
    }

    [Fact]
    public void Analyze_PixelResolutionUnit_ConvertsDpi()
    {
        using var img = new Image<L8>(10, 10);
        img.Metadata.HorizontalResolution = 10; // pixels per cm
        img.Metadata.VerticalResolution = 10;
        img.Metadata.ResolutionUnits = SixLabors.ImageSharp.Metadata.PixelResolutionUnit.PixelsPerCentimeter;

        using var ms = new MemoryStream();
        img.SaveAsPng(ms);
        ms.Position = 0;

        var analyzer = new ImageSharpQualityAnalyzer();
        var metrics = analyzer.Analyze(ms);

        Assert.NotNull(metrics);
        Assert.InRange(metrics.Value.DpiX, 25.3, 25.5);
        Assert.InRange(metrics.Value.DpiY, 25.3, 25.5);
    }
}
