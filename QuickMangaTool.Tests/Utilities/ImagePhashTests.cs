using NetVips;

namespace QuickMangaTool.Tests.Utilities;

/// <summary>
/// Unit tests for <see cref="ImagePhash"/>.
/// </summary>
public class ImagePhashTests
{
    [Fact]
    public void ComputeHash_SameImageData_ReturnsSameHash()
    {
        using var image = Image.Black(32, 32) + 255;

        using var stream1 = new MemoryStream();
        using var stream2 = new MemoryStream();

        image.WriteToStream(stream1, ".png");
        image.WriteToStream(stream2, ".png");

        stream1.Position = 0;
        stream2.Position = 0;

        var hash1 = ImagePhash.ComputeHash(stream1);
        var hash2 = ImagePhash.ComputeHash(stream2);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeHash_DifferentImages_ReturnsDifferentHash()
    {
        using var whiteImage = Image.Black(32, 32) + 255;
        using var blackImage = Image.Black(32, 32);

        using var whiteStream = new MemoryStream();
        using var blackStream = new MemoryStream();

        whiteImage.WriteToStream(whiteStream, ".png");
        blackImage.WriteToStream(blackStream, ".png");

        whiteStream.Position = 0;
        blackStream.Position = 0;

        var whiteHash = ImagePhash.ComputeHash(whiteStream);
        var blackHash = ImagePhash.ComputeHash(blackStream);

        Assert.NotEqual(whiteHash, blackHash);
    }
}
