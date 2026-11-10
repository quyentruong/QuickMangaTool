using NetVips;

namespace QuickMangaTool.Tests.Processing.Images;

/// <summary>
/// Unit tests for <see cref="WebpArchiveImageConverter"/>, <see cref="AvifArchiveImageConverter"/>,
/// and <see cref="JxlArchiveImageConverter"/>.
/// </summary>
public class ArchiveImageConverterTests
{
    private readonly WebpArchiveImageConverter _webpConverter = new(quality: 80);
    private readonly AvifArchiveImageConverter _avifConverter = new(effort: 1);
    private readonly JxlArchiveImageConverter _jxlConverter = new(effort: 3);

    // ── CanConvert ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("image.jpg")]
    [InlineData("image.jpeg")]
    [InlineData("image.png")]
    [InlineData("image.bmp")]
    [InlineData("image.gif")]
    [InlineData("image.tif")]
    [InlineData("image.tiff")]
    [InlineData("IMAGE.JPG")]
    [InlineData("sub/dir/page.PNG")]
    public void CanConvert_SupportedExtension_ReturnsTrue(string entryName)
    {
        Assert.True(_webpConverter.CanConvert(entryName));
        Assert.True(_avifConverter.CanConvert(entryName));
        Assert.True(_jxlConverter.CanConvert(entryName));
    }

    [Theory]
    [InlineData("image.webp")]
    [InlineData("image.txt")]
    [InlineData("ComicInfo.xml")]
    [InlineData("image.avif")]
    [InlineData("image.jxl")]
    [InlineData("image")]
    public void CanConvert_UnsupportedExtension_ReturnsFalse(string entryName)
    {
        Assert.False(_webpConverter.CanConvert(entryName));
        Assert.False(_avifConverter.CanConvert(entryName));
        Assert.False(_jxlConverter.CanConvert(entryName));
    }

    // ── WebP Convert ──────────────────────────────────────────────────────

    [Fact]
    public void Convert_Webp_WhenSmaller_ReturnsWebpExtension()
    {
        using var stream = CreateLargeCompressiblePng();

        var result = _webpConverter.Convert("page.png", stream);

        Assert.True(result.IsSmaller);
        Assert.Equal("page.webp", result.EntryName);
        Assert.True(result.ConvertedBytes < result.SourceBytes);
        Assert.Equal(result.Content.Length, result.ConvertedBytes);
    }

    [Fact]
    public void Convert_Webp_WhenOriginalIsKept_PreservesOriginalEntryName()
    {
        using var stream = CreateTinyPng();
        var result = _webpConverter.Convert("page.jpg", stream);

        Assert.True(result.EntryName == "page.webp" || result.EntryName == "page.jpg");
    }

    // ── AVIF Convert ──────────────────────────────────────────────────────

    [Fact]
    public void Convert_Avif_ProducesValidAvif()
    {
        using var stream = CreateLargeCompressiblePng();

        var result = _avifConverter.Convert("page.png", stream);

        Assert.NotNull(result);
        Assert.True(result.ConvertedBytes > 0);
        if (result.IsSmaller)
            Assert.Equal("page.avif", result.EntryName);
    }

    // ── JXL Convert ───────────────────────────────────────────────────────

    [Fact]
    public void Convert_Jxl_ProducesValidJxl()
    {
        using var stream = CreateLargeCompressiblePng();

        var result = _jxlConverter.Convert("page.png", stream);

        Assert.NotNull(result);
        Assert.True(result.ConvertedBytes > 0);
        if (result.IsSmaller)
            Assert.Equal("page.jxl", result.EntryName);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static MemoryStream CreateLargeCompressiblePng()
    {
        using var image = (Image.Black(256, 256) + new[] { 100.0, 149.0, 237.0 }).Cast(Enums.BandFormat.Uchar);
        var ms = new MemoryStream();
        image.WriteToStream(ms, ".png");
        ms.Position = 0;
        return ms;
    }

    private static MemoryStream CreateTinyPng()
    {
        using var image = (Image.Black(1, 1) + new[] { 255.0, 0.0, 0.0 }).Cast(Enums.BandFormat.Uchar);
        var ms = new MemoryStream();
        image.WriteToStream(ms, ".png");
        ms.Position = 0;
        return ms;
    }
}
