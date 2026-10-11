using NetVips;

class JxlArchiveImageConverter(int effort = 5) : IArchiveImageConverter
{
    private static readonly HashSet<string> SupportedExtensions =
    [
        ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff"
    ];
    
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
        var distance = isBlackAndWhite ? 3.0 : 2.5;

        using var img = Image.NewFromBuffer(sourceData, string.Empty);
        using var imageToEncode = isBlackAndWhite ? img.Colourspace(Enums.Interpretation.Bw) : null;
        var targetImg = imageToEncode ?? img;

        // Lưu qua tùy chọn .jxl trong NetVips
        var jxlOptions = $".jxl[distance={distance},effort={_effort}]";
        var jxlBytes = targetImg.WriteToBuffer(jxlOptions);

        var jxlWasSmaller = jxlBytes.Length < sourceBytes;

        return jxlWasSmaller
            ? new ConvertedImage(Path.ChangeExtension(entryName, ".jxl"), jxlBytes, sourceBytes, jxlBytes.Length, IsSmaller: true, IsBlackAndWhite: isBlackAndWhite)
            : new ConvertedImage(entryName, sourceData, sourceBytes, jxlBytes.Length, IsSmaller: false, IsBlackAndWhite: isBlackAndWhite);
    }

}