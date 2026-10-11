using NetVips;

class AvifArchiveImageConverter(int bwQuality = 62, int colorQuality = 70, int effort = 3) : IArchiveImageConverter
{
    private static readonly HashSet<string> SupportedExtensions =
    [
        ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff"
    ];

    private readonly int _bwQuality = bwQuality;
    private readonly int _colorQuality = colorQuality;
    private readonly int _effort = effort;

    public bool CanConvert(string entryName)
    {
        var extension = Path.GetExtension(entryName).ToLowerInvariant();
        return SupportedExtensions.Contains(extension);
    }

    public ConvertedImage Convert(string entryName, Stream input)
    {
        var sourceData = StreamByteReader.Read(input, out var sourceBytes);

        // 1. Phân loại màu cực nhanh bằng thumbnail nhỏ (128x128) để tránh quét hàng triệu pixel ảnh gốc
        var isBlackAndWhite = ImageColorClassifier.IsBlackAndWhite(sourceData);

        // 2. Nạp ảnh gốc để encode
        using var img = Image.NewFromBuffer(sourceData, string.Empty);
        using var imageToEncode = isBlackAndWhite ? img.Colourspace(Enums.Interpretation.Bw) : null;
        var targetImg = imageToEncode ?? img;

        // 3. Encode AVIF
        var avifSaveOptions = isBlackAndWhite
            ? $".avif[Q={_bwQuality},compression=av1,effort={_effort}]"
            : $".avif[Q={_colorQuality},compression=av1,effort={_effort},subsample_mode=off]";

        var avifBytes = targetImg.WriteToBuffer(avifSaveOptions);
        var avifWasSmaller = avifBytes.Length < sourceBytes;

        return avifWasSmaller
            ? new ConvertedImage(Path.ChangeExtension(entryName, ".avif"), avifBytes, sourceBytes, avifBytes.Length, IsSmaller: true, IsBlackAndWhite: isBlackAndWhite)
            : new ConvertedImage(entryName, sourceData, sourceBytes, avifBytes.Length, IsSmaller: false, IsBlackAndWhite: isBlackAndWhite);
    }

}