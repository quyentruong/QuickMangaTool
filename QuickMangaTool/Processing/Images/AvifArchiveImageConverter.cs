using ImageMagick;

class AvifArchiveImageConverter : IArchiveImageConverter
{
    private static readonly HashSet<string> SupportedExtensions =
    [
        ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff"
    ];

    private readonly int _quality;

    public AvifArchiveImageConverter(int quality = 65)
    {
        _quality = quality;
    }

    public bool CanConvert(string entryName)
    {
        var extension = Path.GetExtension(entryName).ToLowerInvariant();
        return SupportedExtensions.Contains(extension);
    }

    public ConvertedImage Convert(string entryName, Stream input)
    {
        // Read source into memory (ZipArchive operations keep reads sequential)
        using var sourceBuffer = new MemoryStream();
        input.CopyTo(sourceBuffer);
        var sourceBytes = sourceBuffer.Length;
        var sourceData = sourceBuffer.ToArray();

        // Use Magick.NET to encode to AVIF
        using var img = new MagickImage(sourceData);

        // Set quality (0-100)
        img.Quality = (uint)_quality;

        using var output = new MemoryStream();
        img.Write(output, MagickFormat.Avif);

        var avifBytes = output.ToArray();

        bool avifWasSmaller = avifBytes.Length < sourceBytes;

        return avifWasSmaller
            ? new ConvertedImage(Path.ChangeExtension(entryName, ".avif"), avifBytes, sourceBytes, avifBytes.Length, WebpWasSmaller: true)
            : new ConvertedImage(entryName, sourceData, sourceBytes, avifBytes.Length, WebpWasSmaller: false);
    }
}
