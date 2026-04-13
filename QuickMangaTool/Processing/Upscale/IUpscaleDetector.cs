/// <summary>Scans a CBZ archive and returns a detailed result describing which images need upscaling and why.</summary>
interface IUpscaleDetector
{
    /// <summary>
    /// Scans every image in <paramref name="cbzPath"/> and returns an <see cref="UpscaleScanResult"/>
    /// containing whether upscaling is needed and a per-image breakdown of failing quality checks.
    /// </summary>
    UpscaleScanResult Scan(string cbzPath);
}
