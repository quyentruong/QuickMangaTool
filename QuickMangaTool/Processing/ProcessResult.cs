enum ProcessStatus { NoChange, Updated, WouldUpdate, Error }

/// <param name="FilePath">Path to the archive that was processed.</param>
/// <param name="Status">Outcome of processing.</param>
/// <param name="ErrorMessage">Set when <paramref name="Status"/> is <see cref="ProcessStatus.Error"/>.</param>
/// <param name="Detail">
/// Optional multi-line supplementary text logged below the entry line
/// (e.g. per-image upscale reasons). Each line should already be indented.
/// </param>
record ProcessResult(
    string FilePath,
    ProcessStatus Status,
    string? ErrorMessage = null,
    string? Detail = null);

