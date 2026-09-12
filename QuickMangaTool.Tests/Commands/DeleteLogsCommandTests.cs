namespace QuickMangaTool.Tests.Commands;

public class DeleteLogsCommandTests : IDisposable
{
    private readonly string _tempDir;
    private readonly TextReader _originalIn;

    public DeleteLogsCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
        _originalIn = Console.In;
        // Prevent Console.ReadLine from blocking tests by always answering 'y'
        Console.SetIn(new AlwaysYesTextReader());
    }

    public void Dispose()
    {
        // Restore original console input and clean up temp directory
        Console.SetIn(_originalIn);
        Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Execute_DeletesAll_ReturnsZero()
    {
        var a = Path.Combine(_tempDir, "a.log");
        var b = Path.Combine(_tempDir, "b.log");
        File.WriteAllText(a, "A");
        File.WriteAllText(b, "B");

        var sut = new DeleteLogsCommand(_tempDir);
        var ret = sut.Execute(Array.Empty<string>(), new NoopRunLogger());

        Assert.Equal(0, ret);
        Assert.False(File.Exists(a));
        Assert.False(File.Exists(b));
    }

    /// <summary>TextReader that always returns "y" for ReadLine so Console.ReadLine won't block in tests.</summary>
    private class AlwaysYesTextReader : TextReader
    {
        public override string? ReadLine() => "y";
    }

    [Fact]
    public void Execute_NoLogs_ReturnsZero()
    {
        var sut = new DeleteLogsCommand(_tempDir);
        var ret = sut.Execute(Array.Empty<string>(), new NoopRunLogger());
        Assert.Equal(0, ret);
    }
}
