using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

class ArchiveProcessor : IArchiveProcessor
{
    private readonly bool _dryRun;
    private readonly IReadOnlyList<IXmlTransformation> _transformations;

    public ArchiveProcessor(bool dryRun, IReadOnlyList<IXmlTransformation> transformations)
    {
        _dryRun = dryRun;
        _transformations = transformations;
    }

    public ProcessResult Process(string cbzPath)
    {
        try
        {
            var openMode = _dryRun ? ZipArchiveMode.Read : ZipArchiveMode.Update;
            using var zip = ZipFile.Open(cbzPath, openMode);

            var comicEntries = zip.Entries
                .Where(e => Path.GetFileName(e.FullName).Equals("ComicInfo.xml", StringComparison.OrdinalIgnoreCase))
                .ToList();

            bool changed = false;
            foreach (var entry in comicEntries)
            {
                string xmlText;
                using (var reader = new StreamReader(entry.Open(), Encoding.UTF8, true))
                    xmlText = reader.ReadToEnd();

                var doc = XDocument.Parse(xmlText);

                bool transformApplied = false;
                foreach (var t in _transformations)
                    transformApplied |= t.Apply(doc);

                if (!transformApplied)
                    continue;

                changed = true;

                if (!_dryRun)
                {
                    var entryName = entry.FullName;
                    entry.Delete();
                    using var s = zip.CreateEntry(entryName, CompressionLevel.Optimal).Open();
                    doc.Save(s);
                }
            }

            if (!changed)
                return new ProcessResult(cbzPath, ProcessStatus.NoChange);

            return _dryRun
                ? new ProcessResult(cbzPath, ProcessStatus.WouldUpdate)
                : new ProcessResult(cbzPath, ProcessStatus.Updated);
        }
        catch (Exception ex)
        {
            return new ProcessResult(cbzPath, ProcessStatus.Error, ex.Message);
        }
    }
}
