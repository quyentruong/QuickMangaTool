using Moq;

namespace QuickMangaTool.Tests.Commands;

/// <summary>
/// Unit tests for <see cref="RemoveOldCbzCommand"/>.
/// Uses a temporary directory so real file-system operations can be exercised
/// without touching production paths.
/// </summary>
public class RemoveOldCbzCommandTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Mock<IRunLogger> _loggerMock = new();
    private readonly RemoveOldCbzCommand _sut = new();

    public RemoveOldCbzCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    // ── No .old.cbz files present ─────────────────────────────────────────

    [Fact]
    public void Execute_NoOldFilesExist_ReturnsZero()
    {
        var cbzFiles = new[] { CbzPath("vol1.cbz") };

        var result = _sut.Execute(cbzFiles, _loggerMock.Object);

        Assert.Equal(0, result);
    }

    [Fact]
    public void Execute_NoOldFilesExist_DoesNotCallWriteEntry()
    {
        var cbzFiles = new[] { CbzPath("vol1.cbz") };

        _sut.Execute(cbzFiles, _loggerMock.Object);

        _loggerMock.Verify(l => l.WriteEntry(It.IsAny<ProcessResult>()), Times.Never);
    }

    [Fact]
    public void Execute_EmptyCbzList_ReturnsZero()
    {
        var result = _sut.Execute([], _loggerMock.Object);

        Assert.Equal(0, result);
    }

    // ── One .old.cbz file present ─────────────────────────────────────────

    [Fact]
    public void Execute_OneOldFileExists_ReturnsZero()
    {
        CreateOldCbz("vol1.cbz");
        var cbzFiles = new[] { CbzPath("vol1.cbz") };

        var result = _sut.Execute(cbzFiles, _loggerMock.Object);

        Assert.Equal(0, result);
    }

    [Fact]
    public void Execute_OneOldFileExists_DeletesFile()
    {
        var oldPath = CreateOldCbz("vol1.cbz");
        var cbzFiles = new[] { CbzPath("vol1.cbz") };

        _sut.Execute(cbzFiles, _loggerMock.Object);

        Assert.False(File.Exists(oldPath));
    }

    [Fact]
    public void Execute_OneOldFileExists_CallsWriteEntryOnce()
    {
        CreateOldCbz("vol1.cbz");
        var cbzFiles = new[] { CbzPath("vol1.cbz") };

        _sut.Execute(cbzFiles, _loggerMock.Object);

        _loggerMock.Verify(l => l.WriteEntry(It.IsAny<ProcessResult>()), Times.Once);
    }

    [Fact]
    public void Execute_OneOldFileExists_LogsUpdatedStatus()
    {
        CreateOldCbz("vol1.cbz");
        var cbzFiles = new[] { CbzPath("vol1.cbz") };

        _sut.Execute(cbzFiles, _loggerMock.Object);

        _loggerMock.Verify(l => l.WriteEntry(
            It.Is<ProcessResult>(r => r.Status == ProcessStatus.Updated)), Times.Once);
    }

    [Fact]
    public void Execute_OneOldFileExists_CallsWriteSummaryWithCorrectCounts()
    {
        CreateOldCbz("vol1.cbz");
        var cbzFiles = new[] { CbzPath("vol1.cbz") };

        _sut.Execute(cbzFiles, _loggerMock.Object);

        // total=1, updatedCount=1, wouldUpdateCount=0
        _loggerMock.Verify(l => l.WriteSummary(1, 1, 0), Times.Once);
    }

    // ── Multiple .old.cbz files ───────────────────────────────────────────

    [Fact]
    public void Execute_MultipleOldFilesExist_DeletesAll()
    {
        var oldPaths = new[] { CreateOldCbz("vol1.cbz"), CreateOldCbz("vol2.cbz") };
        var cbzFiles = oldPaths.Select(p => p.Replace(".old.cbz", ".cbz")).ToArray();

        _sut.Execute(cbzFiles, _loggerMock.Object);

        Assert.All(oldPaths, p => Assert.False(File.Exists(p)));
    }

    [Fact]
    public void Execute_MultipleOldFilesExist_ReturnsZero()
    {
        CreateOldCbz("vol1.cbz");
        CreateOldCbz("vol2.cbz");
        var cbzFiles = new[] { CbzPath("vol1.cbz"), CbzPath("vol2.cbz") };

        var result = _sut.Execute(cbzFiles, _loggerMock.Object);

        Assert.Equal(0, result);
    }

    [Fact]
    public void Execute_MultipleOldFilesExist_CallsWriteEntryForEach()
    {
        CreateOldCbz("vol1.cbz");
        CreateOldCbz("vol2.cbz");
        var cbzFiles = new[] { CbzPath("vol1.cbz"), CbzPath("vol2.cbz") };

        _sut.Execute(cbzFiles, _loggerMock.Object);

        _loggerMock.Verify(l => l.WriteEntry(It.IsAny<ProcessResult>()), Times.Exactly(2));
    }

    // ── Mixed: some .old.cbz files present, some not ─────────────────────

    [Fact]
    public void Execute_OnlySubsetHasOldFiles_DeletesOnlyExistingOnes()
    {
        var oldPath = CreateOldCbz("vol1.cbz");
        // vol2.old.cbz is intentionally NOT created
        var cbzFiles = new[] { CbzPath("vol1.cbz"), CbzPath("vol2.cbz") };

        _sut.Execute(cbzFiles, _loggerMock.Object);

        Assert.False(File.Exists(oldPath));
        _loggerMock.Verify(l => l.WriteEntry(It.IsAny<ProcessResult>()), Times.Once);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>Returns the full path for a .cbz file name inside the temp directory.</summary>
    private string CbzPath(string fileName) => Path.Combine(_tempDir, fileName);

    /// <summary>
    /// Creates an empty .old.cbz backup file for the given .cbz file name and returns its full path.
    /// </summary>
    private string CreateOldCbz(string cbzFileName)
    {
        var nameWithoutExt = Path.GetFileNameWithoutExtension(cbzFileName); // e.g. "vol1"
        var ext = Path.GetExtension(cbzFileName);                           // e.g. ".cbz"
        var oldPath = Path.Combine(_tempDir, nameWithoutExt + ".old" + ext);
        File.WriteAllBytes(oldPath, []);
        return oldPath;
    }
}
