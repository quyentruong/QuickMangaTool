using System.Security;

public enum SupportedFileType
{
    Archive,
    Document,
    Image
}

public sealed record SupportedFileInfo(
    string FullPath,
    string FileName,
    long FileSizeBytes,
    SupportedFileType Type)
{
    public string FormattedSize => SizeFormatter.FormatSize(FileSizeBytes);

    public string DisplayIcon => Type switch
    {
        SupportedFileType.Archive => "📦",
        SupportedFileType.Document => "📄",
        SupportedFileType.Image => "🖼️",
        _ => "📄"
    };

    public string Badge => Type switch
    {
        SupportedFileType.Archive => Path.GetExtension(FileName).ToLowerInvariant() switch
        {
            ".cbz" => "[bold magenta]CBZ[/]",
            ".cbr" => "[bold cyan]CBR[/]",
            _ => "[magenta]ARCHIVE[/]"
        },
        SupportedFileType.Document => "[bold red]PDF[/]",
        SupportedFileType.Image => $"[dim]{Path.GetExtension(FileName).TrimStart('.').ToUpperInvariant()}[/]",
        _ => ""
    };
}

public sealed record SupportedFilesResult(
    IReadOnlyList<SupportedFileInfo> Files,
    int HiddenImagesCount);

public readonly record struct DirectorySummary(int SubdirectoryCount, int CbzCount, int CbrCount, int PdfCount)
{
    public bool HasMangaFiles => CbzCount > 0 || CbrCount > 0 || PdfCount > 0;

    public string ToDisplayString()
    {
        var parts = new List<string>(4);
        if (CbzCount > 0) parts.Add($"{CbzCount} .cbz");
        if (CbrCount > 0) parts.Add($"{CbrCount} .cbr");
        if (PdfCount > 0) parts.Add($"{PdfCount} .pdf");
        if (parts.Count == 0) parts.Add("0 file truyện");
        parts.Add($"{SubdirectoryCount} thư mục con");
        return string.Join(", ", parts);
    }
}

public static class FolderNavigationHelper
{
    public static IReadOnlyList<string> GetSubdirectoriesSafely(string directory, bool includeHidden = false)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return Array.Empty<string>();

        try
        {
            var dirs = Directory.EnumerateDirectories(directory);
            if (!includeHidden)
            {
                dirs = dirs.Where(d =>
                {
                    var name = Path.GetFileName(d);
                    return !name.StartsWith('.');
                });
            }

            return dirs.OrderBy(d => Path.GetFileName(d), StringComparer.CurrentCultureIgnoreCase).ToList();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or SecurityException)
        {
            return Array.Empty<string>();
        }
    }

    public static DirectorySummary GetDirectorySummary(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return new DirectorySummary(0, 0, 0, 0);

        int subDirs = 0;
        int cbz = 0;
        int cbr = 0;
        int pdf = 0;

        try
        {
            foreach (var file in Directory.EnumerateFiles(directory))
            {
                var ext = Path.GetExtension(file);
                if (ext.Equals(".cbz", StringComparison.OrdinalIgnoreCase))
                {
                    if (!file.EndsWith(".old.cbz", StringComparison.OrdinalIgnoreCase) &&
                        !file.EndsWith(".cbz.old", StringComparison.OrdinalIgnoreCase))
                    {
                        cbz++;
                    }
                }
                else if (ext.Equals(".cbr", StringComparison.OrdinalIgnoreCase))
                {
                    cbr++;
                }
                else if (ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    pdf++;
                }
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or SecurityException)
        {
            // Ignore unreadable files
        }

        try
        {
            subDirs = Directory.EnumerateDirectories(directory)
                .Count(d => !Path.GetFileName(d).StartsWith('.'));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or SecurityException)
        {
            // Ignore unreadable directories
        }

        return new DirectorySummary(subDirs, cbz, cbr, pdf);
    }

    public static string? GetParentDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            return null;

        var fullPath = Path.GetFullPath(directory);
        var dirInfo = new DirectoryInfo(fullPath);
        return dirInfo.Parent?.FullName;
    }

    public static string GetHomeDirectory()
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    public static IReadOnlyList<(string Name, string Path)> GetQuickLocations()
    {
        var locations = new List<(string Name, string Path)>();
        var home = GetHomeDirectory();
        if (!string.IsNullOrEmpty(home) && Directory.Exists(home))
        {
            locations.Add(("🏠 Thư mục Home (~)", home));
        }

        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady && Directory.Exists(drive.RootDirectory.FullName))
                {
                    locations.Add(($"💾 Ổ đĩa ({drive.Name})", drive.RootDirectory.FullName));
                }
            }
        }
        catch
        {
            // Safe fallback if DriveInfo throws
        }

        return locations;
    }

    public static SupportedFilesResult GetSupportedFilesSafely(string directory, int maxImages = 30)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return new SupportedFilesResult(Array.Empty<SupportedFileInfo>(), 0);

        var archivesAndDocs = new List<SupportedFileInfo>();
        var images = new List<SupportedFileInfo>();

        try
        {
            foreach (var filePath in Directory.EnumerateFiles(directory))
            {
                var fileName = Path.GetFileName(filePath);
                if (fileName.StartsWith('.'))
                    continue;

                var ext = Path.GetExtension(fileName);
                if (ext.Equals(".cbz", StringComparison.OrdinalIgnoreCase))
                {
                    if (fileName.EndsWith(".old.cbz", StringComparison.OrdinalIgnoreCase) ||
                        fileName.EndsWith(".cbz.old", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    long size = SafeGetFileSize(filePath);
                    archivesAndDocs.Add(new SupportedFileInfo(filePath, fileName, size, SupportedFileType.Archive));
                }
                else if (ext.Equals(".cbr", StringComparison.OrdinalIgnoreCase))
                {
                    long size = SafeGetFileSize(filePath);
                    archivesAndDocs.Add(new SupportedFileInfo(filePath, fileName, size, SupportedFileType.Archive));
                }
                else if (ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    long size = SafeGetFileSize(filePath);
                    archivesAndDocs.Add(new SupportedFileInfo(filePath, fileName, size, SupportedFileType.Document));
                }
                else if (ImageExtensions.IsSupportedForAnalysis(fileName))
                {
                    long size = SafeGetFileSize(filePath);
                    images.Add(new SupportedFileInfo(filePath, fileName, size, SupportedFileType.Image));
                }
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or SecurityException)
        {
            // Best-effort
        }

        archivesAndDocs.Sort((a, b) => string.Compare(a.FileName, b.FileName, StringComparison.CurrentCultureIgnoreCase));
        images.Sort((a, b) => string.Compare(a.FileName, b.FileName, StringComparison.CurrentCultureIgnoreCase));

        int hiddenImages = 0;
        var resultList = new List<SupportedFileInfo>(archivesAndDocs.Count + Math.Min(images.Count, maxImages));
        resultList.AddRange(archivesAndDocs);

        if (images.Count <= maxImages)
        {
            resultList.AddRange(images);
        }
        else
        {
            resultList.AddRange(images.Take(maxImages));
            hiddenImages = images.Count - maxImages;
        }

        return new SupportedFilesResult(resultList, hiddenImages);
    }

    private static long SafeGetFileSize(string filePath)
    {
        try
        {
            return new FileInfo(filePath).Length;
        }
        catch
        {
            return 0;
        }
    }
}
