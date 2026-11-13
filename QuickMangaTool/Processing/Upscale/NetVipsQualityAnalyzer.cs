using NetVips;

/// <summary>
/// Analyzes image quality metrics (dimensions, DPI, blur, noise) using NetVips.
/// Images are decoded to 8-bit grayscale so that pixel analysis is format-agnostic and
/// works across PNG, JPEG, and WEBP without separate per-format logic.
/// </summary>
class NetVipsQualityAnalyzer : IImageQualityAnalyzer
{
    // Analyze every Nth pixel row and column to keep processing fast
    // on large manga pages without sacrificing detection accuracy.
    private const int SampleStride = 4;

    // A Laplacian response above this magnitude is treated as a genuine edge.
    // Value of 8 is well above typical flat-region noise (~1–3) but below
    // the response of even soft edges on a manga page (~20–200).
    private const int SignificantLapThreshold = 8;

    // Edges exceeding this magnitude represent crisp, un-interpolated line art.
    private const int StrongLapThreshold = 32;

    // Reference edge density for normalizing Laplacian variance so sparse pages
    // are not penalized by whitespace and dense pages do not mask blur.
    private const double ReferenceEdgeDensity = 0.15;

    private readonly int _minWidthForPixelAnalysis;

    public NetVipsQualityAnalyzer(int minWidthForPixelAnalysis = 3)
    {
        _minWidthForPixelAnalysis = Math.Max(3, minWidthForPixelAnalysis);
    }

    /// <inheritdoc/>
    public ImageQualityMetrics? Analyze(Stream stream)
    {
        try
        {
            var sourceData = StreamByteReader.Read(stream, out _);
            if (sourceData.Length == 0)
                return null;

            using var image = Image.NewFromBuffer(sourceData, kwargs: new VOption
            {
                {"access", Enums.Access.Sequential}
            });

            int width = image.Width;
            int height = image.Height;

            double dpiX = ToDpi(image.Xres);
            double dpiY = ToDpi(image.Yres);

            if (width < _minWidthForPixelAnalysis || width < 3 || height < 3)
                return new ImageQualityMetrics(width, height, dpiX, dpiY, 0, 0, 0, 0);

            using var bwImage = image.Bands == 1 && image.Interpretation == Enums.Interpretation.Bw
                ? null
                : image.Colourspace(Enums.Interpretation.Bw);
            var current = bwImage ?? image;

            using var singleBand = current.Bands > 1
                ? current.ExtractBand(0)
                : null;
            current = singleBand ?? current;

            using var ucharImage = current.Format != Enums.BandFormat.Uchar
                ? current.Cast(Enums.BandFormat.Uchar)
                : null;
            current = ucharImage ?? current;

            var pixels = current.WriteToMemory<byte>();

            (double blurScore, double noiseScore, double edgeDensity, double sharpEdgeRatio) =
                ComputePixelScores(pixels, width, height);

            return new ImageQualityMetrics(width, height, dpiX, dpiY, blurScore, noiseScore, edgeDensity, sharpEdgeRatio);
        }
        catch
        {
            // Unsupported or corrupt images are silently skipped by the caller.
            return null;
        }
    }

    /// <summary>
    /// Converts libvips pixels-per-millimetre resolution into dots-per-inch.
    /// Returns 0 when no resolution is available.
    /// </summary>
    private static double ToDpi(double pixelsPerMillimeter) =>
        pixelsPerMillimeter > 0 ? pixelsPerMillimeter * 25.4 : 0;

    /// <summary>
    /// Computes blur, noise, edge-density, and sharp-edge-ratio scores by sampling pixel values
    /// via the 4-neighbor Laplacian and 2-D Immerkaer operator.
    /// </summary>
    private static (double blurScore, double noiseScore, double edgeDensity, double sharpEdgeRatio) ComputePixelScores(
        byte[] pixels, int width, int height)
    {
        if (width < 3 || height < 3 || pixels.Length < width * height)
            return (0, 0, 0, 0);

        long lapSum = 0;
        long lapSumSq = 0;
        long noiseSum = 0;
        int lapCount = 0;
        int noiseCount = 0;
        int significantEdgeCount = 0;
        int strongEdgeCount = 0;

        for (int y = SampleStride; y < height - SampleStride; y += SampleStride)
        {
            ReadOnlySpan<byte> prevRow = pixels.AsSpan((y - 1) * width, width);
            ReadOnlySpan<byte> currRow = pixels.AsSpan(y * width, width);
            ReadOnlySpan<byte> nextRow = pixels.AsSpan((y + 1) * width, width);

            for (int x = SampleStride; x < width - SampleStride; x += SampleStride)
            {
                int center = currRow[x];

                // 4-neighbor Laplacian: L = Σ(neighbors) − 4·center
                int lap = prevRow[x] + nextRow[x] + currRow[x - 1] + currRow[x + 1] - (4 * center);
                int absLap = Math.Abs(lap);

                lapSum += lap;
                lapSumSq += (long)lap * lap;
                lapCount++;

                if (absLap > SignificantLapThreshold)
                {
                    significantEdgeCount++;
                    if (absLap > StrongLapThreshold)
                        strongEdgeCount++;
                }

                // 2-D Immerkaer (3x3) noise operator evaluated outside strong edges
                // so manga line art (vertical/horizontal lines) is not mistaken for noise.
                if (absLap <= StrongLapThreshold)
                {
                    int immerkaer =
                        (prevRow[x - 1] - (2 * prevRow[x]) + prevRow[x + 1])
                      - (2 * (currRow[x - 1] - (2 * center) + currRow[x + 1]))
                      + (nextRow[x - 1] - (2 * nextRow[x]) + nextRow[x + 1]);
                    noiseSum += Math.Abs(immerkaer);
                    noiseCount++;
                }
            }
        }

        if (lapCount == 0)
            return (0, 0, 0, 0);

        double edgeDensity = (double)significantEdgeCount / lapCount;
        double sharpEdgeRatio = significantEdgeCount > 0
            ? (double)strongEdgeCount / significantEdgeCount
            : 0;

        double mean = (double)lapSum / lapCount;
        double rawVariance = ((double)lapSumSq / lapCount) - (mean * mean);
        double blurScore = significantEdgeCount > 0
            ? rawVariance * (ReferenceEdgeDensity / edgeDensity)
            : rawVariance;

        // Immerkaer (1996) noise sigma estimate using 3x3 second-order finite differences.
        double noiseScore = noiseCount > 0
            ? Math.Sqrt(Math.PI / 2.0) * noiseSum / (6.0 * noiseCount)
            : 0;

        return (blurScore, noiseScore, edgeDensity, sharpEdgeRatio);
    }
}