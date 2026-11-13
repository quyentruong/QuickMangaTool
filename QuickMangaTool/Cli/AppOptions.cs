enum ImageFormat
{
    Webp,
    Avif,
    Jxl
}

record AppOptions(
    string Directory,
    bool DryRun,
    bool Confirm,
    bool Interactive,
    bool DetectUpscale,
    bool CompressFolders,
    bool ConvertImages,
    ImageFormat ImageFormat,
    bool RemoveOldCbz,
    bool DeleteSource,
    bool ReadLogs,
    bool DeleteLogs,
    bool VipsDiagnostics,
    bool PdfToCbz,
    bool CbrToCbz,
    int Parallelism,
    int? Effort,
    bool ClearCache,
    bool UpdateApp = false,
    bool UninstallApp = false)
{
    public bool ConvertToWebp => ConvertImages;
}
