using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace QuickMangaTool.Tests.Utilities;

/// <summary>
/// Unit tests for <see cref="ImagePhash"/>.
/// </summary>
public class ImagePhashTests
{
    [Fact]
    public void ComputeHash_SameImageData_ReturnsSameHash()
    {
        using var image = new Image<Rgba32>(32, 32, Color.White);

        using var stream1 = new MemoryStream();
        using var stream2 = new MemoryStream();

        image.SaveAsPng(stream1);
        image.SaveAsPng(stream2);

        stream1.Position = 0;
        stream2.Position = 0;

        var hash1 = ImagePhash.ComputeHash(stream1);
        var hash2 = ImagePhash.ComputeHash(stream2);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeHash_DifferentImages_ReturnsDifferentHash()
    {
        using var whiteImage = new Image<Rgba32>(32, 32, Color.White);
        using var blackImage = new Image<Rgba32>(32, 32, Color.Black);

        using var whiteStream = new MemoryStream();
        using var blackStream = new MemoryStream();

        whiteImage.SaveAsPng(whiteStream);
        blackImage.SaveAsPng(blackStream);

        whiteStream.Position = 0;
        blackStream.Position = 0;

        var whiteHash = ImagePhash.ComputeHash(whiteStream);
        var blackHash = ImagePhash.ComputeHash(blackStream);

        Assert.NotEqual(whiteHash, blackHash);
    }
}
