using NetVips;
using Xunit;

public class NetVipsQualityAnalyzerTests
{
    [Fact]
    public void Analyze_ReturnsNull_ForInvalidStream()
    {
        var analyzer = new NetVipsQualityAnalyzer();
        using var ms = new MemoryStream(new byte[] { 1, 2, 3 });
        var metrics = analyzer.Analyze(ms);
        Assert.Null(metrics);
    }

    [Fact]
    public void Analyze_UniformImage_ReturnsZeroMetrics()
    {
        using var img = Image.Black(50, 50);
        using var withResolution = img.Copy(xres: 300.0 / 25.4, yres: 300.0 / 25.4);

        using var ms = new MemoryStream();
        withResolution.WriteToStream(ms, ".png");
        ms.Position = 0;

        var analyzer = new NetVipsQualityAnalyzer();
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
        var values = new double[h, w];
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                values[y, x] = x < w / 2 ? 0 : 255;
            }
        }

        using var img = Image.NewFromArray(values).Cast(Enums.BandFormat.Uchar);

        using var ms = new MemoryStream();
        img.WriteToStream(ms, ".png");
        ms.Position = 0;

        var analyzer = new NetVipsQualityAnalyzer();
        var metrics = analyzer.Analyze(ms);

        Assert.NotNull(metrics);
        Assert.True(metrics.Value.EdgeDensity > 0.01);
        Assert.True(metrics.Value.BlurScore > 10);
        Assert.InRange(metrics.Value.NoiseScore, 0, 20);
    }

    [Fact]
    public void Analyze_PixelsPerMillimeter_ConvertsDpi()
    {
        using var img = Image.Black(10, 10);
        using var copied = img.Copy(xres: 10.0 / 25.4, yres: 10.0 / 25.4);

        using var ms = new MemoryStream();
        copied.WriteToStream(ms, ".png");
        ms.Position = 0;

        var analyzer = new NetVipsQualityAnalyzer();
        var metrics = analyzer.Analyze(ms);

        Assert.NotNull(metrics);
        Assert.InRange(metrics.Value.DpiX, 9.9, 10.1);
        Assert.InRange(metrics.Value.DpiY, 9.9, 10.1);
    }
}
