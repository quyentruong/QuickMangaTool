using Spectre.Console;

static class ProgressFactory
{
    public static Progress Create() =>
        AnsiConsole.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(
                new SpinnerColumn(),
                new TaskDescriptionColumn { Alignment = Justify.Left },
                new ProgressBarColumn { Width = 32 },
                new PercentageColumn(),
                new ElapsedTimeColumn());
}
