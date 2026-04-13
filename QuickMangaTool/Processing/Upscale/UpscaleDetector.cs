using System.IO.Compression;

/// <summary>
/// Determines whether a CBZ archive contains images that need upscaling or quality improvement.
/// Detection uses SixLabors.ImageSharp to decode each image and evaluate:
/// <list type="bullet">
///   <item><description>Dimensions — width or height below minimum thresholds.</description></item>
///   <item><description>DPI — embedded resolution below <see cref="MinDpi"/> indicates a low-resolution source scan.</description></item>
///   <item><description>Blur — Laplacian variance below <see cref="BlurThreshold"/> indicates a soft or blurry image.</description></item>
///   <item><description>Noise — Immerkaer noise sigma above <see cref="NoiseThreshold"/> indicates compression artifacts or scan noise.</description></item>
/// </list>
/// </summary>
class UpscaleDetector : IUpscaleDetector
{
    // Minimum acceptable pixel width for manga pages. Height is not used.
    private const int MinWidth = 800;

    // Minimum DPI considered acceptable; 0 means no DPI metadata was embedded.
    // Many scans omit DPI metadata, so only flag when a value is present and below this.
    private const double MinDpi = 72.0;
    // Allow a small tolerance below MinDpi to avoid false positives from minor metadata
    // rounding or scanner reporting differences. For example with MinDpi=72 and
    // DpiTolerance=5 we will not flag any DPI >= 67.
    private const double DpiTolerance = 5.0;

    // Laplacian variance below this value indicates the image is blurry.
    // Typical sharp manga pages score well above 100; heavily blurred images fall below 50.
    private const double BlurThreshold = 50.0;

    // Immerkaer noise sigma above this value indicates significant noise or JPEG block artifacts.
    // Values in 0–5 are clean; above 10 suggests noticeable degradation.
    private const double NoiseThreshold = 12.5;

    // If noise is above NoiseThreshold but the image has very high Laplacian variance
    // (strong, numerous edges) it is likely a high-contrast page with compression
    // artifacts rather than a low-quality scan. When both conditions hold, skip the image.
    private const double SharpnessNoiseIgnoreThreshold = 100.0;

    // Images narrower than this are assumed to be advertisement banners and are excluded
    // from quality checks so they cannot trigger a false upscale detection.
    private const int MinAdWidth = 300;

    // Pages whose edge density falls below this fraction are sparse interstitial pages
    // (chapter dividers, title cards, "Later Part" separators) where the Laplacian variance
    // is naturally low due to absent content rather than actual blur. Skipping them prevents
    // false upscale detections without requiring any pixel-level content classification.
    private const double MinEdgeDensity = 0.05;

    private readonly IImageQualityAnalyzer _analyzer;

    /// <param name="analyzer">Analyzer used to decode images and extract quality metrics.</param>
    public UpscaleDetector(IImageQualityAnalyzer analyzer)
    {
        _analyzer = analyzer;
    }

    /// <inheritdoc/>
    public UpscaleScanResult Scan(string cbzPath)
    {
        try
        {
            using var zip = ZipFile.OpenRead(cbzPath);

            // Collect image entries upfront to apply the index-based skip rule.
            var imageEntries = zip.Entries
                .Where(e => !string.IsNullOrEmpty(e.Name))
                .Where(e => Path.GetExtension(e.Name).ToLowerInvariant() is ".jpg" or ".jpeg" or ".png" or ".webp")
                .ToList();

            // Cover pages and title spreads (indices 0–9) often differ in size, style, or
            // compression from content pages. Skip them when the archive is large enough so
            // that detection is driven by representative interior pages.
            var entriesToCheck = imageEntries.Count > 10
                ? imageEntries.Skip(10)
                : imageEntries;

            foreach (var entry in entriesToCheck)
            {
                using var stream = entry.Open();
                try
                {
                    var metrics = _analyzer.Analyze(stream);
                    if (metrics is null)
                        continue;

                    // Narrow images are ads; skip them so they don't skew quality checks.
                    if (metrics.Value.Width < MinAdWidth)
                        continue;

                    // Sparse pages (chapter dividers, title cards) have few edges by design;
                    // their low Laplacian variance reflects absent content, not blur.
                    if (metrics.Value.EdgeDensity < MinEdgeDensity)
                        continue;

                    // If the image has substantial noise but also very high Laplacian variance
                    // (many strong edges), treat it as content-rich with compression artifacts
                    // and skip it so it does not trigger an upscale.
                    if (metrics.Value.NoiseScore > NoiseThreshold && metrics.Value.BlurScore > SharpnessNoiseIgnoreThreshold)
                        continue;

                    var reasons = GetFailingChecks(metrics.Value);
                    if (reasons.Count > 0)
                    {
                        // Pages in a CBZ almost always originate from the same scan source,
                        // so a single failing image is sufficient evidence. Exit immediately
                        // to avoid decoding every page unnecessarily.
                        var issue = new UpscaleIssue(entry.Name, reasons);
                        return new UpscaleScanResult(true, [issue]);
                    }
                }
                catch
                {
                    // Ignore corrupt or unreadable entries; continue scanning the archive.
                }
            }

            return UpscaleScanResult.Clean;
        }
        catch
        {
            // If the archive itself cannot be opened, conservatively report no upscale needed.
            return UpscaleScanResult.Clean;
        }
    }

    /// <summary>
    /// Returns a human-readable list of quality checks that <paramref name="m"/> failed,
    /// including the measured value and the threshold it violated.
    /// Returns an empty list when the image passes all checks.
    /// </summary>
    private static IReadOnlyList<string> GetFailingChecks(ImageQualityMetrics m)
    {
        var reasons = new List<string>();

        if (m.Width < MinWidth)
            reasons.Add($"dimensions ({m.Width}×{m.Height} px, min width {MinWidth})");

        // Only flag DPI when metadata is present; a value of 0 means no DPI was embedded.
        // Use a tolerance to avoid false positives from small metadata differences.
        double effectiveMinDpi = MinDpi - DpiTolerance;
        if (m.DpiX > 0 && m.DpiX < effectiveMinDpi)
            reasons.Add($"DPI-X too low ({m.DpiX:F0} dpi, min {MinDpi:F0}, tol {DpiTolerance:F0})");
        if (m.DpiY > 0 && m.DpiY < effectiveMinDpi)
            reasons.Add($"DPI-Y too low ({m.DpiY:F0} dpi, min {MinDpi:F0}, tol {DpiTolerance:F0})");

        if (m.BlurScore < BlurThreshold)
            reasons.Add($"blurry (Laplacian σ² = {m.BlurScore:F1}, threshold {BlurThreshold:F1})");

        if (m.NoiseScore > NoiseThreshold)
            reasons.Add($"noisy (Immerkaer σ = {m.NoiseScore:F1}, threshold {NoiseThreshold:F1})");

        return reasons;
    }
}

