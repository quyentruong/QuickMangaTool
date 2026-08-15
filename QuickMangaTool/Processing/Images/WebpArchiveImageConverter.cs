using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;

class WebpArchiveImageConverter : IArchiveImageConverter
{
    private static readonly HashSet<string> SupportedExtensions =
    [
        ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff"
    ];

    private readonly WebpEncoder _encoder;

    public WebpArchiveImageConverter(int quality = 80)
    {
        // FileFormat must be explicitly set to Lossy — ImageSharp defaults to Lossless,
        // which ignores the Quality setting and produces files larger than the source JPEG.
        _encoder = new WebpEncoder
        {
            FileFormat = WebpFileFormatType.Lossy,
            Quality = quality,
        };
    }

    public bool CanConvert(string entryName)
    {
        var extension = Path.GetExtension(entryName).ToLowerInvariant();
        return SupportedExtensions.Contains(extension);
    }

    public ConvertedImage Convert(string entryName, Stream input)
    {
        using var sourceBuffer = new MemoryStream();
        input.CopyTo(sourceBuffer);
        var sourceBytes = sourceBuffer.Length;
        sourceBuffer.Position = 0;

        using var image = Image.Load(sourceBuffer);
        using var webpOutput = new MemoryStream();
        image.Save(webpOutput, _encoder);

        var webpBytes = webpOutput.ToArray();
        var sourceData = sourceBuffer.ToArray();

        // Keep whichever format produces the smaller file.
        // This means the final CBZ may contain a mix of original and WebP entries.
        bool webpWasSmaller = webpBytes.Length < sourceBytes;

        return webpWasSmaller
            ? new ConvertedImage(Path.ChangeExtension(entryName, ".webp"), webpBytes, sourceBytes, webpBytes.Length, WebpWasSmaller: true)
            : new ConvertedImage(entryName, sourceData, sourceBytes, webpBytes.Length, WebpWasSmaller: false);
    }
}
