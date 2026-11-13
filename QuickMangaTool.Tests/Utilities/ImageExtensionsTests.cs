namespace QuickMangaTool.Tests.Utilities;

public class ImageExtensionsTests
{
    [Theory]
    [InlineData("test.jpg", true)]
    [InlineData("test.jpeg", true)]
    [InlineData("test.png", true)]
    [InlineData("test.bmp", true)]
    [InlineData("test.gif", true)]
    [InlineData("test.tif", true)]
    [InlineData("test.tiff", true)]
    [InlineData("test.webp", false)]
    [InlineData("test.avif", false)]
    [InlineData("test.jxl", false)]
    [InlineData("test.txt", false)]
    [InlineData("test", false)]
    public void CanConvert_Tests(string filename, bool expected)
    {
        Assert.Equal(expected, ImageExtensions.CanConvert(filename));
    }

    [Theory]
    [InlineData("test.jpg", true)]
    [InlineData("test.jpeg", true)]
    [InlineData("test.png", true)]
    [InlineData("test.bmp", true)]
    [InlineData("test.gif", true)]
    [InlineData("test.tif", true)]
    [InlineData("test.tiff", true)]
    [InlineData("test.webp", true)]
    [InlineData("test.avif", true)]
    [InlineData("test.jxl", true)]
    [InlineData("test.txt", false)]
    [InlineData("test", false)]
    public void IsSupportedForAnalysis_Tests(string filename, bool expected)
    {
        Assert.Equal(expected, ImageExtensions.IsSupportedForAnalysis(filename));
    }
}
