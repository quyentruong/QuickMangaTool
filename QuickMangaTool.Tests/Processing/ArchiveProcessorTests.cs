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

    private string CreateCbz(params (string Name, byte[] Data)[] entries)
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".cbz");
        _tempFiles.Add(path);

        using var fs = File.Create(path);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: true);
        foreach (var (name, data) in entries)
        {
            var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
            using var s = entry.Open();
            s.Write(data, 0, data.Length);
        }

        return path;
    }

    private static byte[] ReadEntryBytes(ZipArchive zip, string entryName)
    {
        var entry = zip.GetEntry(entryName);
        Assert.NotNull(entry);
        using var s = entry.Open();
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
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

    [Fact]
    public void Process_WithRenamePagesAndConvert_RenamesInNaturalOrder_AndPreservesNonImageEntries()
    {
        var orig1 = Enumerable.Repeat((byte)0x11, 100).ToArray();
        var orig2 = Enumerable.Repeat((byte)0x22, 100).ToArray();
        var orig10 = Enumerable.Repeat((byte)0x33, 100).ToArray();
        var xmlBytes = "<ComicInfo />"u8.ToArray();

        var cbz = CreateCbz(
            ("10_translated.jpg", orig10),
            ("2_translated.jpg", orig2),
            ("1_translated.jpg", orig1),
            ("ComicInfo.xml", xmlBytes));

        var mockConv = new Mock<IArchiveImageConverter>();
        mockConv.Setup(m => m.CanConvert(It.IsAny<string>())).Returns<string>(ImageExtensions.CanConvert);
        mockConv.Setup(m => m.Convert(It.IsAny<string>(), It.IsAny<Stream>()))
            .Returns((string entryName, Stream s) =>
            {
                var input = new byte[s.Length];
                _ = s.Read(input, 0, input.Length);
                var converted = new byte[] { input[0], 0x99 };
                return new ConvertedImage(
                    Path.ChangeExtension(entryName, ".webp"),
                    converted,
                    input.Length,
                    converted.Length,
                    IsSmaller: true,
                    IsBlackAndWhite: false);
            });

        var ap = new ArchiveProcessor(
            dryRun: false,
            convertImages: true,
            removeOldCbz: false,
            parallelism: 1,
            imageConverter: mockConv.Object,
            renamePages: true);

        var result = ap.Process(cbz);

        Assert.Equal(ProcessStatus.Updated, result.Status);
        Assert.Contains("Images renamed: 3", result.Detail);

        using var z = ZipFile.OpenRead(cbz);
        Assert.Equal(4, z.Entries.Count);
        Assert.Equal(new byte[] { 0x11, 0x99 }, ReadEntryBytes(z, "001.webp"));
        Assert.Equal(new byte[] { 0x22, 0x99 }, ReadEntryBytes(z, "002.webp"));
        Assert.Equal(new byte[] { 0x33, 0x99 }, ReadEntryBytes(z, "003.webp"));
        Assert.Equal(xmlBytes, ReadEntryBytes(z, "ComicInfo.xml"));
        Assert.Null(z.GetEntry("1_translated.jpg"));
        Assert.Null(z.GetEntry("2_translated.jpg"));
        Assert.Null(z.GetEntry("10_translated.jpg"));
    }

    [Fact]
    public void Process_WithRenamePages_WhenNotSmallerOrAlreadyWebp_StillRenamesWithOriginalBytes()
    {
        var jpg2 = new byte[] { 1, 2, 3, 4 };
        var webp10 = new byte[] { 5, 6, 7, 8 };

        var cbz = CreateCbz(
            ("10_translated.webp", webp10),
            ("2_translated.jpg", jpg2));

        var mockConv = new Mock<IArchiveImageConverter>();
        mockConv.Setup(m => m.CanConvert(It.IsAny<string>())).Returns<string>(ImageExtensions.CanConvert);
        mockConv.Setup(m => m.Convert(It.IsAny<string>(), It.IsAny<Stream>()))
            .Returns((string entryName, Stream s) =>
                new ConvertedImage(Path.ChangeExtension(entryName, ".webp"), new byte[] { 9, 9, 9, 9, 9 }, jpg2.Length, 5, IsSmaller: false, IsBlackAndWhite: false));

        var ap = new ArchiveProcessor(
            dryRun: false,
            convertImages: true,
            removeOldCbz: false,
            parallelism: 1,
            imageConverter: mockConv.Object,
            renamePages: true);

        var result = ap.Process(cbz);

        Assert.Equal(ProcessStatus.Updated, result.Status);
        Assert.Contains("Images renamed: 2", result.Detail);

        using var z = ZipFile.OpenRead(cbz);
        Assert.Equal(2, z.Entries.Count);
        Assert.Equal(jpg2, ReadEntryBytes(z, "001.jpg"));
        Assert.Equal(webp10, ReadEntryBytes(z, "002.webp"));
    }

    [Fact]
    public void Process_RenameOnly_RenamesWithoutConverting()
    {
        var webp1 = new byte[] { 10, 20 };
        var jpg10 = new byte[] { 30, 40 };

        var cbz = CreateCbz(
            ("10_translated.jpg", jpg10),
            ("1_translated.webp", webp1));

        var mockConv = new Mock<IArchiveImageConverter>();

        var ap = new ArchiveProcessor(
            dryRun: false,
            convertImages: false,
            removeOldCbz: false,
            parallelism: 1,
            imageConverter: mockConv.Object,
            renamePages: true);

        var result = ap.Process(cbz);

        Assert.Equal(ProcessStatus.Updated, result.Status);
        Assert.Contains("Images renamed: 2", result.Detail);
        mockConv.Verify(m => m.Convert(It.IsAny<string>(), It.IsAny<Stream>()), Times.Never);

        using var z = ZipFile.OpenRead(cbz);
        Assert.Equal(2, z.Entries.Count);
        Assert.Equal(webp1, ReadEntryBytes(z, "001.webp"));
        Assert.Equal(jpg10, ReadEntryBytes(z, "002.jpg"));
    }

    [Fact]
    public void Process_WithRenamePages_OverlappingNames_RenamesSafelyWithoutDataLoss()
    {
        var bytes0 = new byte[] { 0xA0, 0xA1 };
        var bytes1 = new byte[] { 0xB0, 0xB1 };

        var cbz = CreateCbz(
            ("000.jpg", bytes0),
            ("001.jpg", bytes1));

        var ap = new ArchiveProcessor(
            dryRun: false,
            convertImages: false,
            removeOldCbz: false,
            parallelism: 1,
            renamePages: true);

        var result = ap.Process(cbz);

        Assert.Equal(ProcessStatus.Updated, result.Status);

        using var z = ZipFile.OpenRead(cbz);
        Assert.Equal(2, z.Entries.Count);
        Assert.Null(z.GetEntry("000.jpg"));
        Assert.Equal(bytes0, ReadEntryBytes(z, "001.jpg"));
        Assert.Equal(bytes1, ReadEntryBytes(z, "002.jpg"));
    }

    [Fact]
    public void Process_WithRenamePages_AlreadySequentiallyNamed_ReturnsNoChange()
    {
        var cbz = CreateCbz(
            ("001.webp", new byte[] { 1 }),
            ("002.webp", new byte[] { 2 }));

        var mockConv = new Mock<IArchiveImageConverter>();
        mockConv.Setup(m => m.CanConvert(It.IsAny<string>())).Returns<string>(ImageExtensions.CanConvert);

        var ap = new ArchiveProcessor(
            dryRun: false,
            convertImages: true,
            removeOldCbz: false,
            parallelism: 1,
            imageConverter: mockConv.Object,
            renamePages: true);

        var result = ap.Process(cbz);

        Assert.Equal(ProcessStatus.NoChange, result.Status);
    }

    [Fact]
    public void Process_DryRunWithRenamePages_ReturnsWouldUpdate_AndModifiesNothing()
    {
        var bytes1 = new byte[] { 1, 2 };
        var bytes2 = new byte[] { 3, 4 };

        var cbz = CreateCbz(
            ("2_translated.webp", bytes2),
            ("1_translated.webp", bytes1));

        var ap = new ArchiveProcessor(
            dryRun: true,
            convertImages: false,
            removeOldCbz: false,
            parallelism: 1,
            renamePages: true);

        var result = ap.Process(cbz);

        Assert.Equal(ProcessStatus.WouldUpdate, result.Status);
        Assert.Contains("Images renamed: 2", result.Detail);

        using var z = ZipFile.OpenRead(cbz);
        Assert.Equal(2, z.Entries.Count);
        Assert.Equal(bytes1, ReadEntryBytes(z, "1_translated.webp"));
        Assert.Equal(bytes2, ReadEntryBytes(z, "2_translated.webp"));
        Assert.Null(z.GetEntry("001.webp"));
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
