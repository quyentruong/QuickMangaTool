enum ImageFormat
{
    Webp,
    Avif
}

record AppOptions(
    string Directory,
    bool DryRun,
    bool AddDuplicates,
    bool GenreToTags,
    bool DetectUpscale,
    bool DetectDuplicates,
    bool CompressFolders,
    bool ConvertToWebp,
    ImageFormat ImageFormat,
    bool RemoveOldCbz,
    bool ReadLogs,
    bool DeleteLogs,
    bool PdfToCbz,
    bool CbrToCbz,
    int Parallelism,
    int PhashThreshold,
    bool ClearCache);
