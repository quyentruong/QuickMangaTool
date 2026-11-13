interface IArchiveImageConverter
{
    bool CanConvert(string entryName);

    ConvertedImage Convert(string entryName, Stream input);
}

/// <param name="EntryName">Final entry name to use in the archive (either original or the converted format).</param>
/// <param name="Content">Final bytes to write (whichever format won the size comparison).</param>
/// <param name="SourceBytes">Original image size in bytes.</param>
/// <param name="ConvertedBytes">Converted image size in bytes.</param>
/// <param name="IsSmaller">
/// <see langword="true"/> when the converted output was kept because it was smaller than the source.
/// <see langword="false"/> when the original was kept because it was smaller or equal.
/// </param>
record ConvertedImage(
    string EntryName,
    byte[] Content,
    long SourceBytes,
    long ConvertedBytes,
    bool IsSmaller,
    bool IsBlackAndWhite);