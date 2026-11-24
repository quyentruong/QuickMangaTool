namespace QuickMangaTool.Tests.Utilities;

public class PageRenamePlannerTests
{
    [Fact]
    public void BuildRenameMap_SortsNaturallyByNumber()
    {
        var entries = new[]
        {
            "10_translated.jpg",
            "2_translated.jpg",
            "1_translated.jpg"
        };

        var map = PageRenamePlanner.BuildRenameMap(entries);

        Assert.Equal("001.jpg", map["1_translated.jpg"]);
        Assert.Equal("002.jpg", map["2_translated.jpg"]);
        Assert.Equal("003.jpg", map["10_translated.jpg"]);
    }

    [Fact]
    public void BuildRenameMap_MixedPrefixesAndNumbers_OrdersNaturally()
    {
        var entries = new[]
        {
            "page_10.png",
            "02a.jpg",
            "page_2.png",
            "01.jpg",
            "page_1.png",
            "02.jpg"
        };

        var map = PageRenamePlanner.BuildRenameMap(entries);

        Assert.Equal("001.jpg", map["01.jpg"]);
        Assert.Equal("002.jpg", map["02.jpg"]);
        Assert.Equal("003.jpg", map["02a.jpg"]);
        Assert.Equal("004.png", map["page_1.png"]);
        Assert.Equal("005.png", map["page_2.png"]);
        Assert.Equal("006.png", map["page_10.png"]);
    }

    [Fact]
    public void BuildRenameMap_UsesExtensionOverrides_WhenProvided()
    {
        var entries = new[]
        {
            "1_translated.jpg",
            "2_translated.png"
        };
        var overrides = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["1_translated.jpg"] = ".webp"
        };

        var map = PageRenamePlanner.BuildRenameMap(entries, overrides);

        Assert.Equal("001.webp", map["1_translated.jpg"]);
        Assert.Equal("002.png", map["2_translated.png"]);
    }

    [Fact]
    public void BuildRenameMap_Subfolders_PreservesDirectoryPrefixAndNumbersPerDirectory()
    {
        var entries = new[]
        {
            "ch1/10_translated.jpg",
            "ch1/2_translated.jpg",
            "ch1/1_translated.jpg",
            "ch2/2_translated.png",
            "ch2/1_translated.png"
        };

        var map = PageRenamePlanner.BuildRenameMap(entries);

        Assert.Equal("ch1/001.jpg", map["ch1/1_translated.jpg"]);
        Assert.Equal("ch1/002.jpg", map["ch1/2_translated.jpg"]);
        Assert.Equal("ch1/003.jpg", map["ch1/10_translated.jpg"]);
        Assert.Equal("ch2/001.png", map["ch2/1_translated.png"]);
        Assert.Equal("ch2/002.png", map["ch2/2_translated.png"]);
    }

    [Fact]
    public void BuildRenameMap_Over1000Entries_PadsToFourDigits()
    {
        var entries = Enumerable.Range(1, 1000)
            .Reverse()
            .Select(n => $"{n}_page.JPG")
            .ToArray();

        var map = PageRenamePlanner.BuildRenameMap(entries);

        Assert.Equal("0001.jpg", map["1_page.JPG"]);
        Assert.Equal("0010.jpg", map["10_page.JPG"]);
        Assert.Equal("0999.jpg", map["999_page.JPG"]);
        Assert.Equal("1000.jpg", map["1000_page.JPG"]);
    }
}
