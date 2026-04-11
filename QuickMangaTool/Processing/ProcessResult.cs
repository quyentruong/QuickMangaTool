enum ProcessStatus { NoChange, Updated, WouldUpdate, Error }

record ProcessResult(string FilePath, ProcessStatus Status, string? ErrorMessage = null);
