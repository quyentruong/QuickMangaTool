using System.Diagnostics;

/// <summary>
/// Command to print NetVips diagnostic information to the console.
/// </summary>
class VipsDiagnosticCommand : IAppCommand
{
    private readonly TextWriter? _output;
    private readonly Func<VipsProbeResult>? _probe;
    private readonly bool? _isWindows;

    public VipsDiagnosticCommand(
        TextWriter? output = null,
        Func<VipsProbeResult>? probe = null,
        bool? isWindows = null)
    {
        _output = output;
        _probe = probe;
        _isWindows = isWindows;
    }

    public int Execute(IReadOnlyList<string> cbzFiles, IRunLogger logger)
    {
        VipsDiagnostics.PrintDiagnosticReport(_output, _probe, _isWindows);
        return 0;
    }
}

internal sealed record VipsCodecSupport(string Name, bool CanLoad, bool CanSave, bool RequireSave = true);

internal sealed record VipsProbeResult(
    bool IsInitialized,
    string? Version,
    int Concurrency,
    string? LoadedBinaryPath,
    IReadOnlyList<VipsCodecSupport> Codecs,
    string? ErrorMessage);

public static class VipsDiagnostics
{
    public static void PrintDiagnosticReport()
        => PrintDiagnosticReport(output: null, probe: null, isWindows: null);

    internal static void PrintDiagnosticReport(
        TextWriter? output,
        Func<VipsProbeResult>? probe = null,
        bool? isWindows = null)
    {
        var writer = output ?? Console.Out;
        var effectiveIsWindows = isWindows ?? OperatingSystem.IsWindows();
        var result = (probe ?? ProbeSystem)();

        writer.WriteLine("================ VIPS DIAGNOSTIC REPORT ================");

        if (!result.IsInitialized)
        {
            writer.WriteLine("libvips Status       : NOT FOUND / FAILED TO LOAD");
            if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                writer.WriteLine($"Error Details        : {result.ErrorMessage}");
            }

            PrintInstallationGuide(writer, effectiveIsWindows, missingCodecsOnly: false);
        }
        else
        {
            writer.WriteLine($"libvips Core Version : {result.Version ?? "Unknown"}");
            writer.WriteLine($"Concurrency Threads  : {result.Concurrency}");
            writer.WriteLine($"Loaded Core Binary   : {result.LoadedBinaryPath ?? "Using OS dynamic linker"}");

            foreach (var codec in result.Codecs)
            {
                writer.WriteLine($"Codec {codec.Name.ToUpperInvariant(),-4}           : Load=[{codec.CanLoad}] Save=[{codec.CanSave}]");
            }

            bool hasMissingRequiredCodec = result.Codecs.Any(c => !c.CanLoad || (c.RequireSave && !c.CanSave));
            if (hasMissingRequiredCodec)
            {
                PrintInstallationGuide(writer, effectiveIsWindows, missingCodecsOnly: true);
            }
        }

        writer.WriteLine("--------------------------------------------------------");
        writer.WriteLine($"Config Directory     : {AppPaths.GetConfigDirectory()}");
        writer.WriteLine($"Cache Directory      : {AppPaths.GetCacheDirectory()}");
        writer.WriteLine($"Logs Directory       : {AppPaths.GetLogsDirectory()}");
        writer.WriteLine($"Skip Cache File      : {AppPaths.GetSkipCacheFilePath()}");
        writer.WriteLine("========================================================");
    }

    internal static VipsProbeResult ProbeSystem()
    {
        try
        {
            // Initialize libvips C runtime inside try/catch so missing native libraries are handled gracefully.
            if (!NetVips.NetVips.Init())
            {
                return new VipsProbeResult(
                    IsInitialized: false,
                    Version: null,
                    Concurrency: 0,
                    LoadedBinaryPath: null,
                    Codecs: Array.Empty<VipsCodecSupport>(),
                    ErrorMessage: "NetVips.Init() returned false (libvips native library could not be initialized).");
            }

            var major = NetVips.NetVips.Version(0);
            var minor = NetVips.NetVips.Version(1);
            var micro = NetVips.NetVips.Version(2);
            var version = $"{major}.{minor}.{micro}";
            var concurrency = NetVips.NetVips.Concurrency;

            string? loadedBinaryPath = null;
            try
            {
                using var currentProcess = Process.GetCurrentProcess();
                var coreModule = currentProcess.Modules
                    .Cast<ProcessModule>()
                    .FirstOrDefault(m =>
                        m.ModuleName?.StartsWith("libvips", StringComparison.OrdinalIgnoreCase) == true ||
                        m.ModuleName?.StartsWith("vips-42", StringComparison.OrdinalIgnoreCase) == true);
                loadedBinaryPath = coreModule?.FileName;
            }
            catch
            {
                // Some platforms or sandboxes restrict process module enumeration; fall back to default display.
            }

            (string Name, bool RequireSave)[] codecSpecs =
            [
                ("jxl", true),
                ("heif", true),
                ("webp", true),
                ("jpeg", false),
                ("png", false),
                ("gif", false),
                ("tiff", false)
            ];

            var codecs = new List<VipsCodecSupport>(codecSpecs.Length);
            foreach (var (name, requireSave) in codecSpecs)
            {
                var loadOk = NetVips.NetVips.TypeFind("VipsOperation", $"{name}load") != IntPtr.Zero;
                var saveOk = NetVips.NetVips.TypeFind("VipsOperation", $"{name}save") != IntPtr.Zero;
                codecs.Add(new VipsCodecSupport(name, loadOk, saveOk, requireSave));
            }

            return new VipsProbeResult(
                IsInitialized: true,
                Version: version,
                Concurrency: concurrency,
                LoadedBinaryPath: loadedBinaryPath,
                Codecs: codecs,
                ErrorMessage: null);
        }
        catch (Exception ex)
        {
            var message = ex.InnerException is not null && !string.IsNullOrWhiteSpace(ex.InnerException.Message)
                ? $"{ex.Message} ({ex.InnerException.Message})"
                : ex.Message;

            return new VipsProbeResult(
                IsInitialized: false,
                Version: null,
                Concurrency: 0,
                LoadedBinaryPath: null,
                Codecs: Array.Empty<VipsCodecSupport>(),
                ErrorMessage: message);
        }
    }

    internal static void PrintInstallationGuide(TextWriter writer, bool isWindows, bool missingCodecsOnly = false)
    {
        writer.WriteLine("--------------------------------------------------------");
        if (missingCodecsOnly)
        {
            writer.WriteLine("[!] Một số codec cần thiết cho chuyển đổi/phân tích ảnh (JXL, HEIF/AVIF, WEBP, JPEG, PNG, GIF, TIFF) chưa được hỗ trợ đầy đủ.");
        }
        else
        {
            writer.WriteLine("[!] Không tìm thấy hoặc không thể nạp thư viện gốc libvips trên hệ thống.");
        }

        if (isWindows)
        {
            writer.WriteLine("Hướng dẫn cài đặt trên Windows (PowerShell / Terminal):");
            writer.WriteLine("  winget install -e --id libvips.libvips");
            writer.WriteLine("Lưu ý: Sau khi cài đặt xong, hãy tắt và mở lại Terminal để cập nhật biến môi trường PATH.");
        }
        else
        {
            writer.WriteLine("Hướng dẫn cài đặt đầy đủ libvips & các gói codec (WebP, AVIF/HEIF, JXL, JPEG, PNG, GIF, TIFF) trên Linux:");
            writer.WriteLine("  • Ubuntu / Debian : sudo apt update && sudo apt install -y libvips libheif-plugin-aomenc libheif-plugin-dav1d");
            writer.WriteLine("  • Arch / Manjaro  : sudo pacman -S --needed libvips libwebp libheif libjxl aom dav1d libjpeg-turbo libpng libtiff cgif");
            writer.WriteLine("  • Fedora / RHEL   : sudo dnf install -y vips libheif-freeworld");
            writer.WriteLine("  • openSUSE        : sudo zypper install -y libvips42 libheif-aom libheif-dav1d");
            writer.WriteLine("  • Alpine Linux    : sudo apk add vips vips-heif vips-jxl");
        }
    }
}
