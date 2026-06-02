static class SizeFormatter
{
    public static string FormatSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        double kb = bytes / 1024.0;
        if (kb < 1024)
            return $"{kb:F1} KB";
        double mb = kb / 1024.0;
        if (mb < 1024)
            return $"{mb:F2} MB";
        double gb = mb / 1024.0;
        return $"{gb:F2} GB";
    }
}
