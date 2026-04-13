/// <summary>
/// Describes a single image entry inside a CBZ archive that failed one or more quality checks.
/// </summary>
/// <param name="EntryName">File name of the image inside the archive (e.g. <c>page001.jpg</c>).</param>
/// <param name="Reasons">
/// Human-readable list of checks that triggered, ordered by detection category
/// (dimensions → DPI → blur → noise).
/// </param>
record UpscaleIssue(string EntryName, IReadOnlyList<string> Reasons);
