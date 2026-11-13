using System.IO.Compression;
using Spectre.Console;
using SharpCompress.Readers.Rar;

internal interface ICbrArchiveReader
{
    IReadOnlyList<CbrArchiveEntry> ReadEntries(string sourcePath);
}

internal sealed record CbrArchiveEntry(string Key, byte[] Content);

internal interface ICbrConversionProgressReporter
{
    void Run(IReadOnlyList<string> cbrFiles, Action<ICbrConversionProgressTask> action);
}

internal interface ICbrConversionProgressTask
{
    void Update(string status, string path);

    void Increment();
}

internal sealed class SpectreConsoleCbrConversionProgressReporter : ICbrConversionProgressReporter
{
    public void Run(IReadOnlyList<string> cbrFiles, Action<ICbrConversionProgressTask> action)
    {
        ProgressFactory.Create().Start(ctx =>
        {
            var task = ctx.AddTask("Converting CBR files", maxValue: cbrFiles.Count);
            action(new SpectreConsoleCbrConversionProgressTask(task));
        });
    }

    private sealed class SpectreConsoleCbrConversionProgressTask : ICbrConversionProgressTask
    {
        private readonly ProgressTask _task;

        public SpectreConsoleCbrConversionProgressTask(ProgressTask task) => _task = task;

        public void Update(string status, string path)
        {
            _task.Description = Markup.Escape(ProgressDescriptionFormatter.Format(status, Path.GetFileName(path)));
        }

        public void Increment() => _task.Increment(1);
    }
}

internal sealed class SharpCompressCbrArchiveReader : ICbrArchiveReader
{
    public IReadOnlyList<CbrArchiveEntry> ReadEntries(string sourcePath)
    {
        using var sourceStream = File.OpenRead(sourcePath);
        using var reader = RarReader.OpenReader(sourceStream);
           
        var entries = new List<CbrArchiveEntry>();

        while (reader.MoveToNextEntry())
        {
            if (reader.Entry.IsDirectory)
                continue;

            if (string.IsNullOrWhiteSpace(reader.Entry.Key))
                continue;

            using var buffer = new MemoryStream();
            reader.WriteEntryTo(buffer);
            entries.Add(new CbrArchiveEntry(reader.Entry.Key, buffer.ToArray()));
        }

        return entries.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase).ToList();
    }
}

class ConvertCbrToCbzCommand : IAppCommand
{
    private readonly string _root;
    private readonly ICbrArchiveReader _reader;
    private readonly ICbrConversionProgressReporter _progressReporter;
    private readonly bool _deleteSource;

    public ConvertCbrToCbzCommand(string root, bool deleteSource = false)
        : this(root, new SharpCompressCbrArchiveReader(), new SpectreConsoleCbrConversionProgressReporter(), deleteSource)
    {
    }

    internal ConvertCbrToCbzCommand(
        string root,
        ICbrArchiveReader reader,
        ICbrConversionProgressReporter progressReporter,
        bool deleteSource = false)
    {
        _root = root;
        _reader = reader;
        _progressReporter = progressReporter;
        _deleteSource = deleteSource;
    }

    public int Execute(IReadOnlyList<string> cbzFiles, IRunLogger logger)
    {
        if (!Directory.Exists(_root))
        {
            Console.Error.WriteLine($"Directory not found: {_root}");
            return 1;
        }

        var cbrFiles = Directory.EnumerateFiles(_root, "*.cbr", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (cbrFiles.Count == 0)
        {
            Console.WriteLine("No .cbr files found.");
            return 0;
        }

        int converted = 0;
        int skipped = 0;
        int failed = 0;

        _progressReporter.Run(cbrFiles, progress =>
        {
            foreach (var cbrPath in cbrFiles)
            {
                var targetPath = Path.Combine(
                    Path.GetDirectoryName(cbrPath) ?? _root,
                    Path.GetFileNameWithoutExtension(cbrPath) + ".cbz");

                if (File.Exists(targetPath))
                {
                    logger.WriteEntry(new ProcessResult(targetPath, ProcessStatus.NoChange, Detail: "Matching .cbz already exists."));
                    skipped++;
                    progress.Update("Skipped", cbrPath);
                    progress.Increment();
                    continue;
                }

                var tempPath = targetPath + ".tmp";

                try
                {
                    progress.Update("Converting", cbrPath);
                    ConvertSingleArchive(cbrPath, tempPath);
                    File.Move(tempPath, targetPath);

                    logger.WriteEntry(new ProcessResult(targetPath, ProcessStatus.Updated));
                    converted++;
                    progress.Update("Converted", cbrPath);

                    if (_deleteSource && File.Exists(cbrPath))
                    {
                        try { File.Delete(cbrPath); }
                        catch { /* best-effort cleanup */ }
                    }
                }
                catch (Exception ex)
                {
                    if (File.Exists(tempPath))
                    {
                        try
                        {
                            File.Delete(tempPath);
                        }
                        catch
                        {
                            // Ignore cleanup failures and surface the original conversion error.
                        }
                    }

                    logger.WriteEntry(new ProcessResult(cbrPath, ProcessStatus.Error, ex.Message));
                    Console.Error.WriteLine($"Failed to convert {cbrPath}: {ex.Message}");
                    failed++;
                    progress.Update("Failed", cbrPath);
                }

                progress.Increment();
            }
        });

        logger.WriteSummary(cbrFiles.Count, converted, 0);
        Console.WriteLine($"Converted {converted} .cbr file(s). Skipped: {skipped}. Failed: {failed}.");
        return failed > 0 ? 1 : 0;
    }

    private void ConvertSingleArchive(string sourcePath, string targetPath)
    {
        var entries = _reader.ReadEntries(sourcePath);
        if (entries.Count == 0)
            throw new InvalidDataException("Archive contains no files.");

        using var zip = ZipFile.Open(targetPath, ZipArchiveMode.Create);
        foreach (var entry in entries)
        {
            var entryName = entry.Key.Replace('\\', '/').TrimStart('/');
            if (string.IsNullOrWhiteSpace(entryName))
                continue;

            var zipEntry = zip.CreateEntry(entryName, CompressionLevel.NoCompression);
            using var zipStream = zipEntry.Open();
            zipStream.Write(entry.Content, 0, entry.Content.Length);
        }
    }
}
