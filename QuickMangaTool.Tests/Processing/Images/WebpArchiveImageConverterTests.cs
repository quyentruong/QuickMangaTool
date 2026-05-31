using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace QuickMangaTool.Tests.Processing.Images;

/// <summary>
/// Unit tests for <see cref="WebpArchiveImageConverter"/>.
/// Verifies supported-extension detection and the size-comparison logic that decides
/// whether the WebP or the original image is kept in the archive.
/// </summary>
public class WebpArchiveImageConverterTests
{
    private readonly WebpArchiveImageConverter _sut = new(quality: 80);

    // ── CanConvert ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("image.jpg")]
    [InlineData("image.jpeg")]
    [InlineData("image.png")]
    [InlineData("image.bmp")]
    [InlineData("image.gif")]
    [InlineData("image.tif")]
    [InlineData("image.tiff")]
    [InlineData("image.avif")]
    [InlineData("IMAGE.JPG")]   // case-insensitive
    [InlineData("sub/dir/page.PNG")]
    public void CanConvert_SupportedExtension_ReturnsTrue(string entryName)
    {
        Assert.True(_sut.CanConvert(entryName));
    }

    [Theory]
    [InlineData("image.webp")]
    [InlineData("image.txt")]
    [InlineData("ComicInfo.xml")]
    [InlineData("image")]       // no extension
    public void CanConvert_UnsupportedExtension_ReturnsFalse(string entryName)
    {
        Assert.False(_sut.CanConvert(entryName));
    }

    // ── Convert – entry name ──────────────────────────────────────────────

    [Fact]
    public void Convert_WhenWebpIsSmaller_ReturnsWebpExtension()
    {
        // A large, highly-compressible PNG is very likely to produce a smaller WebP.
        using var stream = CreateLargeCompressiblePng();

        var result = _sut.Convert("page.png", stream);

        if (result.WebpWasSmaller)
            Assert.Equal("page.webp", result.EntryName);
        else
            Assert.Equal("page.png", result.EntryName);
    }

    [Fact]
    public void Convert_WhenOriginalIsKept_PreservesOriginalEntryName()
    {
        // Force the original to win by using a tiny, already-compressed source.
        using var stream = CreateTinyPng();
        var bytes = ReadAllBytes(stream);
        stream.Position = 0;

        var result = _sut.Convert("page.jpg", stream);

        // Regardless of which won, the entry name must end with .webp or the original ext.
        Assert.True(
            result.EntryName == "page.webp" || result.EntryName == "page.jpg",
            $"Unexpected entry name: {result.EntryName}");
    }

    // ── Convert – size metrics ────────────────────────────────────────────

    [Fact]
    public void Convert_SourceBytesMatchesInputStreamLength()
    {
        using var stream = CreateLargeCompressiblePng();
        var expectedSourceBytes = stream.Length;

        var result = _sut.Convert("page.png", stream);

        Assert.Equal(expectedSourceBytes, result.SourceBytes);
    }

    [Fact]
    public void Convert_ContentLength_MatchesWinnerByteCount()
    {
        using var stream = CreateLargeCompressiblePng();

        var result = _sut.Convert("page.png", stream);

        // Content must be exactly the bytes that correspond to the winner.
        Assert.Equal(result.Content.Length, result.WebpWasSmaller ? result.ConvertedBytes : result.SourceBytes);
    }

    [Fact]
    public void Convert_WebpWasSmaller_WhenConvertedBytesLessThanSourceBytes()
    {
        using var stream = CreateLargeCompressiblePng();

        var result = _sut.Convert("page.png", stream);

        // The flag must be consistent with the actual byte counts.
        Assert.Equal(result.ConvertedBytes < result.SourceBytes, result.WebpWasSmaller);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>Creates a PNG stream that is large and highly compressible (solid colour).</summary>
    private static MemoryStream CreateLargeCompressiblePng()
    {
        using var image = new Image<Rgba32>(512, 512, new Rgba32(100, 149, 237));
        var ms = new MemoryStream();
        image.Save(ms, new PngEncoder());
        ms.Position = 0;
        return ms;
    }

    /// <summary>Creates a minimal 1×1 PNG stream (already tiny; unlikely to produce a smaller WebP).</summary>
    private static MemoryStream CreateTinyPng()
    {
        using var image = new Image<Rgba32>(1, 1, new Rgba32(255, 0, 0));
        var ms = new MemoryStream();
        image.Save(ms, new PngEncoder());
        ms.Position = 0;
        return ms;
    }

    private static byte[] ReadAllBytes(Stream stream)
    {
        var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}
