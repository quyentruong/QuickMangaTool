/// <summary>
/// Analyzes quality metrics from a decoded image stream.
/// Implementations must handle PNG, JPEG, and WEBP formats via NetVips.
/// </summary>
interface IImageQualityAnalyzer
{
    /// <summary>
    /// Analyzes the image at the given stream and returns quality metrics,
    /// or <see langword="null"/> if the image cannot be decoded.
    /// </summary>
    /// <param name="stream">A readable stream positioned at the start of an image file.</param>
    /// <returns>Populated <see cref="ImageQualityMetrics"/>, or <see langword="null"/> on failure.</returns>
    ImageQualityMetrics? Analyze(Stream stream);
}
