using Spectre.Console;

static class InteractiveMenu
{
    public static AppOptions? Run(string initialDirectory)
    {
        var dir = initialDirectory;
        return PromptOperation(ref dir);
    }

    public static AppOptions? PromptOperation(ref string currentDir)
    {
        while (true)
        {
            currentDir = Path.GetFullPath(string.IsNullOrWhiteSpace(currentDir) || !Directory.Exists(currentDir)
                ? (LastLocationStore.GetLastDirectory() ?? Directory.GetCurrentDirectory())
                : currentDir);

            AnsiConsole.Clear();
            AnsiConsole.Write(new Rule($"[bold cyan]QuickMangaTool v{Markup.Escape(AppVersionHelper.GetCurrentVersionString())}[/]").LeftJustified());
            AnsiConsole.WriteLine();

            var summary = FolderNavigationHelper.GetDirectorySummary(currentDir);
            AnsiConsole.MarkupLine($"[bold]Thư mục làm việc:[/] [yellow]{Markup.Escape(currentDir)}[/]");
            AnsiConsole.MarkupLine($"[dim]Thống kê:[/] [italic]{Markup.Escape(summary.ToDisplayString())}[/]");
            AnsiConsole.WriteLine();

            var action = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold yellow]Chọn tác vụ cần thực hiện / Select operation:[/]")
                    .PageSize(15)
                    .AddChoices(new[]
                    {
                        "1. 🔄 Chuyển đổi ảnh trong .cbz (WebP / AVIF / JXL)",
                        "2. 📦 Nén các thư mục con thành .cbz (Compress Folders)",
                        "3. 📑 Chuyển đổi .cbr sang .cbz (CBR to CBZ)",
                        "4. 📄 Trích xuất .pdf sang .cbz (PDF to CBZ)",
                        "5. 🔍 Quét chất lượng ảnh tìm truyện cần Upscale (Upscale Scan)",
                        "6. 🧹 Dọn dẹp file sao lưu (.old.cbz)",
                        "7. 📁 Đổi thư mục làm việc / Duyệt thư mục (Folder Browser)...",
                        "8. 📜 Xem nhật ký chạy (Read Logs)",
                        "9. 🗑️ Xóa nhật ký chạy (Delete Logs)",
                        "10. ⚙️ Kiểm tra cấu hình libvips (VIPS Diagnostics)",
                        "11. 🧼 Xóa bộ nhớ đệm (Clear Skip Cache)",
                        "12. 🚀 Kiểm tra & Cập nhật ứng dụng (Update App)",
                        "0. 🚪 Thoát (Exit)"
                    }));

            if (action.StartsWith("0."))
                return null;

            if (action.StartsWith("7."))
            {
                var browsed = FolderBrowser.Browse(currentDir);
                if (browsed is not null)
                {
                    currentDir = browsed;
                    LastLocationStore.SaveLastDirectory(currentDir);
                }
                continue;
            }

            if (action.StartsWith("10."))
            {
                return CreateDefaultOptions(currentDir) with { VipsDiagnostics = true };
            }

            if (action.StartsWith("11."))
            {
                return CreateDefaultOptions(currentDir) with { ClearCache = true };
            }

            if (action.StartsWith("12."))
            {
                return CreateDefaultOptions(currentDir) with { UpdateApp = true };
            }

            if (action.StartsWith("8."))
            {
                return CreateDefaultOptions(currentDir) with { ReadLogs = true };
            }

            if (action.StartsWith("9."))
            {
                bool confirmDelete = AnsiConsole.Confirm("Xác nhận xóa tất cả các file .log tìm thấy?", true);
                return CreateDefaultOptions(currentDir) with { DeleteLogs = true, Confirm = confirmDelete };
            }

            // Manga processing actions (1-6): Confirm target directory
            var targetChoice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title($"Tác vụ sẽ áp dụng cho thư mục: [yellow]{Markup.Escape(currentDir)}[/]")
                    .AddChoices(
                        "1. ✔ Tiến hành với thư mục này",
                        "2. 📁 Duyệt / Đổi thư mục khác...",
                        "0. Quay lại menu chính"));

            if (targetChoice.StartsWith("0."))
                continue;

            if (targetChoice.StartsWith("2."))
            {
                var browsed = FolderBrowser.Browse(currentDir);
                if (browsed is not null)
                {
                    currentDir = browsed;
                    LastLocationStore.SaveLastDirectory(currentDir);
                }
                else
                {
                    continue;
                }
            }

            bool convertImages = false;
            bool compressFolders = false;
            bool cbrToCbz = false;
            bool pdfToCbz = false;
            bool detectUpscale = false;
            bool removeOldCbz = false;

            var imageFormat = ImageFormat.Webp;
            int? effort = null;
            bool dryRun = false;
            bool confirm = false;
            bool deleteSource = false;

            if (action.StartsWith("1."))
            {
                convertImages = true;
                (imageFormat, effort) = PromptFormatAndEffort();
                dryRun = AnsiConsole.Confirm("Chạy thử không sửa file (Dry-Run)?", false);
                removeOldCbz = AnsiConsole.Confirm("Xóa file sao lưu .old.cbz sau khi nén thành công?", false);
            }
            else if (action.StartsWith("2."))
            {
                compressFolders = true;
                (imageFormat, effort) = PromptFormatAndEffort();
                deleteSource = AnsiConsole.Confirm("Xóa thư mục con sau khi nén thành .cbz thành công?", false);
            }
            else if (action.StartsWith("3."))
            {
                cbrToCbz = true;
                deleteSource = AnsiConsole.Confirm("Xóa file .cbr gốc sau khi chuyển đổi sang .cbz thành công?", false);
            }
            else if (action.StartsWith("4."))
            {
                pdfToCbz = true;
                (imageFormat, effort) = PromptFormatAndEffort();
                deleteSource = AnsiConsole.Confirm("Xóa file .pdf gốc sau khi tạo .cbz thành công?", false);
            }
            else if (action.StartsWith("5."))
            {
                detectUpscale = true;
            }
            else if (action.StartsWith("6."))
            {
                removeOldCbz = true;
                confirm = AnsiConsole.Confirm("Xác nhận xóa tất cả các file .old.cbz tìm thấy?", true);
            }

            return new AppOptions(
                Directory: currentDir,
                DryRun: dryRun,
                Confirm: confirm,
                Interactive: true,
                DetectUpscale: detectUpscale,
                CompressFolders: compressFolders,
                ConvertImages: convertImages,
                ImageFormat: imageFormat,
                RemoveOldCbz: removeOldCbz,
                DeleteSource: deleteSource,
                ReadLogs: false,
                DeleteLogs: false,
                VipsDiagnostics: false,
                PdfToCbz: pdfToCbz,
                CbrToCbz: cbrToCbz,
                Parallelism: 0,
                Effort: effort,
                ClearCache: false);
        }
    }

    private static (ImageFormat format, int? effort) PromptFormatAndEffort()
    {
        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Chọn định dạng ảnh đích:")
                .AddChoices(
                    "WebP (Khuyên dùng - cân bằng tốt giữa tốc độ và dung lượng)",
                    "AVIF (Nén tối đa)",
                    "JXL / JPEG XL (Chất lượng cao)"));

        var format = choice.StartsWith("AVIF")
            ? ImageFormat.Avif
            : choice.StartsWith("JXL")
                ? ImageFormat.Jxl
                : ImageFormat.Webp;

        int defaultEffort = format switch
        {
            ImageFormat.Avif => 3,
            ImageFormat.Jxl => 5,
            _ => 6
        };

        bool customEffort = AnsiConsole.Confirm($"Mức nén effort mặc định là {defaultEffort}. Bạn có muốn tùy chỉnh không?", false);
        int? effort = null;
        if (customEffort)
        {
            effort = AnsiConsole.Prompt(
                new TextPrompt<int>("Nhập mức effort (0-9):")
                    .DefaultValue(defaultEffort)
                    .Validate(val => val is >= 0 and <= 9 ? ValidationResult.Success() : ValidationResult.Error("Effort phải từ 0 đến 9")));
        }

        return (format, effort);
    }

    private static AppOptions CreateDefaultOptions(string directory) => new(
        Directory: directory,
        DryRun: false,
        Confirm: false,
        Interactive: true,
        DetectUpscale: false,
        CompressFolders: false,
        ConvertImages: false,
        ImageFormat: ImageFormat.Webp,
        RemoveOldCbz: false,
        DeleteSource: false,
        ReadLogs: false,
        DeleteLogs: false,
        VipsDiagnostics: false,
        PdfToCbz: false,
        CbrToCbz: false,
        Parallelism: 0,
        Effort: null,
        ClearCache: false);
}
