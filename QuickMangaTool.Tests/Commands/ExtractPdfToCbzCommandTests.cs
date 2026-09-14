namespace QuickMangaTool.Tests.Commands;

public class ExtractPdfToCbzCommandTests : IDisposable
{
    private readonly string _tempDir;

    public ExtractPdfToCbzCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public void Execute_NoPdfs_PrintsNoPdfMessage()
    {
        var sut = new ExtractPdfToCbzCommand(_tempDir);

        var sw = new StringWriter();
        var originalOut = Console.Out;
        try
        {
            Console.SetOut(sw);
            var ret = sut.Execute(Array.Empty<string>(), new NoopRunLogger());
            Assert.Equal(0, ret);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var output = sw.ToString();
        Assert.Contains("No PDF files found.", output);
    }

    [Fact]
    public void Execute_ExistingCbz_SkipsAndReportsZeroCreated()
    {
        var pdfPath = Path.Combine(_tempDir, "sample.pdf");
        File.WriteAllText(pdfPath, "%PDF-1.4\n%\u00e2\u00e3\u00cf\u00d3\n");
        var cbzPath = Path.Combine(_tempDir, "sample.cbz");
        File.WriteAllText(cbzPath, "");

        var sut = new ExtractPdfToCbzCommand(_tempDir);

        var sw = new StringWriter();
        var originalOut = Console.Out;
        try
        {
            Console.SetOut(sw);
            var ret = sut.Execute(Array.Empty<string>(), new NoopRunLogger());
            Assert.Equal(0, ret);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var output = sw.ToString();
        Assert.Contains("Created 0 .cbz file(s).", output);
    }
}
