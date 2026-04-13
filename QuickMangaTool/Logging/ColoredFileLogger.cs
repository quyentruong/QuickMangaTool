using System.Text;

class ColoredFileLogger : IBatchLogger
{
    private const string Reset  = "\x1b[0m";
    private const string Bold   = "\x1b[1m";
    private const string Green  = "\x1b[92m";
    private const string Yellow = "\x1b[93m";
    private const string Cyan   = "\x1b[96m";
    private const string Red    = "\x1b[91m";
    private const string Gray   = "\x1b[90m";
    private const string White  = "\x1b[97m";
    private const string Blue   = "\x1b[94m";
    private const string Rule   = "════════════════════════════════════════════════════════════════════════════════";

    private readonly StreamWriter _writer;

    public ColoredFileLogger(string filePath)
    {
        _writer = new StreamWriter(filePath, append: false, Encoding.UTF8);
    }

    public void WriteHeader(DateTime timestamp, bool dryRun, string feature)
    {
        _writer.WriteLine($"{Bold}{Blue}{Rule}{Reset}");
        _writer.WriteLine($"  {Bold}{White}QuickMangaTool  \u00b7  {timestamp:yyyy-MM-dd HH:mm:ss}  \u00b7  Feature: {feature}  \u00b7  Mode: {(dryRun ? "Dry Run" : "Apply Changes")}{Reset}");
        _writer.WriteLine($"{Bold}{Blue}{Rule}{Reset}");
        _writer.WriteLine();
        _writer.Flush();
    }

    public void WriteEntry(ProcessResult result)
    {
        var (color, icon, label) = result.Status switch
        {
            ProcessStatus.Updated     => (Green,  "\u2713", "Updated"),
            ProcessStatus.WouldUpdate => (Yellow, "\u25cf", "Would update"),
            ProcessStatus.Error       => (Red,    "\u2717", "Error"),
            _                         => (Gray,   "\u00b7", "No change"),
        };

        _writer.WriteLine($"  {Gray}[{DateTime.Now:HH:mm:ss}]{Reset} {color}{icon} {label,-14}{Reset} {Cyan}{result.FilePath}{Reset}");

        if (result.Status == ProcessStatus.Error && result.ErrorMessage is not null)
            _writer.WriteLine($"    {Red}{result.ErrorMessage}{Reset}");

        if (result.Detail is not null)
            _writer.WriteLine($"    {Gray}{result.Detail}{Reset}");

        _writer.Flush();
    }

    public void WriteSummary(int total, int updatedCount, int wouldUpdateCount)
    {
        _writer.WriteLine();
        _writer.WriteLine($"{Bold}{Blue}{Rule}{Reset}");
        if (wouldUpdateCount > 0)
            _writer.WriteLine($"  {Bold}{White}Summary: Processed {total}  \u00b7  Would update: {Yellow}{wouldUpdateCount}{Reset}{Bold}{White}  \u00b7  No changes made{Reset}");
        else
            _writer.WriteLine($"  {Bold}{White}Summary: Processed {total}  \u00b7  Updated: {Green}{updatedCount}{Reset}");
        _writer.WriteLine($"{Bold}{Blue}{Rule}{Reset}");
        _writer.Flush();
    }

    public void Dispose() => _writer.Dispose();
}
