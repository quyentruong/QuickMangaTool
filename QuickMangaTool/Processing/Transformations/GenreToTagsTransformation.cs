using System.Xml.Linq;

class GenreToTagsTransformation : IXmlTransformation
{
    public bool Apply(XDocument doc)
    {
        var genreElements = doc.Descendants()
            .Where(e => e.Name.LocalName == "Genre")
            .ToList();

        if (genreElements.Count == 0)
            return false;

        foreach (var ge in genreElements)
        {
            var ns = ge.Name.Namespace;
            ge.ReplaceWith(new XElement(ns + "Tags", ge.Attributes(), ge.Nodes()));
        }

        return true;
    }
}
