using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Analyzes image quality metrics (dimensions, DPI, blur, noise) using SixLabors.ImageSharp.
/// Images are decoded to 8-bit grayscale (L8) so that pixel analysis is format-agnostic and
/// works across PNG, JPEG, and WEBP without separate per-format logic.
/// </summary>
class ImageSharpQualityAnalyzer : IImageQualityAnalyzer
{
    // Analyze every Nth pixel row and column to keep processing fast
    // on large manga pages without sacrificing detection accuracy.
    private const int SampleStride = 4;

    /// <inheritdoc/>
    public ImageQualityMetrics? Analyze(Stream stream)
    {
        try
        {
            // Load as L8 (8-bit grayscale) — converts any color format automatically.
            // This avoids color-channel complexity and is sufficient for structural analysis.
            using var image = Image.Load<L8>(stream);

            int width = image.Width;
            int height = image.Height;

            double dpiX = ToDpi(image.Metadata.HorizontalResolution, image.Metadata.ResolutionUnits);
            double dpiY = ToDpi(image.Metadata.VerticalResolution, image.Metadata.ResolutionUnits);

            (double blurScore, double noiseScore, double edgeDensity) = ComputePixelScores(image, width, height);

            return new ImageQualityMetrics(width, height, dpiX, dpiY, blurScore, noiseScore, edgeDensity);
        }
        catch
        {
            // Unsupported or corrupt images are silently skipped by the caller.
            return null;
        }
    }

    /// <summary>
    /// Converts the ImageSharp resolution value to dots-per-inch regardless of the stored unit.
    /// Returns 0 when the metadata only encodes an aspect ratio (no physical DPI available).
    /// </summary>
    private static double ToDpi(double value, PixelResolutionUnit unit) =>
        unit switch
        {
            PixelResolutionUnit.PixelsPerInch        => value,
            PixelResolutionUnit.PixelsPerCentimeter  => value * 2.54,
            PixelResolutionUnit.PixelsPerMeter       => value / 39.3701,
            _                                        => 0   // AspectRatio or unknown
        };

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
    private static (double blurScore, double noiseScore, double edgeDensity) ComputePixelScores(Image<L8> image, int width, int height)
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

        // ProcessPixelRows provides safe, efficient span access to each row.
        image.ProcessPixelRows(accessor =>
        {
            for (int y = SampleStride; y < height - SampleStride; y += SampleStride)
            {
                var prevRow = accessor.GetRowSpan(y - 1);
                var curRow  = accessor.GetRowSpan(y);
                var nextRow = accessor.GetRowSpan(y + 1);

                for (int x = SampleStride; x < width - SampleStride; x += SampleStride)
                {
                    byte center = curRow[x].PackedValue;

                    // 4-neighbor Laplacian: L = Σ(neighbors) − 4·center
                    double lap = prevRow[x].PackedValue
                               + nextRow[x].PackedValue
                               + curRow[x - 1].PackedValue
                               + curRow[x + 1].PackedValue
                               - 4.0 * center;
                    lapSum   += lap;
                    lapSumSq += lap * lap;
                    lapCount++;

                    if (Math.Abs(lap) > SignificantLapThreshold)
                        significantEdgeCount++;

                    // 1-D second-order finite difference: |I(x-1) - 2·I(x) + I(x+1)|
                    // Approximates the contribution of noise at this sample point.
                    double diff = Math.Abs(
                        curRow[x - 1].PackedValue - 2.0 * center + curRow[x + 1].PackedValue);
                    noiseSum += diff;
                    noiseCount++;
                }
            }
        });

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
