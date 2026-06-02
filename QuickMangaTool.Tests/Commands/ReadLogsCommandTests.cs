using System.Text;

namespace QuickMangaTool.Tests.Commands;

public class ReadLogsCommandTests : IDisposable
{
    private readonly string _tempDir;

    public ReadLogsCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public void Execute_SingleLog_PrintsContent()
    {
        var path = Path.Combine(_tempDir, "one.log");
        File.WriteAllText(path, "first\nsecond\n", Encoding.UTF8);

        var sut = new ReadLogsCommand(_tempDir);

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
        Assert.Contains("first", output);
        Assert.Contains("second", output);
        Assert.Contains("one.log", output);
    }

    [Fact]
    public void Execute_MultipleLogs_AllSelection_PrintsAll()
    {
        var a = Path.Combine(_tempDir, "a.log");
        var b = Path.Combine(_tempDir, "b.log");
        File.WriteAllText(a, "Aline\n", Encoding.UTF8);
        File.WriteAllText(b, "Bline\n", Encoding.UTF8);

        var sut = new ReadLogsCommand(_tempDir);

        var sw = new StringWriter();
        var originalOut = Console.Out;
        var originalIn = Console.In;
        try
        {
            Console.SetOut(sw);
            Console.SetIn(new StringReader("all\n"));
            var ret = sut.Execute(Array.Empty<string>(), new NoopRunLogger());
            Assert.Equal(0, ret);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetIn(originalIn);
        }

        var output = sw.ToString();
        Assert.Contains("Aline", output);
        Assert.Contains("Bline", output);
    }

    [Fact]
    public void Execute_MultipleLogs_SelectNumber_PrintsThatOne()
    {
        var a = Path.Combine(_tempDir, "a.log");
        var b = Path.Combine(_tempDir, "b.log");
        File.WriteAllText(a, "Aline\n", Encoding.UTF8);
        File.WriteAllText(b, "Bline\n", Encoding.UTF8);

        var sut = new ReadLogsCommand(_tempDir);

        var sw = new StringWriter();
        var originalOut = Console.Out;
        var originalIn = Console.In;
        try
        {
            Console.SetOut(sw);
            Console.SetIn(new StringReader("2\n"));
            var ret = sut.Execute(Array.Empty<string>(), new NoopRunLogger());
            Assert.Equal(0, ret);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetIn(originalIn);
        }

        var output = sw.ToString();
        Assert.DoesNotContain("Aline", output);
        Assert.Contains("Bline", output);
    }

    [Fact]
    public void Execute_MultipleLogs_SelectCommaList_PrintsThose()
    {
        var a = Path.Combine(_tempDir, "a.log");
        var b = Path.Combine(_tempDir, "b.log");
        var c = Path.Combine(_tempDir, "c.log");
        File.WriteAllText(a, "A\n", Encoding.UTF8);
        File.WriteAllText(b, "B\n", Encoding.UTF8);
        File.WriteAllText(c, "C\n", Encoding.UTF8);

        var sut = new ReadLogsCommand(_tempDir);

        var sw = new StringWriter();
        var originalOut = Console.Out;
        var originalIn = Console.In;
        try
        {
            Console.SetOut(sw);
            Console.SetIn(new StringReader("1,3\n"));
            var ret = sut.Execute(Array.Empty<string>(), new NoopRunLogger());
            Assert.Equal(0, ret);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetIn(originalIn);
        }

        var output = sw.ToString();
        Assert.Contains("A", output);
        Assert.Contains("C", output);
        Assert.DoesNotContain("B\n", output);
    }
}
