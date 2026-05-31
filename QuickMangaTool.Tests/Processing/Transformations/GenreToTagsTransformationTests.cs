using System.Xml.Linq;

namespace QuickMangaTool.Tests.Processing.Transformations;

/// <summary>
/// Unit tests for <see cref="GenreToTagsTransformation"/>.
/// Verifies that &lt;Genre&gt; elements are correctly renamed to &lt;Tags&gt; and that
/// documents without &lt;Genre&gt; elements are reported as unchanged.
/// </summary>
public class GenreToTagsTransformationTests
{
    private readonly GenreToTagsTransformation _sut = new();

    // ── Apply returns false (no change) ───────────────────────────────────

    [Fact]
    public void Apply_DocumentWithNoGenreElements_ReturnsFalse()
    {
        var doc = XDocument.Parse("<ComicInfo><Title>Test</Title></ComicInfo>");

        var changed = _sut.Apply(doc);

        Assert.False(changed);
    }

    [Fact]
    public void Apply_EmptyDocument_ReturnsFalse()
    {
        var doc = XDocument.Parse("<ComicInfo />");

        var changed = _sut.Apply(doc);

        Assert.False(changed);
    }

    // ── Apply returns true (changed) ──────────────────────────────────────

    [Fact]
    public void Apply_SingleGenreElement_ReturnsTrue()
    {
        var doc = XDocument.Parse("<ComicInfo><Genre>Action</Genre></ComicInfo>");

        var changed = _sut.Apply(doc);

        Assert.True(changed);
    }

    [Fact]
    public void Apply_SingleGenreElement_RenamesElementToTags()
    {
        var doc = XDocument.Parse("<ComicInfo><Genre>Action</Genre></ComicInfo>");

        _sut.Apply(doc);

        Assert.Null(doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Genre"));
        var tags = doc.Descendants().Single(e => e.Name.LocalName == "Tags");
        Assert.Equal("Action", tags.Value);
    }

    [Fact]
    public void Apply_MultipleGenreElements_RenamesAll()
    {
        var doc = XDocument.Parse(
            "<ComicInfo><Genre>Action</Genre><Genre>Comedy</Genre></ComicInfo>");

        _sut.Apply(doc);

        Assert.DoesNotContain(doc.Descendants(), e => e.Name.LocalName == "Genre");
        Assert.Equal(2, doc.Descendants().Count(e => e.Name.LocalName == "Tags"));
    }

    [Fact]
    public void Apply_PreservesGenreElementText()
    {
        var doc = XDocument.Parse("<ComicInfo><Genre>Sci-Fi</Genre></ComicInfo>");

        _sut.Apply(doc);

        var tags = doc.Descendants().Single(e => e.Name.LocalName == "Tags");
        Assert.Equal("Sci-Fi", tags.Value);
    }

    [Fact]
    public void Apply_PreservesGenreElementAttributes()
    {
        var doc = XDocument.Parse(
            "<ComicInfo><Genre lang=\"en\">Drama</Genre></ComicInfo>");

        _sut.Apply(doc);

        var tags = doc.Descendants().Single(e => e.Name.LocalName == "Tags");
        Assert.Equal("en", tags.Attribute("lang")?.Value);
    }

    [Fact]
    public void Apply_PreservesNamespace()
    {
        // If the Genre element lives in a namespace the renamed element must keep it.
        var ns = XNamespace.Get("http://example.com/ns");
        var doc = new XDocument(new XElement(ns + "ComicInfo",
            new XElement(ns + "Genre", "Horror")));

        _sut.Apply(doc);

        var tags = doc.Descendants().Single(e => e.Name.LocalName == "Tags");
        Assert.Equal(ns, tags.Name.Namespace);
    }

    [Fact]
    public void Apply_CanBeCalledTwice_SecondCallReturnsFalse()
    {
        // After Genre→Tags the document no longer contains Genre; a second call is a no-op.
        var doc = XDocument.Parse("<ComicInfo><Genre>Action</Genre></ComicInfo>");
        _sut.Apply(doc);

        var secondCall = _sut.Apply(doc);

        Assert.False(secondCall);
    }
}
