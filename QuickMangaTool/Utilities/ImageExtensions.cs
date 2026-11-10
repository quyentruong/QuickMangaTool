static class ImageExtensions
{
    private static readonly HashSet<string> ConvertibleExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff"
    };

    private static readonly HashSet<string> AnalysisExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".webp", ".avif", ".jxl"
    };

    public static bool CanConvert(string pathOrName)
    {
        var ext = Path.GetExtension(pathOrName);
        return !string.IsNullOrEmpty(ext) && ConvertibleExtensions.Contains(ext);
    }

    public static bool IsSupportedForAnalysis(string pathOrName)
    {
        var ext = Path.GetExtension(pathOrName);
        return !string.IsNullOrEmpty(ext) && AnalysisExtensions.Contains(ext);
    }
}
