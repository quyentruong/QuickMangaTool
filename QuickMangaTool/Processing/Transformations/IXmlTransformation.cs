using System.Xml.Linq;

interface IXmlTransformation
{
    /// <summary>Applies the transformation to <paramref name="doc"/> in place. Returns <see langword="true"/> if any change was made.</summary>
    bool Apply(XDocument doc);
}
