namespace QuickMangaTool.Tests.Commands;

public class DeleteLogsCommandTests : IDisposable
{
    private readonly string _tempDir;

    public DeleteLogsCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

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

    [Fact]
    public void Execute_NoLogs_ReturnsZero()
    {
        var sut = new DeleteLogsCommand(_tempDir);
        var ret = sut.Execute(Array.Empty<string>(), new NoopRunLogger());
        Assert.Equal(0, ret);
    }
}
