using Spectre.Console;

static class InteractiveMenu
{
    public static AppOptions? Run(string initialDirectory)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[bold cyan]QuickMangaTool[/]").LeftJustified());
        AnsiConsole.WriteLine();

        var dir = AnsiConsole.Prompt(
            new TextPrompt<string>("Thư mục làm việc / Target directory:")
                .DefaultValue(string.IsNullOrWhiteSpace(initialDirectory) ? Directory.GetCurrentDirectory() : initialDirectory)
                .Validate(d => Directory.Exists(d)
                    ? ValidationResult.Success()
                    : ValidationResult.Error($"[red]Thư mục không tồn tại: {d}[/]")));

        dir = Path.GetFullPath(dir);

        var action = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold yellow]Chọn tác vụ cần thực hiện / Select operation:[/]")
                .PageSize(12)
                .AddChoices(new[]
                {
                    "1. Chuyển đổi ảnh trong .cbz (WebP / AVIF / JXL)",
                    "2. Nén các thư mục con thành .cbz (Compress Folders)",
                    "3. Chuyển đổi .cbr sang .cbz (CBR to CBZ)",
                    "4. Trích xuất .pdf sang .cbz (PDF to CBZ)",
                    "5. Quét chất lượng ảnh tìm truyện cần Upscale (Upscale Scan)",
                    "6. Dọn dẹp file sao lưu (.old.cbz)",
                    "7. Xem nhật ký chạy (Read Logs)",
                    "8. Xóa nhật ký chạy (Delete Logs)",
                    "9. Kiểm tra cấu hình libvips (VIPS Diagnostics)",
                    "10. Xóa bộ nhớ đệm (Clear Skip Cache)",
                    "0. Thoát (Exit)"
                }));

        if (action.StartsWith("0."))
            return null;

        bool convertImages = false;
        bool compressFolders = false;
        bool cbrToCbz = false;
        bool pdfToCbz = false;
        bool detectUpscale = false;
        bool removeOldCbz = false;
        bool readLogs = false;
        bool deleteLogs = false;
        bool vipsDiagnostics = false;
        bool clearCache = false;

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
        else if (action.StartsWith("7."))
        {
            readLogs = true;
        }
        else if (action.StartsWith("8."))
        {
            deleteLogs = true;
            confirm = AnsiConsole.Confirm("Xác nhận xóa tất cả các file .log tìm thấy?", true);
        }
        else if (action.StartsWith("9."))
        {
            vipsDiagnostics = true;
        }
        else if (action.StartsWith("10."))
        {
            clearCache = true;
        }

        return new AppOptions(
            Directory: dir,
            DryRun: dryRun,
            Confirm: confirm,
            Interactive: true,
            DetectUpscale: detectUpscale,
            CompressFolders: compressFolders,
            ConvertImages: convertImages,
            ImageFormat: imageFormat,
            RemoveOldCbz: removeOldCbz,
            DeleteSource: deleteSource,
            ReadLogs: readLogs,
            DeleteLogs: deleteLogs,
            VipsDiagnostics: vipsDiagnostics,
            PdfToCbz: pdfToCbz,
            CbrToCbz: cbrToCbz,
            Parallelism: 0,
            Effort: effort,
            ClearCache: clearCache);
    }

    private static (ImageFormat format, int? effort) PromptFormatAndEffort()
    {
        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Chọn định dạng ảnh đích:")
                .AddChoices("WebP (Khuyên dùng - cân bằng tốt giữa tốc độ và dung lượng)", "AVIF (Nén tối đa)", "JXL / JPEG XL (Chất lượng cao)"));

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
}
