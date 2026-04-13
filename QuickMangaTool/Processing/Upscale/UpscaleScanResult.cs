/// <summary>
/// The result of scanning a single CBZ archive for upscale need.
/// Contains both the overall verdict and a per-image breakdown of which checks failed.
/// </summary>
/// <param name="NeedsUpscale"><see langword="true"/> if at least one image failed a quality check.</param>
/// <param name="Issues">
/// One entry per image that failed; empty when <paramref name="NeedsUpscale"/> is <see langword="false"/>.
/// </param>
record UpscaleScanResult(bool NeedsUpscale, IReadOnlyList<UpscaleIssue> Issues)
{
    /// <summary>Singleton result used when all images pass every quality check.</summary>
    public static readonly UpscaleScanResult Clean = new(false, []);
}
