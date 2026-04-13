using Spectre.Console;

static class ProgressFactory
{
    public static Progress Create() =>
        AnsiConsole.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn(),
                new TaskDescriptionColumn());
}
