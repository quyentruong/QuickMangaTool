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

    /// <inheritdoc/>
    public ImageQualityMetrics? Analyze(Stream stream)
    {
        try
        {
            var sourceData = StreamByteReader.Read(stream, out _);
            using var image = Image.NewFromBuffer(sourceData, kwargs: new VOption
            {
                {"access", Enums.Access.Sequential}
            });

            int width = image.Width;
            int height = image.Height;

            double dpiX = ToDpi(image.Xres);
            double dpiY = ToDpi(image.Yres);

            using var grayscale = image.Colourspace(Enums.Interpretation.Bw).Cast(Enums.BandFormat.Uchar);
            var pixels = grayscale.WriteToMemory<byte>();

            (double blurScore, double noiseScore, double edgeDensity) = ComputePixelScores(pixels, width, height);

            return new ImageQualityMetrics(width, height, dpiX, dpiY, blurScore, noiseScore, edgeDensity);
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
    /// Computes blur, noise, and edge-density scores by sampling pixel values via the 4-neighbor Laplacian.
    /// <list type="bullet">
    ///   <item><description>
    ///     <b>Blur score</b>: variance of the Laplacian — high variance means sharp edges exist;
    ///     low variance indicates the image is blurry.
    ///   </description></item>
    ///   <item><description>
    ///     <b>Noise score</b>: Immerkaer-style noise sigma from 1-D second-order finite differences —
    ///     high sigma indicates uniform high-frequency noise or compression artifacts.
    ///   </description></item>
    ///   <item><description>
    ///     <b>Edge density</b>: fraction of sampled pixels whose |Laplacian| exceeds
    ///     <see cref="SignificantLapThreshold"/> — low values identify sparse pages
    ///     (chapter dividers, title cards) that should be excluded from blur detection.
    ///   </description></item>
    /// </list>
    /// </summary>
    private static (double blurScore, double noiseScore, double edgeDensity) ComputePixelScores(byte[] pixels, int width, int height)
    {
        if (width < 3 || height < 3)
            return (0, 0, 0);

        double lapSum = 0;
        double lapSumSq = 0;
        double noiseSum = 0;
        int lapCount = 0;
        int noiseCount = 0;
        int significantEdgeCount = 0;

        // A Laplacian response above this magnitude is treated as a genuine edge.
        // Value of 8 is well above typical flat-region noise (~1–3) but below
        // the response of even soft edges on a manga page (~20–200).
        const double SignificantLapThreshold = 8.0;

        for (int y = SampleStride; y < height - SampleStride; y += SampleStride)
        {
            var rowOffset = y * width;
            var prevRowOffset = (y - 1) * width;
            var nextRowOffset = (y + 1) * width;

            for (int x = SampleStride; x < width - SampleStride; x += SampleStride)
            {
                byte center = pixels[rowOffset + x];

                // 4-neighbor Laplacian: L = Σ(neighbors) − 4·center
                double lap = pixels[prevRowOffset + x]
                           + pixels[nextRowOffset + x]
                           + pixels[rowOffset + x - 1]
                           + pixels[rowOffset + x + 1]
                           - 4.0 * center;
                lapSum += lap;
                lapSumSq += lap * lap;
                lapCount++;

                if (Math.Abs(lap) > SignificantLapThreshold)
                    significantEdgeCount++;

                // 1-D second-order finite difference: |I(x-1) - 2·I(x) + I(x+1)|
                // Approximates the contribution of noise at this sample point.
                double diff = Math.Abs(
                    pixels[rowOffset + x - 1] - 2.0 * center + pixels[rowOffset + x + 1]);
                noiseSum += diff;
                noiseCount++;
            }
        }

        if (lapCount == 0)
            return (0, 0, 0);

        double mean = lapSum / lapCount;
        double blurScore = (lapSumSq / lapCount) - (mean * mean); // variance of Laplacian

        // Immerkaer (1996) noise sigma estimate using 1-D finite differences.
        // sqrt(π/2) / (6·n) · Σ|Δ²| approximates the standard deviation of additive Gaussian noise.
        double noiseScore = noiseCount > 0
            ? Math.Sqrt(Math.PI / 2.0) * noiseSum / (6.0 * noiseCount)
            : 0;

        // Fraction of sampled pixels that responded as genuine edges.
        double edgeDensity = (double)significantEdgeCount / lapCount;

        return (blurScore, noiseScore, edgeDensity);
    }
}