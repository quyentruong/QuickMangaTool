using System.Diagnostics;

/// <summary>
/// Command to print NetVips diagnostic information to the console.
/// </summary>
class VipsDiagnosticCommand : IAppCommand
{
    public int Execute(IReadOnlyList<string> cbzFiles, IRunLogger logger)
    {
        VipsDiagnostics.PrintDiagnosticReport();
        return 0;
    }

}

public static class VipsDiagnostics
{
    public static void PrintDiagnosticReport()
    {
        
        // 1. Khởi tạo runtime C của libvips trước khi đọc config
        NetVips.NetVips.Init();
        Console.WriteLine("================ VIPS DIAGNOSTIC REPORT ================");
        try
        {
            // 2. Lấy version đầy đủ Major.Minor.Micro (ví dụ: 8.18.3)
            var major = NetVips.NetVips.Version(0);
            var minor = NetVips.NetVips.Version(1);
            var micro = NetVips.NetVips.Version(2);
            Console.WriteLine($"libvips Core Version : {major}.{minor}.{micro}");
            Console.WriteLine($"Concurrency Threads  : {NetVips.NetVips.Concurrency}");

            // 3. Quét chính xác module lõi libvips thay vì plugin phụ
            var currentProcess = Process.GetCurrentProcess();
            var coreModule = currentProcess.Modules
                .Cast<ProcessModule>()
                .FirstOrDefault(m =>
                    m.ModuleName?.StartsWith("libvips", StringComparison.OrdinalIgnoreCase) == true ||
                    m.ModuleName?.StartsWith("vips-42", StringComparison.OrdinalIgnoreCase) == true);

            Console.WriteLine($"Loaded Core Binary   : {coreModule?.FileName ?? "Using OS dynamic linker"}");

            string[] codecs = new[] { "jxl", "heif", "webp" };
            foreach (var c in codecs)
            {
                var loadOk = NetVips.NetVips.TypeFind("VipsOperation", $"{c}load") != IntPtr.Zero;
                var saveOk = NetVips.NetVips.TypeFind("VipsOperation", $"{c}save") != IntPtr.Zero;
                Console.WriteLine($"Codec {c.ToUpper(),-4}           : Load=[{loadOk}] Save=[{saveOk}]");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while gathering VIPS diagnostics: {ex.Message}");
        }

        Console.WriteLine("--------------------------------------------------------");
        Console.WriteLine($"Config Directory     : {AppPaths.GetConfigDirectory()}");
        Console.WriteLine($"Cache Directory      : {AppPaths.GetCacheDirectory()}");
        Console.WriteLine($"Logs Directory       : {AppPaths.GetLogsDirectory()}");
        Console.WriteLine($"Skip Cache File      : {AppPaths.GetSkipCacheFilePath()}");
        Console.WriteLine("========================================================");
    }
}
