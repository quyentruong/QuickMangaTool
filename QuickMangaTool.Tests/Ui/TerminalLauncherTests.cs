namespace QuickMangaTool.Tests.Ui;

/// <summary>
/// Unit tests for <see cref="TerminalLauncher"/>.
/// Verifies interactive candidate detection, terminal relaunch conditions,
/// desktop-aware terminal ordering, argument construction, and working directory resolution.
/// </summary>
public class TerminalLauncherTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _fakeExePath;

    public TerminalLauncherTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "qmt-terminal-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _fakeExePath = Path.Combine(_tempDir, "QuickMangaTool");
        File.WriteAllText(_fakeExePath, "binary-placeholder");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // Best-effort test cleanup
        }
    }

    // ── IsInteractiveCandidate ────────────────────────────────────────────

    [Theory]
    [InlineData(new string[0], true)]
    [InlineData(new[] { "--interactive" }, true)]
    [InlineData(new[] { "-i" }, true)]
    [InlineData(new[] { "/home/user/manga" }, true)]
    [InlineData(new[] { "/home/user/manga", "-i" }, true)]
    [InlineData(new[] { "convert" }, false)]
    [InlineData(new[] { "update" }, false)]
    [InlineData(new[] { "/home/user/manga", "convert" }, false)]
    [InlineData(new[] { "--help" }, false)]
    [InlineData(new[] { "-h" }, false)]
    [InlineData(new[] { "-w" }, false)]
    public void IsInteractiveCandidate_MatchesExpectedArgs(string[] args, bool expected)
    {
        Assert.Equal(expected, TerminalLauncher.IsInteractiveCandidate(args));
    }

    // ── ShouldAttemptTerminalRelaunch ─────────────────────────────────────

    [Fact]
    public void ShouldAttemptTerminalRelaunch_LinuxDoubleClickWithDisplay_ReturnsTrue()
    {
        var result = TerminalLauncher.ShouldAttemptTerminalRelaunch(
            args: Array.Empty<string>(),
            isInputRedirected: true,
            isOutputRedirected: true,
            isWindows: false,
            processPath: _fakeExePath,
            relaunchGuardValue: null,
            displayValue: ":0",
            waylandDisplayValue: null);

        Assert.True(result);
    }

    [Fact]
    public void ShouldAttemptTerminalRelaunch_WaylandOnly_ReturnsTrue()
    {
        var result = TerminalLauncher.ShouldAttemptTerminalRelaunch(
            args: Array.Empty<string>(),
            isInputRedirected: true,
            isOutputRedirected: false,
            isWindows: false,
            processPath: _fakeExePath,
            relaunchGuardValue: null,
            displayValue: null,
            waylandDisplayValue: "wayland-1");

        Assert.True(result);
    }

    [Fact]
    public void ShouldAttemptTerminalRelaunch_OnWindows_ReturnsFalse()
    {
        var result = TerminalLauncher.ShouldAttemptTerminalRelaunch(
            args: Array.Empty<string>(),
            isInputRedirected: true,
            isOutputRedirected: true,
            isWindows: true,
            processPath: _fakeExePath,
            relaunchGuardValue: null,
            displayValue: ":0",
            waylandDisplayValue: null);

        Assert.False(result);
    }

    [Fact]
    public void ShouldAttemptTerminalRelaunch_WhenAlreadyAttachedToTty_ReturnsFalse()
    {
        var result = TerminalLauncher.ShouldAttemptTerminalRelaunch(
            args: Array.Empty<string>(),
            isInputRedirected: false,
            isOutputRedirected: false,
            isWindows: false,
            processPath: _fakeExePath,
            relaunchGuardValue: null,
            displayValue: ":0",
            waylandDisplayValue: "wayland-1");

        Assert.False(result);
    }

    [Fact]
    public void ShouldAttemptTerminalRelaunch_WhenGuardEnvSet_ReturnsFalse()
    {
        var result = TerminalLauncher.ShouldAttemptTerminalRelaunch(
            args: new[] { "--interactive" },
            isInputRedirected: true,
            isOutputRedirected: true,
            isWindows: false,
            processPath: _fakeExePath,
            relaunchGuardValue: "1",
            displayValue: ":0",
            waylandDisplayValue: null);

        Assert.False(result);
    }

    [Fact]
    public void ShouldAttemptTerminalRelaunch_WithoutDisplayOrWayland_ReturnsFalse()
    {
        var result = TerminalLauncher.ShouldAttemptTerminalRelaunch(
            args: Array.Empty<string>(),
            isInputRedirected: true,
            isOutputRedirected: true,
            isWindows: false,
            processPath: _fakeExePath,
            relaunchGuardValue: null,
            displayValue: null,
            waylandDisplayValue: "  ");

        Assert.False(result);
    }

    [Fact]
    public void ShouldAttemptTerminalRelaunch_WhenRunningViaDotnetHost_ReturnsFalse()
    {
        var dotnetPath = Path.Combine(_tempDir, "dotnet");
        File.WriteAllText(dotnetPath, "dotnet-host");

        var result = TerminalLauncher.ShouldAttemptTerminalRelaunch(
            args: Array.Empty<string>(),
            isInputRedirected: true,
            isOutputRedirected: true,
            isWindows: false,
            processPath: dotnetPath,
            relaunchGuardValue: null,
            displayValue: ":0",
            waylandDisplayValue: null);

        Assert.False(result);
    }

    [Fact]
    public void ShouldAttemptTerminalRelaunch_WithCliSubcommandOrHelp_ReturnsFalse()
    {
        Assert.False(TerminalLauncher.ShouldAttemptTerminalRelaunch(
            new[] { "convert" }, true, true, false, _fakeExePath, null, ":0", null));
        Assert.False(TerminalLauncher.ShouldAttemptTerminalRelaunch(
            new[] { "--help" }, true, true, false, _fakeExePath, null, ":0", null));
    }

    // ── GetCandidateTerminals ─────────────────────────────────────────────

    [Fact]
    public void GetCandidateTerminals_KdeDesktop_PrioritizesKonsole()
    {
        var candidates = TerminalLauncher.GetCandidateTerminals(null, "KDE");

        Assert.Equal("konsole", candidates[0]);
        Assert.Single(candidates, c => c == "konsole");
    }

    [Fact]
    public void GetCandidateTerminals_GnomeDesktop_PrioritizesGnomeTerminals()
    {
        var candidates = TerminalLauncher.GetCandidateTerminals(null, "ubuntu:GNOME");

        Assert.Equal(new[] { "ptyxis", "kgx", "gnome-terminal" }, candidates.Take(3).ToArray());
    }

    [Fact]
    public void GetCandidateTerminals_XfceDesktop_PrioritizesXfce4Terminal()
    {
        var candidates = TerminalLauncher.GetCandidateTerminals(null, "XFCE");

        Assert.Equal("xfce4-terminal", candidates[0]);
        Assert.Single(candidates, c => c == "xfce4-terminal");
    }

    [Fact]
    public void GetCandidateTerminals_CustomTerminalEnv_TakesTopPriorityAndDeduplicates()
    {
        var candidates = TerminalLauncher.GetCandidateTerminals("alacritty", "KDE");

        Assert.Equal("alacritty", candidates[0]);
        Assert.Equal("konsole", candidates[1]);
        Assert.Single(candidates, c => c == "alacritty");
    }

    // ── BuildTerminalArguments ────────────────────────────────────────────

    [Fact]
    public void BuildTerminalArguments_Konsole_BuildsExpectedArgs()
    {
        var args = TerminalLauncher.BuildTerminalArguments(
            "/usr/bin/konsole",
            "/opt/QuickMangaTool",
            "/manga/library",
            Array.Empty<string>());

        Assert.Equal(
            new[] { "--workdir", "/manga/library", "-e", "/opt/QuickMangaTool", "--interactive" },
            args.ToArray());
    }

    [Theory]
    [InlineData("ptyxis")]
    [InlineData("kgx")]
    [InlineData("gnome-terminal")]
    public void BuildTerminalArguments_GnomeFamily_BuildsExpectedArgs(string terminal)
    {
        var args = TerminalLauncher.BuildTerminalArguments(
            terminal,
            "/opt/QuickMangaTool",
            "/manga/library",
            new[] { "/manga/library" });

        Assert.Equal(
            new[] { "--working-directory", "/manga/library", "--", "/opt/QuickMangaTool", "--interactive", "/manga/library" },
            args.ToArray());
    }

    [Theory]
    [InlineData("xfce4-terminal")]
    [InlineData("mate-terminal")]
    [InlineData("terminator")]
    public void BuildTerminalArguments_XfceMateTerminator_BuildsExpectedArgs(string terminal)
    {
        var args = TerminalLauncher.BuildTerminalArguments(
            terminal,
            "/opt/QuickMangaTool",
            "/manga/library",
            Array.Empty<string>());

        Assert.Equal(
            new[] { "--working-directory", "/manga/library", "-x", "/opt/QuickMangaTool", "--interactive" },
            args.ToArray());
    }

    [Fact]
    public void BuildTerminalArguments_Kitty_BuildsExpectedArgs()
    {
        var args = TerminalLauncher.BuildTerminalArguments(
            "kitty",
            "/opt/QuickMangaTool",
            "/manga/library",
            Array.Empty<string>());

        Assert.Equal(
            new[] { "--directory", "/manga/library", "/opt/QuickMangaTool", "--interactive" },
            args.ToArray());
    }

    [Fact]
    public void BuildTerminalArguments_Alacritty_BuildsExpectedArgs()
    {
        var args = TerminalLauncher.BuildTerminalArguments(
            "alacritty",
            "/opt/QuickMangaTool",
            "/manga/library",
            Array.Empty<string>());

        Assert.Equal(
            new[] { "--working-directory", "/manga/library", "-e", "/opt/QuickMangaTool", "--interactive" },
            args.ToArray());
    }

    [Fact]
    public void BuildTerminalArguments_Wezterm_BuildsExpectedArgs()
    {
        var args = TerminalLauncher.BuildTerminalArguments(
            "wezterm",
            "/opt/QuickMangaTool",
            "/manga/library",
            Array.Empty<string>());

        Assert.Equal(
            new[] { "start", "--cwd", "/manga/library", "--", "/opt/QuickMangaTool", "--interactive" },
            args.ToArray());
    }

    [Theory]
    [InlineData("xdg-terminal-exec")]
    [InlineData("foot")]
    public void BuildTerminalArguments_DirectCommandTerminals_BuildsExpectedArgs(string terminal)
    {
        var args = TerminalLauncher.BuildTerminalArguments(
            terminal,
            "/opt/QuickMangaTool",
            "/manga/library",
            Array.Empty<string>());

        Assert.Equal(
            new[] { "/opt/QuickMangaTool", "--interactive" },
            args.ToArray());
    }

    [Fact]
    public void BuildTerminalArguments_Xterm_UsesDashE()
    {
        var args = TerminalLauncher.BuildTerminalArguments(
            "xterm",
            "/opt/QuickMangaTool",
            "/manga/library",
            Array.Empty<string>());

        Assert.Equal(
            new[] { "-e", "/opt/QuickMangaTool", "--interactive" },
            args.ToArray());
    }

    // ── ResolveExecutableInPath ───────────────────────────────────────────

    [Fact]
    public void ResolveExecutableInPath_FindsBinaryInCustomPath()
    {
        var binDir = Path.Combine(_tempDir, "bin");
        Directory.CreateDirectory(binDir);
        var termPath = Path.Combine(binDir, "my-term");
        File.WriteAllText(termPath, "#!/bin/sh");

        var resolved = TerminalLauncher.ResolveExecutableInPath("my-term", $"/nonexistent{Path.PathSeparator}{binDir}");

        Assert.Equal(termPath, resolved);
        Assert.Null(TerminalLauncher.ResolveExecutableInPath("missing-term", binDir));
    }

    [Fact]
    public void ResolveExecutableInPath_WithExplicitPath_ReturnsFullPathIfExists()
    {
        var resolved = TerminalLauncher.ResolveExecutableInPath(_fakeExePath, string.Empty);

        Assert.Equal(Path.GetFullPath(_fakeExePath), resolved);
    }

    // ── ResolveDefaultWorkingDirectory ────────────────────────────────────

    [Fact]
    public void ResolveDefaultWorkingDirectory_WhenCwdIsHomeAndExeInUserFolder_ReturnsExeDirectory()
    {
        var homeDir = Path.Combine(_tempDir, "home");
        var mangaFolder = Path.Combine(_tempDir, "manga-folder");
        Directory.CreateDirectory(homeDir);
        Directory.CreateDirectory(mangaFolder);
        var exeInFolder = Path.Combine(mangaFolder, "QuickMangaTool");
        File.WriteAllText(exeInFolder, "binary");

        var resolved = TerminalLauncher.ResolveDefaultWorkingDirectory(homeDir, exeInFolder, homeDir);

        Assert.Equal(Path.GetFullPath(mangaFolder), resolved);
    }

    [Fact]
    public void ResolveDefaultWorkingDirectory_WhenCwdIsCustomDirectory_PreservesCurrentDirectory()
    {
        var homeDir = Path.Combine(_tempDir, "home");
        var customDir = Path.Combine(_tempDir, "custom-manga");
        var exeFolder = Path.Combine(_tempDir, "tool-dir");
        Directory.CreateDirectory(homeDir);
        Directory.CreateDirectory(customDir);
        Directory.CreateDirectory(exeFolder);
        var exeInFolder = Path.Combine(exeFolder, "QuickMangaTool");
        File.WriteAllText(exeInFolder, "binary");

        var resolved = TerminalLauncher.ResolveDefaultWorkingDirectory(customDir, exeInFolder, homeDir);

        Assert.Equal(customDir, resolved);
    }

    [Fact]
    public void ResolveDefaultWorkingDirectory_WhenExeInUserLocalBin_PreservesHomeDirectory()
    {
        var homeDir = Path.Combine(_tempDir, "home");
        var localBin = Path.Combine(homeDir, ".local", "bin");
        Directory.CreateDirectory(localBin);
        var exeInLocalBin = Path.Combine(localBin, "QuickMangaTool");
        File.WriteAllText(exeInLocalBin, "binary");

        var resolved = TerminalLauncher.ResolveDefaultWorkingDirectory(homeDir, exeInLocalBin, homeDir);

        Assert.Equal(homeDir, resolved);
    }

    [Fact]
    public void ResolveDefaultWorkingDirectory_WhenExeInSystemBin_PreservesCurrentDirectory()
    {
        var homeDir = Path.Combine(_tempDir, "home");
        Directory.CreateDirectory(homeDir);

        // Even if /usr/bin/env exists, a system bin directory must not override homeDir
        var systemBinExe = "/usr/bin/env";
        if (File.Exists(systemBinExe))
        {
            var resolved = TerminalLauncher.ResolveDefaultWorkingDirectory(homeDir, systemBinExe, homeDir);
            Assert.Equal(homeDir, resolved);
        }
    }
}
