/// <summary>
/// Compares strings using natural numeric ordering so embedded numbers are compared
/// numerically rather than lexicographically (e.g., <c>2_translated</c> before <c>10_translated</c>).
/// </summary>
sealed class NaturalStringComparer : IComparer<string>
{
    public static readonly NaturalStringComparer Instance = new();

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y))
            return 0;
        if (x is null)
            return -1;
        if (y is null)
            return 1;

        int ix = 0;
        int iy = 0;

        while (ix < x.Length && iy < y.Length)
        {
            if (char.IsAsciiDigit(x[ix]) && char.IsAsciiDigit(y[iy]))
            {
                int nx = ix;
                while (nx < x.Length && char.IsAsciiDigit(x[nx]))
                    nx++;

                int ny = iy;
                while (ny < y.Length && char.IsAsciiDigit(y[ny]))
                    ny++;

                int sx = ix;
                while (sx < nx && x[sx] == '0')
                    sx++;

                int sy = iy;
                while (sy < ny && y[sy] == '0')
                    sy++;

                int lenX = nx - sx;
                int lenY = ny - sy;
                if (lenX != lenY)
                    return lenX.CompareTo(lenY);

                for (int k = 0; k < lenX; k++)
                {
                    int digitCmp = x[sx + k].CompareTo(y[sy + k]);
                    if (digitCmp != 0)
                        return digitCmp;
                }

                ix = nx;
                iy = ny;
                continue;
            }

            int charCmp = char.ToUpperInvariant(x[ix]).CompareTo(char.ToUpperInvariant(y[iy]));
            if (charCmp != 0)
                return charCmp;

            ix++;
            iy++;
        }

        int remainingCmp = (x.Length - ix).CompareTo(y.Length - iy);
        return remainingCmp != 0 ? remainingCmp : string.CompareOrdinal(x, y);
    }
}

/// <summary>
/// Plans sequential zero-padded page renames (<c>001</c>, <c>002</c>, ...) within each directory
/// inside an archive using natural numeric filename ordering.
/// </summary>
static class PageRenamePlanner
{
    public static Dictionary<string, string> BuildRenameMap(
        IReadOnlyList<string> entryNames,
        IReadOnlyDictionary<string, string>? extensionOverrides = null)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (entryNames.Count == 0)
            return result;

        var groups = new Dictionary<string, List<(string EntryName, string FileName, string Stem)>>(StringComparer.Ordinal);

        foreach (var entryName in entryNames)
        {
            int lastSlash = entryName.LastIndexOf('/');
            string dirPrefix = lastSlash >= 0 ? entryName[..(lastSlash + 1)] : string.Empty;
            string fileName = lastSlash >= 0 ? entryName[(lastSlash + 1)..] : entryName;
            string stem = Path.GetFileNameWithoutExtension(fileName);

            if (!groups.TryGetValue(dirPrefix, out var group))
            {
                group = new List<(string EntryName, string FileName, string Stem)>();
                groups[dirPrefix] = group;
            }

            group.Add((entryName, fileName, stem));
        }

        foreach (var (dirPrefix, group) in groups)
        {
            group.Sort((a, b) =>
            {
                int cmp = NaturalStringComparer.Instance.Compare(a.Stem, b.Stem);
                if (cmp != 0)
                    return cmp;

                cmp = NaturalStringComparer.Instance.Compare(a.FileName, b.FileName);
                if (cmp != 0)
                    return cmp;

                return string.CompareOrdinal(a.EntryName, b.EntryName);
            });

            int padWidth = Math.Max(3, group.Count.ToString().Length);
            var numberFormat = $"D{padWidth}";

            for (int i = 0; i < group.Count; i++)
            {
                var entryName = group[i].EntryName;
                string ext = extensionOverrides is not null && extensionOverrides.TryGetValue(entryName, out var overrideExt)
                    ? overrideExt
                    : Path.GetExtension(entryName).ToLowerInvariant();

                result[entryName] = $"{dirPrefix}{(i + 1).ToString(numberFormat)}{ext}";
            }
        }

        return result;
    }
}
