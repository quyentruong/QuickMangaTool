static class ProgressDescriptionFormatter
{
    public static string Format(string status, string path, int width)
    {
        var prefix = string.IsNullOrEmpty(status) ? string.Empty : status + ": ";
        if (width <= 0) return prefix + path;

        if (prefix.Length >= width)
            return prefix[..width];

        var remaining = width - prefix.Length;
        string displayPath = path.Length <= remaining
            ? path
            : remaining > 3 ? "..." + path[^(remaining - 3)..] : path[^remaining..];

        var result = prefix + displayPath;
        return result.Length < width ? result.PadRight(width) : result;
    }
}
