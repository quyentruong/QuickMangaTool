using NetVips;

class WebpArchiveImageConverter(int quality = 78, int effort = 6) : IArchiveImageConverter
{
    private readonly int _quality = quality;
    private readonly int _effort = effort;

    public bool CanConvert(string entryName) => ImageExtensions.CanConvert(entryName);

    public ConvertedImage Convert(string entryName, Stream input)
    {
        var sourceData = StreamByteReader.Read(input, out var sourceBytes);
        var isBlackAndWhite = ImageColorClassifier.IsBlackAndWhite(sourceData);

        // 1. Dùng mặc định Access.Random để tránh triệt để lỗi "error in tile" với file JPEG
        using var img = Image.NewFromBuffer(sourceData, string.Empty);

        // 2. Tùy chọn nén WebP tối ưu cho Manga/Text
        // - smart_subsample=true: Giữ chữ thoại, line art sắc nét, không bị lem màu viền
        // - effort: Cân bằng tốc độ mã hóa
        // - strip=true: Xóa metadata EXIF/XMP thừa thãi
        var options = $".webp[Q={_quality},effort={_effort},smart_subsample=true,strip=true]";

        var webpBytes = img.WriteToBuffer(options);
        var webpWasSmaller = webpBytes.Length < sourceBytes;

        return webpWasSmaller
            ? new ConvertedImage(Path.ChangeExtension(entryName, ".webp"), webpBytes, sourceBytes, webpBytes.Length, IsSmaller: true, IsBlackAndWhite: isBlackAndWhite)
            : new ConvertedImage(entryName, sourceData, sourceBytes, webpBytes.Length, IsSmaller: false, IsBlackAndWhite: isBlackAndWhite);
    }
}
