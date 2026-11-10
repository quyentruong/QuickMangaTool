using Spectre.Console;

public enum FolderChoiceType
{
    SelectCurrent,
    GoUp,
    Subdirectory,
    File,
    QuickLocation,
    ManualInput,
    Cancel
}

public sealed record FolderChoice(FolderChoiceType Type, string TargetPath, string DisplayText)
{
    public override string ToString() => DisplayText;
}

public static class FolderBrowser
{
    public static string? Browse(string? startDirectory = null)
    {
        var preferred = !string.IsNullOrWhiteSpace(startDirectory) && Directory.Exists(startDirectory)
            ? startDirectory
            : (LastLocationStore.GetLastDirectory() ?? Directory.GetCurrentDirectory());

        var currentDir = Path.GetFullPath(preferred);

        while (true)
        {
            AnsiConsole.Clear();
            AnsiConsole.Write(new Rule("[bold cyan]📁 QuickMangaTool - Trình duyệt thư mục[/]").LeftJustified());
            AnsiConsole.WriteLine();

            var summary = FolderNavigationHelper.GetDirectorySummary(currentDir);
            AnsiConsole.MarkupLine($"[bold]Thư mục hiện tại:[/] [yellow]{Markup.Escape(currentDir)}[/]");
            AnsiConsole.MarkupLine($"[dim]Thống kê:[/] [italic]{Markup.Escape(summary.ToDisplayString())}[/]");
            AnsiConsole.WriteLine();

            var prompt = new SelectionPrompt<FolderChoice>()
                .Title("[bold yellow]Dùng ↑/↓ di chuyển, Enter để mở/chọn, gõ chữ để lọc danh sách:[/]")
                .PageSize(15)
                .EnableSearch()
                .UseConverter(c => c.DisplayText);

            var dirName = Path.GetFileName(currentDir);
            if (string.IsNullOrEmpty(dirName))
                dirName = currentDir;

            prompt.AddChoice(new FolderChoice(
                FolderChoiceType.SelectCurrent,
                currentDir,
                $"[bold green]✔ CHỌN THƯ MỤC NÀY[/] ({Markup.Escape(dirName)})"));

            var parent = FolderNavigationHelper.GetParentDirectory(currentDir);
            if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent))
            {
                prompt.AddChoice(new FolderChoice(
                    FolderChoiceType.GoUp,
                    parent,
                    "[yellow]⬆ Lên thư mục cha (..)[/]"));
            }

            var subDirs = FolderNavigationHelper.GetSubdirectoriesSafely(currentDir);
            foreach (var subDir in subDirs)
            {
                var name = Path.GetFileName(subDir);
                prompt.AddChoice(new FolderChoice(
                    FolderChoiceType.Subdirectory,
                    subDir,
                    $"📁 {Markup.Escape(name)}/"));
            }

            var supported = FolderNavigationHelper.GetSupportedFilesSafely(currentDir);
            foreach (var file in supported.Files)
            {
                prompt.AddChoice(new FolderChoice(
                    FolderChoiceType.File,
                    file.FullPath,
                    $"{file.DisplayIcon} {Markup.Escape(file.FileName)} {file.Badge} [dim]({file.FormattedSize})[/]"));
            }

            if (supported.HiddenImagesCount > 0)
            {
                prompt.AddChoice(new FolderChoice(
                    FolderChoiceType.SelectCurrent,
                    currentDir,
                    $"[dim]... và {supported.HiddenImagesCount} file ảnh khác (chọn để dùng thư mục này)[/]"));
            }

            var lastDir = LastLocationStore.GetLastDirectory();
            if (!string.IsNullOrEmpty(lastDir) && Directory.Exists(lastDir) &&
                !string.Equals(lastDir, currentDir, StringComparison.OrdinalIgnoreCase))
            {
                var lastDirName = Path.GetFileName(lastDir);
                if (string.IsNullOrEmpty(lastDirName))
                    lastDirName = lastDir;

                prompt.AddChoice(new FolderChoice(
                    FolderChoiceType.QuickLocation,
                    lastDir,
                    $"[blue]💾 Về thư mục gần nhất (~/{Markup.Escape(lastDirName)})[/]"));
            }

            var home = FolderNavigationHelper.GetHomeDirectory();
            if (!string.IsNullOrEmpty(home) && Directory.Exists(home) &&
                !string.Equals(home, currentDir, StringComparison.OrdinalIgnoreCase))
            {
                prompt.AddChoice(new FolderChoice(
                    FolderChoiceType.QuickLocation,
                    home,
                    "[blue]🏠 Về thư mục Home (~)[/]"));
            }

            prompt.AddChoice(new FolderChoice(
                FolderChoiceType.ManualInput,
                "",
                "[cyan]✏️ Nhập đường dẫn thủ công...[/]"));

            prompt.AddChoice(new FolderChoice(
                FolderChoiceType.Cancel,
                "",
                "[red]❌ Hủy bỏ / Quay lại[/]"));

            var choice = AnsiConsole.Prompt(prompt);

            switch (choice.Type)
            {
                case FolderChoiceType.SelectCurrent:
                case FolderChoiceType.File:
                    LastLocationStore.SaveLastDirectory(currentDir);
                    return currentDir;

                case FolderChoiceType.GoUp:
                case FolderChoiceType.Subdirectory:
                case FolderChoiceType.QuickLocation:
                    if (Directory.Exists(choice.TargetPath))
                    {
                        currentDir = Path.GetFullPath(choice.TargetPath);
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[red]Không thể truy cập thư mục: {Markup.Escape(choice.TargetPath)}[/]");
                        Thread.Sleep(1200);
                    }
                    break;

                case FolderChoiceType.ManualInput:
                    var entered = AnsiConsole.Prompt(
                        new TextPrompt<string>("Nhập đường dẫn thư mục (để trống để quay lại):")
                            .AllowEmpty());

                    if (!string.IsNullOrWhiteSpace(entered))
                    {
                        var resolved = Path.GetFullPath(entered.Trim());
                        if (Directory.Exists(resolved))
                        {
                            currentDir = resolved;
                        }
                        else
                        {
                            AnsiConsole.MarkupLine($"[red]Thư mục không tồn tại: {Markup.Escape(resolved)}[/]");
                            Thread.Sleep(1200);
                        }
                    }
                    break;

                case FolderChoiceType.Cancel:
                    return null;
            }
        }
    }
}
