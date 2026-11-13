/// <summary>
/// Holds quality metrics extracted from a single decoded image.
/// All scores are computed from pixel data using NetVips.
/// </summary>
/// <param name="Width">Image width in pixels.</param>
/// <param name="Height">Image height in pixels.</param>
/// <param name="DpiX">Horizontal resolution in dots per inch; 0 if not embedded in metadata.</param>
/// <param name="DpiY">Vertical resolution in dots per inch; 0 if not embedded in metadata.</param>
/// <param name="BlurScore">
/// Edge-normalized variance of the Laplacian operator applied to the grayscale image.
/// Higher values indicate sharper images; lower values indicate blur.
/// </param>
/// <param name="NoiseScore">
/// Estimated noise standard deviation using 2-D Immerkaer finite-difference analysis.
/// Higher values indicate more noise or compression artifacts.
/// </param>
/// <param name="EdgeDensity">
/// Fraction of sampled pixels whose absolute Laplacian exceeds a significance threshold.
/// Content-rich manga pages score 0.10 or higher; sparse pages such as chapter dividers
/// or title cards score near 0 because most pixels are flat background.
/// </param>
/// <param name="SharpEdgeRatio">
/// Fraction of significant edge pixels that also register as strong, crisp edges.
/// Helps distinguish genuinely crisp line art from soft or interpolated (fake-upscaled) edges.
/// </param>
record struct ImageQualityMetrics(
    int Width,
    int Height,
    double DpiX,
    double DpiY,
    double BlurScore,
    double NoiseScore,
    double EdgeDensity,
    double SharpEdgeRatio = 1.0);
