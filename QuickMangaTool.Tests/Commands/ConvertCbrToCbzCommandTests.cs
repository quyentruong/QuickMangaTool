using System.IO.Compression;

namespace QuickMangaTool.Tests.Commands;

public class ConvertCbrToCbzCommandTests : IDisposable
{
    private readonly string _tempDir;

    public ConvertCbrToCbzCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public void Execute_ConvertsRecursively_WritesCbzNextToEachCbr()
    {
        var rootCbr = CreateCbr("root.cbr");
        var nestedCbr = CreateCbr(Path.Combine("nested", "chapter-01.cbr"));
        var reader = new FakeCbrArchiveReader();
        var progress = new FakeCbrConversionProgressReporter();
        reader.SetEntries(rootCbr, ("001.jpg", [1, 2, 3]));
        reader.SetEntries(nestedCbr, ("pages/cover.png", [4, 5]), ("pages/page-01.png", [6]));

        var sut = new ConvertCbrToCbzCommand(_tempDir, reader, progress);
        var result = sut.Execute(Array.Empty<string>(), new NoopRunLogger());

        Assert.Equal(0, result);
        Assert.True(File.Exists(Path.ChangeExtension(rootCbr, ".cbz")));
        Assert.True(File.Exists(Path.ChangeExtension(nestedCbr, ".cbz")));

        AssertEntryBytes(Path.ChangeExtension(rootCbr, ".cbz"), "001.jpg", [1, 2, 3]);
        AssertEntryBytes(Path.ChangeExtension(nestedCbr, ".cbz"), "pages/cover.png", [4, 5]);
        AssertEntryBytes(Path.ChangeExtension(nestedCbr, ".cbz"), "pages/page-01.png", [6]);
    }

    [Fact]
    public void Execute_ExistingCbzSkipsConversion_LeavesFileUntouched()
    {
        var cbrPath = CreateCbr("already-done.cbr");
        var cbzPath = Path.ChangeExtension(cbrPath, ".cbz");
        File.WriteAllText(cbzPath, "keep-me");

        var reader = new FakeCbrArchiveReader();
        var progress = new FakeCbrConversionProgressReporter();
        reader.SetEntries(cbrPath, ("001.jpg", [1]));

        var sut = new ConvertCbrToCbzCommand(_tempDir, reader, progress);
        var result = sut.Execute(Array.Empty<string>(), new NoopRunLogger());

        Assert.Equal(0, result);
        Assert.Equal("keep-me", File.ReadAllText(cbzPath));
        Assert.Equal(0, reader.CallCount);
    }

    [Fact]
    public void Execute_WhenReaderThrows_ReturnsErrorAndKeepsSuccessfulConverts()
    {
        var successCbr = CreateCbr("good.cbr");
        var failingCbr = CreateCbr("bad.cbr");
        var reader = new FakeCbrArchiveReader();
        var progress = new FakeCbrConversionProgressReporter();
        reader.SetEntries(successCbr, ("001.jpg", [7, 8]));
        reader.SetFailure(failingCbr, new InvalidDataException("boom"));

        var sut = new ConvertCbrToCbzCommand(_tempDir, reader, progress);
        var result = sut.Execute(Array.Empty<string>(), new NoopRunLogger());

        Assert.Equal(1, result);
        Assert.True(File.Exists(Path.ChangeExtension(successCbr, ".cbz")));
        Assert.False(File.Exists(Path.ChangeExtension(failingCbr, ".cbz")));
    }

    [Fact]
    public void Execute_ReportsProgressForEachArchive()
    {
        var firstCbr = CreateCbr("first.cbr");
        var secondCbr = CreateCbr("second.cbr");
        var reader = new FakeCbrArchiveReader();
        var progress = new FakeCbrConversionProgressReporter();
        reader.SetEntries(firstCbr, ("001.jpg", [1]));
        reader.SetEntries(secondCbr, ("001.jpg", [2]));

        var sut = new ConvertCbrToCbzCommand(_tempDir, reader, progress);
        var result = sut.Execute(Array.Empty<string>(), new NoopRunLogger());

        Assert.Equal(0, result);
        Assert.Equal(4, progress.Task.Updates.Count);
        Assert.Contains(progress.Task.Updates, update => update.Status == "Converting" && update.Path == firstCbr);
        Assert.Contains(progress.Task.Updates, update => update.Status == "Converted" && update.Path == firstCbr);
        Assert.Contains(progress.Task.Updates, update => update.Status == "Converting" && update.Path == secondCbr);
        Assert.Contains(progress.Task.Updates, update => update.Status == "Converted" && update.Path == secondCbr);
        Assert.Equal(2, progress.Task.IncrementCount);
    }

    private string CreateCbr(string relativePath)
    {
        var path = Path.Combine(_tempDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "placeholder");
        return path;
    }

    private static void AssertEntryBytes(string cbzPath, string entryName, byte[] expected)
    {
        using var archive = ZipFile.OpenRead(cbzPath);
        var entry = archive.GetEntry(entryName);
        Assert.NotNull(entry);

        using var stream = entry!.Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        Assert.Equal(expected, buffer.ToArray());
    }

    private sealed class FakeCbrArchiveReader : ICbrArchiveReader
    {
        private readonly Dictionary<string, IReadOnlyList<CbrArchiveEntry>> _entries = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Exception> _failures = new(StringComparer.OrdinalIgnoreCase);

        public int CallCount { get; private set; }

        public void SetEntries(string path, params (string Key, byte[] Content)[] entries)
            => _entries[path] = entries.Select(entry => new CbrArchiveEntry(entry.Key, entry.Content)).ToList();

        public void SetFailure(string path, Exception exception)
            => _failures[path] = exception;

        public IReadOnlyList<CbrArchiveEntry> ReadEntries(string sourcePath)
        {
            CallCount++;

            if (_failures.TryGetValue(sourcePath, out var failure))
                throw failure;

            return _entries.TryGetValue(sourcePath, out var entries)
                ? entries
                : [];
        }
    }

    private sealed class FakeCbrConversionProgressReporter : ICbrConversionProgressReporter
    {
        public FakeProgressTask Task { get; } = new();

        public void Run(IReadOnlyList<string> cbrFiles, Action<ICbrConversionProgressTask> action)
            => action(Task);
    }

    private sealed class FakeProgressTask : ICbrConversionProgressTask
    {
        public List<(string Status, string Path)> Updates { get; } = [];

        public int IncrementCount { get; private set; }

        public void Update(string status, string path) => Updates.Add((status, path));

        public void Increment() => IncrementCount++;
    }
}