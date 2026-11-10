using System.IO.Compression;

namespace QuickMangaTool.Tests.Processing;

public class ArchiveProcessorTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    private string CreateCbz(byte[] imageData, string imageName = "page1.jpg")
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".cbz");
        _tempFiles.Add(path);

        using var fs = File.Create(path);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: true);
        var img = zip.CreateEntry(imageName, CompressionLevel.Optimal);
        using (var s = img.Open())
            s.Write(imageData, 0, imageData.Length);

        return path;
    }

    [Fact]
    public void Process_ConvertImagesFalse_ReturnsNoChange()
    {
        var cbz = CreateCbz(new byte[] { 1, 2, 3 });

        var ap = new ArchiveProcessor(
            dryRun: false,
            convertImages: false,
            removeOldCbz: false,
            parallelism: 1);

        var result = ap.Process(cbz);

        Assert.Equal(ProcessStatus.NoChange, result.Status);
    }

    [Fact]
    public void Process_Updates_WhenConvertedIsSmaller()
    {
        var original = Enumerable.Repeat((byte)0x21, 1000).ToArray();
        var webp = Enumerable.Repeat((byte)0x00, 100).ToArray();
        var cbz = CreateCbz(original, "page1.jpg");

        var mockConv = new Mock<IArchiveImageConverter>();
        mockConv.Setup(m => m.CanConvert(It.IsAny<string>())).Returns(true);
        mockConv.Setup(m => m.Convert(It.IsAny<string>(), It.IsAny<Stream>()))
            .Returns((string entryName, Stream s) =>
                new ConvertedImage(Path.ChangeExtension(entryName, ".webp"), webp, original.Length, webp.Length, IsSmaller: true, IsBlackAndWhite: false));

        var ap = new ArchiveProcessor(
            dryRun: false,
            convertImages: true,
            removeOldCbz: false,
            parallelism: 1,
            imageConverter: mockConv.Object);

        var result = ap.Process(cbz);

        Assert.Equal(ProcessStatus.Updated, result.Status);

        using var z = ZipFile.OpenRead(cbz);
        Assert.Contains(z.Entries, e => e.FullName.EndsWith(".webp", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(z.Entries, e => e.FullName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Process_EncodesEachImageOnlyOnce_EvenWithBackupCreation()
    {
        var original = Enumerable.Repeat((byte)0x21, 1000).ToArray();
        var webp = Enumerable.Repeat((byte)0x00, 100).ToArray();
        var cbz = CreateCbz(original, "page1.jpg");

        var mockConv = new Mock<IArchiveImageConverter>();
        mockConv.Setup(m => m.CanConvert(It.IsAny<string>())).Returns(true);
        mockConv.Setup(m => m.Convert(It.IsAny<string>(), It.IsAny<Stream>()))
            .Returns((string entryName, Stream s) =>
                new ConvertedImage(Path.ChangeExtension(entryName, ".webp"), webp, original.Length, webp.Length, IsSmaller: true, IsBlackAndWhite: false));

        var ap = new ArchiveProcessor(
            dryRun: false,
            convertImages: true,
            removeOldCbz: false,
            parallelism: 1,
            imageConverter: mockConv.Object);

        var result = ap.Process(cbz);

        Assert.Equal(ProcessStatus.Updated, result.Status);
        // Verify Convert was called exactly ONCE - confirming zero double-encoding
        mockConv.Verify(m => m.Convert("page1.jpg", It.IsAny<Stream>()), Times.Once);
    }

    [Fact]
    public void Process_WithBackup_RemovesOldWhenFlagSet()
    {
        var original = new byte[] { 1, 2, 3, 4 };
        var webp = new byte[] { 9 };
        var cbz = CreateCbz(original, "p.jpg");

        var mockConv = new Mock<IArchiveImageConverter>();
        mockConv.Setup(m => m.CanConvert(It.IsAny<string>())).Returns(true);
        mockConv.Setup(m => m.Convert(It.IsAny<string>(), It.IsAny<Stream>()))
            .Returns((string entryName, Stream s) =>
                new ConvertedImage(Path.ChangeExtension(entryName, ".webp"), webp, original.Length, webp.Length, IsSmaller: true, IsBlackAndWhite: false));

        var ap = new ArchiveProcessor(
            dryRun: false,
            convertImages: true,
            removeOldCbz: true,
            parallelism: 1,
            imageConverter: mockConv.Object);

        var oldPath = Path.Combine(Path.GetDirectoryName(cbz) ?? string.Empty, Path.GetFileNameWithoutExtension(cbz) + ".old" + Path.GetExtension(cbz));
        if (File.Exists(oldPath)) File.Delete(oldPath);

        var result = ap.Process(cbz);

        Assert.Equal(ProcessStatus.Updated, result.Status);
        Assert.False(File.Exists(oldPath), ".old file should be removed when removeOldCbz is true");
    }

    [Fact]
    public void Process_WhenConversionFails_ReturnsError()
    {
        var original = new byte[] { 1, 2, 3, 4 };
        var cbz = CreateCbz(original, "p.jpg");

        var mockConv = new Mock<IArchiveImageConverter>();
        mockConv.Setup(m => m.CanConvert(It.IsAny<string>())).Returns(true);
        mockConv.Setup(m => m.Convert(It.IsAny<string>(), It.IsAny<Stream>()))
            .Throws(new InvalidOperationException("boom"));

        var ap = new ArchiveProcessor(
            dryRun: false,
            convertImages: true,
            removeOldCbz: false,
            parallelism: 1,
            imageConverter: mockConv.Object);

        var result = ap.Process(cbz);

        Assert.Equal(ProcessStatus.Error, result.Status);
        Assert.Contains("Failed to convert", result.ErrorMessage);
    }

    [Fact]
    public void Process_DryRun_WouldUpdate_ReturnsWouldUpdate_AndModifiesNothing()
    {
        var original = Enumerable.Repeat((byte)0x21, 1000).ToArray();
        var webp = Enumerable.Repeat((byte)0x00, 100).ToArray();
        var cbz = CreateCbz(original, "page1.jpg");

        var mockConv = new Mock<IArchiveImageConverter>();
        mockConv.Setup(m => m.CanConvert(It.IsAny<string>())).Returns(true);
        mockConv.Setup(m => m.Convert(It.IsAny<string>(), It.IsAny<Stream>()))
            .Returns((string entryName, Stream s) =>
                new ConvertedImage(Path.ChangeExtension(entryName, ".webp"), webp, original.Length, webp.Length, IsSmaller: true, IsBlackAndWhite: false));

        var ap = new ArchiveProcessor(
            dryRun: true,
            convertImages: true,
            removeOldCbz: false,
            parallelism: 1,
            imageConverter: mockConv.Object);

        var result = ap.Process(cbz);

        Assert.Equal(ProcessStatus.WouldUpdate, result.Status);

        // Verify the file was not modified
        using var z = ZipFile.OpenRead(cbz);
        Assert.Contains(z.Entries, e => e.FullName == "page1.jpg");
        Assert.DoesNotContain(z.Entries, e => e.FullName.EndsWith(".webp"));
    }

    [Fact]
    public void Process_ExistingOldBackup_SkipsImmediately()
    {
        var original = new byte[] { 1, 2, 3 };
        var cbz = CreateCbz(original, "p.jpg");

        var oldPath = Path.Combine(Path.GetDirectoryName(cbz) ?? string.Empty, Path.GetFileNameWithoutExtension(cbz) + ".old" + Path.GetExtension(cbz));
        File.Copy(cbz, oldPath);
        _tempFiles.Add(oldPath);

        var mockConv = new Mock<IArchiveImageConverter>();
        var ap = new ArchiveProcessor(
            dryRun: false,
            convertImages: true,
            removeOldCbz: false,
            parallelism: 1,
            imageConverter: mockConv.Object);

        var result = ap.Process(cbz);

        Assert.Equal(ProcessStatus.NoChange, result.Status);
        mockConv.Verify(m => m.CanConvert(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Process_OriginalKeptWhenNotSmaller_ReturnsNoChange()
    {
        var original = new byte[] { 1, 2, 3, 4 };
        var cbz = CreateCbz(original, "p.jpg");

        var mockConv = new Mock<IArchiveImageConverter>();
        mockConv.Setup(m => m.CanConvert(It.IsAny<string>())).Returns(true);
        mockConv.Setup(m => m.Convert(It.IsAny<string>(), It.IsAny<Stream>()))
            .Returns((string entryName, Stream s) =>
                new ConvertedImage(Path.ChangeExtension(entryName, ".webp"), new byte[] { 9 }, original.Length, 10, IsSmaller: false, IsBlackAndWhite: false));

        var ap = new ArchiveProcessor(
            dryRun: false,
            convertImages: true,
            removeOldCbz: false,
            parallelism: 1,
            imageConverter: mockConv.Object);

        var result = ap.Process(cbz);

        Assert.Equal(ProcessStatus.NoChange, result.Status);

        using var z = ZipFile.OpenRead(cbz);
        Assert.Contains(z.Entries, e => e.FullName == "p.jpg");
    }

    [Fact]
    public void Process_UpdateHookThrows_RestoresOriginalFromBackup()
    {
        var original = new byte[] { 1, 2, 3, 4 };
        var webp = new byte[] { 9 };
        var cbz = CreateCbz(original, "p.jpg");

        var mockConv = new Mock<IArchiveImageConverter>();
        mockConv.Setup(m => m.CanConvert(It.IsAny<string>())).Returns(true);
        mockConv.Setup(m => m.Convert(It.IsAny<string>(), It.IsAny<Stream>()))
            .Returns((string entryName, Stream s) =>
                new ConvertedImage(Path.ChangeExtension(entryName, ".webp"), webp, original.Length, webp.Length, IsSmaller: true, IsBlackAndWhite: false));

        Action<ZipArchive> hook = _ => throw new InvalidOperationException("hook-fail");

        var ap = new ArchiveProcessor(
            dryRun: false,
            convertImages: true,
            removeOldCbz: false,
            parallelism: 1,
            imageConverter: mockConv.Object,
            updateHook: hook);

        var result = ap.Process(cbz);

        Assert.Equal(ProcessStatus.Error, result.Status);
        Assert.Contains("hook-fail", result.ErrorMessage);

        // Archive must be restored
        using var z = ZipFile.OpenRead(cbz);
        Assert.Contains(z.Entries, e => e.FullName == "p.jpg");
    }

    [Fact]
    public void Process_InvokesProgressCallback()
    {
        var original = Enumerable.Repeat((byte)0x21, 1000).ToArray();
        var webp = Enumerable.Repeat((byte)0x00, 100).ToArray();
        var cbz = CreateCbz(original, "page1.jpg");

        var mockConv = new Mock<IArchiveImageConverter>();
        mockConv.Setup(m => m.CanConvert(It.IsAny<string>())).Returns(true);
        mockConv.Setup(m => m.Convert(It.IsAny<string>(), It.IsAny<Stream>()))
            .Returns((string entryName, Stream s) =>
                new ConvertedImage(Path.ChangeExtension(entryName, ".webp"), webp, original.Length, webp.Length, IsSmaller: true, IsBlackAndWhite: false));

        var ap = new ArchiveProcessor(
            dryRun: false,
            convertImages: true,
            removeOldCbz: false,
            parallelism: 1,
            imageConverter: mockConv.Object);

        int reportedCurrent = 0;
        int reportedTotal = 0;
        ap.Process(cbz, (cur, tot) =>
        {
            reportedCurrent = cur;
            reportedTotal = tot;
        });

        Assert.Equal(1, reportedCurrent);
        Assert.Equal(1, reportedTotal);
    }

    public void Dispose()
    {
        foreach (var f in _tempFiles)
        {
            try { if (File.Exists(f)) File.Delete(f); } catch { }
            var old = Path.Combine(Path.GetDirectoryName(f) ?? string.Empty, Path.GetFileNameWithoutExtension(f) + ".old" + Path.GetExtension(f));
            try { if (File.Exists(old)) File.Delete(old); } catch { }
        }
    }
}
