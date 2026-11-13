namespace QuickMangaTool.Tests.Commands;

public class VipsDiagnosticCommandTests
{
    [Fact]
    public void Execute_WhenLibvipsMissingOnWindows_PrintsWingetGuideAndAppDirectories()
    {
        using var output = new StringWriter();
        var probeResult = new VipsProbeResult(
            IsInitialized: false,
            Version: null,
            Concurrency: 0,
            LoadedBinaryPath: null,
            Codecs: Array.Empty<VipsCodecSupport>(),
            ErrorMessage: "Unable to load shared library 'libvips-42.dll'");

        var cmd = new VipsDiagnosticCommand(
            output: output,
            probe: () => probeResult,
            isWindows: true);

        var exitCode = cmd.Execute(Array.Empty<string>(), new NoopRunLogger());

        Assert.Equal(0, exitCode);
        var text = output.ToString();
        Assert.Contains("NOT FOUND", text);
        Assert.Contains("Unable to load shared library 'libvips-42.dll'", text);
        Assert.Contains("winget install -e --id libvips.libvips", text);
        Assert.DoesNotContain("sudo apt", text);
        Assert.Contains("Config Directory", text);
        Assert.Contains("Cache Directory", text);
        Assert.Contains("Logs Directory", text);
    }

    [Fact]
    public void Execute_WhenLibvipsMissingOnLinux_PrintsLinuxDistroGuidesAndAppDirectories()
    {
        using var output = new StringWriter();
        var probeResult = new VipsProbeResult(
            IsInitialized: false,
            Version: null,
            Concurrency: 0,
            LoadedBinaryPath: null,
            Codecs: Array.Empty<VipsCodecSupport>(),
            ErrorMessage: "Unable to load shared library 'libvips.so.42'");

        var cmd = new VipsDiagnosticCommand(
            output: output,
            probe: () => probeResult,
            isWindows: false);

        var exitCode = cmd.Execute(Array.Empty<string>(), new NoopRunLogger());

        Assert.Equal(0, exitCode);
        var text = output.ToString();
        Assert.Contains("NOT FOUND", text);
        Assert.Contains("sudo apt update && sudo apt install -y libvips libheif-plugin-aomenc libheif-plugin-dav1d", text);
        Assert.Contains("pacman", text);
        Assert.Contains("dnf", text);
        Assert.Contains("zypper", text);
        Assert.Contains("apk", text);
        Assert.DoesNotContain("winget install", text);
        Assert.Contains("Config Directory", text);
        Assert.Contains("Cache Directory", text);
        Assert.Contains("Logs Directory", text);
    }

    [Fact]
    public void Execute_WhenLibvipsLoadedButCodecMissingOnLinux_PrintsMissingCodecGuide()
    {
        using var output = new StringWriter();
        var probeResult = new VipsProbeResult(
            IsInitialized: true,
            Version: "8.18.0",
            Concurrency: 8,
            LoadedBinaryPath: "/usr/lib/x86_64-linux-gnu/libvips.so.42",
            Codecs: new[]
            {
                new VipsCodecSupport("jxl", true, true, RequireSave: true),
                new VipsCodecSupport("heif", true, false, RequireSave: true),
                new VipsCodecSupport("webp", true, true, RequireSave: true),
                new VipsCodecSupport("jpeg", true, true, RequireSave: false),
                new VipsCodecSupport("png", true, true, RequireSave: false),
                new VipsCodecSupport("gif", true, false, RequireSave: false),
                new VipsCodecSupport("tiff", true, true, RequireSave: false)
            },
            ErrorMessage: null);

        var cmd = new VipsDiagnosticCommand(
            output: output,
            probe: () => probeResult,
            isWindows: false);

        var exitCode = cmd.Execute(Array.Empty<string>(), new NoopRunLogger());

        Assert.Equal(0, exitCode);
        var text = output.ToString();
        Assert.Contains("libvips Core Version : 8.18.0", text);
        Assert.Contains("Codec HEIF", text);
        Assert.Contains("Save=[False]", text);
        Assert.Contains("sudo apt update && sudo apt install -y libvips libheif-plugin-aomenc libheif-plugin-dav1d", text);
        Assert.Contains("Config Directory", text);
    }

    [Fact]
    public void Execute_WhenLibvipsAndAllCodecsAvailable_DoesNotPrintInstallationGuide()
    {
        using var output = new StringWriter();
        var probeResult = new VipsProbeResult(
            IsInitialized: true,
            Version: "8.18.3",
            Concurrency: 12,
            LoadedBinaryPath: @"C:\Program Files\libvips\bin\libvips-42.dll",
            Codecs: new[]
            {
                new VipsCodecSupport("jxl", true, true, RequireSave: true),
                new VipsCodecSupport("heif", true, true, RequireSave: true),
                new VipsCodecSupport("webp", true, true, RequireSave: true),
                new VipsCodecSupport("jpeg", true, true, RequireSave: false),
                new VipsCodecSupport("png", true, true, RequireSave: false),
                new VipsCodecSupport("gif", true, false, RequireSave: false),
                new VipsCodecSupport("tiff", true, true, RequireSave: false)
            },
            ErrorMessage: null);

        var cmd = new VipsDiagnosticCommand(
            output: output,
            probe: () => probeResult,
            isWindows: true);

        var exitCode = cmd.Execute(Array.Empty<string>(), new NoopRunLogger());

        Assert.Equal(0, exitCode);
        var text = output.ToString();
        Assert.Contains("libvips Core Version : 8.18.3", text);
        Assert.Contains("Codec JXL", text);
        Assert.Contains("Codec HEIF", text);
        Assert.Contains("Codec WEBP", text);
        Assert.Contains("Codec JPEG", text);
        Assert.Contains("Codec PNG", text);
        Assert.Contains("Codec GIF", text);
        Assert.Contains("Codec TIFF", text);
        Assert.Contains("Config Directory", text);
        Assert.DoesNotContain("winget install", text);
        Assert.DoesNotContain("sudo apt", text);
    }
}
