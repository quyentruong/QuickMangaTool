namespace QuickMangaTool.Tests.Utilities;

public class FolderNavigationHelperTests : IDisposable
{
    private readonly string _testRoot;

    public FolderNavigationHelperTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "QuickMangaTool_NavTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRoot))
                Directory.Delete(_testRoot, true);
        }
        catch
        {
            // Ignore cleanup failures in tests
        }
    }

    [Fact]
    public void GetSubdirectoriesSafely_ReturnsEmpty_WhenDirectoryDoesNotExist()
    {
        var nonExistent = Path.Combine(_testRoot, "does_not_exist");
        var result = FolderNavigationHelper.GetSubdirectoriesSafely(nonExistent);

        Assert.Empty(result);
        Assert.Empty(FolderNavigationHelper.GetSubdirectoriesSafely(""));
        Assert.Empty(FolderNavigationHelper.GetSubdirectoriesSafely(null!));
    }

    [Fact]
    public void GetSubdirectoriesSafely_FiltersHidden_ByDefault()
    {
        var dirBeta = Path.Combine(_testRoot, "Beta");
        var dirAlpha = Path.Combine(_testRoot, "Alpha");
        var dirHidden = Path.Combine(_testRoot, ".hidden");

        Directory.CreateDirectory(dirBeta);
        Directory.CreateDirectory(dirAlpha);
        Directory.CreateDirectory(dirHidden);

        var normal = FolderNavigationHelper.GetSubdirectoriesSafely(_testRoot, includeHidden: false);
        Assert.Equal(2, normal.Count);
        Assert.Equal("Alpha", Path.GetFileName(normal[0]));
        Assert.Equal("Beta", Path.GetFileName(normal[1]));

        var all = FolderNavigationHelper.GetSubdirectoriesSafely(_testRoot, includeHidden: true);
        Assert.Equal(3, all.Count);
        Assert.Contains(all, d => Path.GetFileName(d) == ".hidden");
    }

    [Fact]
    public void GetDirectorySummary_ReturnsZeroes_WhenDirectoryDoesNotExist()
    {
        var summary = FolderNavigationHelper.GetDirectorySummary(Path.Combine(_testRoot, "non_existent"));

        Assert.Equal(0, summary.CbzCount);
        Assert.Equal(0, summary.CbrCount);
        Assert.Equal(0, summary.PdfCount);
        Assert.Equal(0, summary.SubdirectoryCount);
        Assert.False(summary.HasMangaFiles);
    }

    [Fact]
    public void GetDirectorySummary_CountsFilesAndDirectoriesCorrectly()
    {
        File.WriteAllText(Path.Combine(_testRoot, "ch01.cbz"), "fake");
        File.WriteAllText(Path.Combine(_testRoot, "ch02.cbz"), "fake");
        File.WriteAllText(Path.Combine(_testRoot, "ch01.old.cbz"), "backup");
        File.WriteAllText(Path.Combine(_testRoot, "ch02.cbz.old"), "backup");
        File.WriteAllText(Path.Combine(_testRoot, "comic.cbr"), "fake");
        File.WriteAllText(Path.Combine(_testRoot, "doc1.pdf"), "fake");
        File.WriteAllText(Path.Combine(_testRoot, "doc2.pdf"), "fake");
        File.WriteAllText(Path.Combine(_testRoot, "readme.txt"), "fake");

        Directory.CreateDirectory(Path.Combine(_testRoot, "Vol1"));
        Directory.CreateDirectory(Path.Combine(_testRoot, "Vol2"));
        Directory.CreateDirectory(Path.Combine(_testRoot, ".cache"));

        var summary = FolderNavigationHelper.GetDirectorySummary(_testRoot);

        Assert.Equal(2, summary.CbzCount);
        Assert.Equal(1, summary.CbrCount);
        Assert.Equal(2, summary.PdfCount);
        Assert.Equal(2, summary.SubdirectoryCount);
        Assert.True(summary.HasMangaFiles);

        var display = summary.ToDisplayString();
        Assert.Contains("2 .cbz", display);
        Assert.Contains("1 .cbr", display);
        Assert.Contains("2 .pdf", display);
        Assert.Contains("2 thư mục con", display);
    }

    [Fact]
    public void GetDirectorySummary_HandlesEmptyDirectory()
    {
        var summary = FolderNavigationHelper.GetDirectorySummary(_testRoot);

        Assert.Equal(0, summary.CbzCount);
        Assert.Equal(0, summary.CbrCount);
        Assert.Equal(0, summary.PdfCount);
        Assert.Equal(0, summary.SubdirectoryCount);
        Assert.False(summary.HasMangaFiles);
        Assert.Equal("0 file truyện, 0 thư mục con", summary.ToDisplayString());
    }

    [Fact]
    public void GetParentDirectory_ReturnsParent_WhenValid()
    {
        var subDir = Path.Combine(_testRoot, "Sub");
        Directory.CreateDirectory(subDir);

        var parent = FolderNavigationHelper.GetParentDirectory(subDir);
        Assert.NotNull(parent);
        Assert.Equal(Path.GetFullPath(_testRoot), Path.GetFullPath(parent));

        Assert.Null(FolderNavigationHelper.GetParentDirectory(""));
        Assert.Null(FolderNavigationHelper.GetParentDirectory(null!));
    }

    [Fact]
    public void GetHomeDirectory_And_QuickLocations_Work()
    {
        var home = FolderNavigationHelper.GetHomeDirectory();
        Assert.NotNull(home);

        var locations = FolderNavigationHelper.GetQuickLocations();
        Assert.NotNull(locations);
    }

    [Theory]
    [InlineData("NormalFolder")]
    [InlineData("[Author] Manga Title [Vol.01]")]
    [InlineData("Special & Characters <Test>")]
    public void FolderBrowser_MarkupStrings_AreValidAndParseable(string dirName)
    {
        var escaped = Spectre.Console.Markup.Escape(dirName);
        var selectCurrent = $"[bold green]✔ CHỌN THƯ MỤC NÀY[/] ({escaped})";
        var upChoice = "[yellow]⬆ Lên thư mục cha (..)[/]";
        var subChoice = $"📁 {escaped}/";
        var homeChoice = "[blue]🏠 Về thư mục Home (~)[/]";
        var manualChoice = "[cyan]✏️ Nhập đường dẫn thủ công...[/]";
        var cancelChoice = "[red]❌ Hủy bỏ / Quay lại[/]";
        var title = "[bold yellow]Dùng ↑/↓ di chuyển, Enter để mở/chọn, gõ chữ để lọc danh sách:[/]";
        var fileChoice = $"📦 {escaped} [bold magenta]CBZ[/] [dim](124.5 MB)[/]";
        var hiddenSummary = "[dim]... và 45 file ảnh khác (chọn để dùng thư mục này)[/]";

        // All must parse without throwing InvalidOperationException
        _ = new Spectre.Console.Markup(title);
        _ = new Spectre.Console.Markup(selectCurrent);
        _ = new Spectre.Console.Markup(upChoice);
        _ = new Spectre.Console.Markup(subChoice);
        _ = new Spectre.Console.Markup(homeChoice);
        _ = new Spectre.Console.Markup(manualChoice);
        _ = new Spectre.Console.Markup(cancelChoice);
        _ = new Spectre.Console.Markup(fileChoice);
        _ = new Spectre.Console.Markup(hiddenSummary);
    }

    [Fact]
    public void GetSupportedFilesSafely_CategorizesFilesAndLimitsImages_Correctly()
    {
        File.WriteAllText(Path.Combine(_testRoot, "manga01.cbz"), "cbz1");
        File.WriteAllText(Path.Combine(_testRoot, "manga01.old.cbz"), "backup");
        File.WriteAllText(Path.Combine(_testRoot, "comic01.cbr"), "cbr1");
        File.WriteAllText(Path.Combine(_testRoot, "document.pdf"), "pdf1");
        File.WriteAllText(Path.Combine(_testRoot, "ignored.txt"), "text");
        File.WriteAllText(Path.Combine(_testRoot, "page1.jpg"), "jpg");
        File.WriteAllText(Path.Combine(_testRoot, "page2.png"), "png");
        File.WriteAllText(Path.Combine(_testRoot, "page3.webp"), "webp");
        File.WriteAllText(Path.Combine(_testRoot, "page4.jxl"), "jxl");

        var result = FolderNavigationHelper.GetSupportedFilesSafely(_testRoot, maxImages: 2);

        // Archives and Document: manga01.cbz, comic01.cbr, document.pdf (3 files)
        // Images: 4 images exist, maxImages = 2, so 2 included and 2 hidden
        Assert.Equal(5, result.Files.Count);
        Assert.Equal(2, result.HiddenImagesCount);

        Assert.Contains(result.Files, f => f.FileName == "manga01.cbz" && f.Type == SupportedFileType.Archive);
        Assert.Contains(result.Files, f => f.FileName == "comic01.cbr" && f.Type == SupportedFileType.Archive);
        Assert.Contains(result.Files, f => f.FileName == "document.pdf" && f.Type == SupportedFileType.Document);
        Assert.DoesNotContain(result.Files, f => f.FileName == "manga01.old.cbz");
        Assert.DoesNotContain(result.Files, f => f.FileName == "ignored.txt");

        // Verify badge strings parse properly
        foreach (var file in result.Files)
        {
            _ = new Spectre.Console.Markup(file.Badge);
        }
    }
}
