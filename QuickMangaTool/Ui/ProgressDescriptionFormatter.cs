static class ProgressDescriptionFormatter
{
    public const int DefaultWidth = 48;

    public static string Format(string status, string path, int width = DefaultWidth)
    {
        var prefix = string.IsNullOrEmpty(status) ? string.Empty : status + ": ";
        var fileName = Path.GetFileName(path);
        var textToDisplay = string.IsNullOrEmpty(fileName) ? path : fileName;

        if (width <= 0)
            return prefix + textToDisplay;

        if (prefix.Length >= width)
            return prefix[..width];

        var remaining = width - prefix.Length;
        string display = textToDisplay.Length <= remaining
            ? textToDisplay
            : remaining > 3 ? "..." + textToDisplay[^(remaining - 3)..] : textToDisplay[^remaining..];

        var result = prefix + display;
        return result.Length < width ? result.PadRight(width) : result;
    }
}
