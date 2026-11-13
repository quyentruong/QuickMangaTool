using System.Diagnostics;

/// <summary>
/// Detects when the application is launched without an attached terminal (e.g. double-clicked
/// in a Linux file manager) and relaunches it inside an available terminal emulator.
/// </summary>
static class TerminalLauncher
{
    public const string RelaunchGuardEnvVar = "QUICKMANGATOOL_TERMINAL_RELAUNCH";

    private static readonly string[] FallbackTerminals =
    [
        "xdg-terminal-exec",
        "x-terminal-emulator",
        "konsole",
        "ptyxis",
        "kgx",
        "gnome-terminal",
        "xfce4-terminal",
        "kitty",
        "alacritty",
        "wezterm",
        "ghostty",
        "foot",
        "tilix",
        "mate-terminal",
        "terminator",
        "qterminal",
        "lxterminal",
        "cosmic-term",
        "xterm",
        "urxvt"
    ];

    public static bool IsInteractiveCandidate(string[]? args)
    {
        if (args is null || args.Length == 0)
            return true;

        if (args.Contains("--interactive") || args.Contains("-i"))
            return true;

        return args.Length == 1 && !args[0].StartsWith('-') && !OptionsParser.IsKnownSubcommand(args[0]);
    }

    public static bool ShouldAttemptTerminalRelaunch(
        string[]? args,
        bool isInputRedirected,
        bool isOutputRedirected,
        bool isWindows,
        string? processPath,
        string? relaunchGuardValue,
        string? displayValue,
        string? waylandDisplayValue)
    {
        if (isWindows)
            return false;

        if (!isInputRedirected && !isOutputRedirected)
            return false;

        if (!IsInteractiveCandidate(args))
            return false;

        if (string.Equals(relaunchGuardValue, "1", StringComparison.Ordinal))
            return false;

        if (string.IsNullOrWhiteSpace(displayValue) && string.IsNullOrWhiteSpace(waylandDisplayValue))
            return false;

        if (!UpdateCommand.IsValidTargetExecutable(processPath, out _))
            return false;

        return true;
    }

    public static IReadOnlyList<string> GetCandidateTerminals(string? terminalEnv, string? currentDesktop)
    {
        var candidates = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void AddCandidate(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;

            var trimmed = name.Trim();
            if (seen.Add(trimmed))
                candidates.Add(trimmed);
        }

        AddCandidate(terminalEnv);

        if (!string.IsNullOrWhiteSpace(currentDesktop))
        {
            if (currentDesktop.Contains("kde", StringComparison.OrdinalIgnoreCase) ||
                currentDesktop.Contains("plasma", StringComparison.OrdinalIgnoreCase))
            {
                AddCandidate("konsole");
            }

            if (currentDesktop.Contains("gnome", StringComparison.OrdinalIgnoreCase) ||
                currentDesktop.Contains("ubuntu", StringComparison.OrdinalIgnoreCase))
            {
                AddCandidate("ptyxis");
                AddCandidate("kgx");
                AddCandidate("gnome-terminal");
            }

            if (currentDesktop.Contains("xfce", StringComparison.OrdinalIgnoreCase))
            {
                AddCandidate("xfce4-terminal");
            }

            if (currentDesktop.Contains("mate", StringComparison.OrdinalIgnoreCase))
            {
                AddCandidate("mate-terminal");
            }

            if (currentDesktop.Contains("lxqt", StringComparison.OrdinalIgnoreCase))
            {
                AddCandidate("qterminal");
            }

            if (currentDesktop.Contains("lxde", StringComparison.OrdinalIgnoreCase))
            {
                AddCandidate("lxterminal");
            }

            if (currentDesktop.Contains("cosmic", StringComparison.OrdinalIgnoreCase))
            {
                AddCandidate("cosmic-term");
            }
        }

        foreach (var fallback in FallbackTerminals)
        {
            AddCandidate(fallback);
        }

        return candidates;
    }

    public static IReadOnlyList<string> BuildTerminalArguments(
        string terminalExecutable,
        string targetExecutablePath,
        string workingDirectory,
        IReadOnlyList<string>? appArgs)
    {
        var terminalName = Path.GetFileNameWithoutExtension(terminalExecutable).ToLowerInvariant();
        var targetCmd = new List<string> { targetExecutablePath, "--interactive" };

        var positionalDir = appArgs?.FirstOrDefault(a => !string.IsNullOrWhiteSpace(a) && !a.StartsWith('-'));
        if (!string.IsNullOrWhiteSpace(positionalDir))
        {
            targetCmd.Add(positionalDir);
        }

        return terminalName switch
        {
            "konsole" => ["--workdir", workingDirectory, "-e", .. targetCmd],
            "ptyxis" or "kgx" or "gnome-terminal" => ["--working-directory", workingDirectory, "--", .. targetCmd],
            "xfce4-terminal" or "mate-terminal" or "terminator" => ["--working-directory", workingDirectory, "-x", .. targetCmd],
            "kitty" => ["--directory", workingDirectory, .. targetCmd],
            "alacritty" or "tilix" => ["--working-directory", workingDirectory, "-e", .. targetCmd],
            "wezterm" => ["start", "--cwd", workingDirectory, "--", .. targetCmd],
            "foot" or "xdg-terminal-exec" => [.. targetCmd],
            _ => ["-e", .. targetCmd]
        };
    }

    public static string? ResolveExecutableInPath(string command, string? pathEnv = null)
    {
        if (string.IsNullOrWhiteSpace(command))
            return null;

        var trimmed = command.Trim();
        if (trimmed.Contains(Path.DirectorySeparatorChar) || trimmed.Contains(Path.AltDirectorySeparatorChar))
        {
            return File.Exists(trimmed) ? Path.GetFullPath(trimmed) : null;
        }

        var effectivePath = pathEnv ?? Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(effectivePath))
            return null;

        foreach (var dir in effectivePath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (string.IsNullOrWhiteSpace(dir))
                continue;

            var candidate = Path.Combine(dir, trimmed);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    public static string ResolveDefaultWorkingDirectory(string currentDirectory, string? processPath, string? homeDirectory)
    {
        if (!string.IsNullOrWhiteSpace(processPath) && UpdateCommand.IsValidTargetExecutable(processPath, out _))
        {
            var exeDir = Path.GetDirectoryName(Path.GetFullPath(processPath));
            if (!string.IsNullOrWhiteSpace(exeDir) && Directory.Exists(exeDir) && !IsSystemBinDirectory(exeDir, homeDirectory))
            {
                if (string.IsNullOrWhiteSpace(currentDirectory) ||
                    !Directory.Exists(currentDirectory) ||
                    PathsEqual(currentDirectory, homeDirectory))
                {
                    return exeDir;
                }
            }
        }

        return currentDirectory;
    }

    public static bool TryRelaunchInTerminal(string[] args)
    {
        var processPath = Environment.ProcessPath;
        if (!ShouldAttemptTerminalRelaunch(
            args,
            Console.IsInputRedirected,
            Console.IsOutputRedirected,
            OperatingSystem.IsWindows(),
            processPath,
            Environment.GetEnvironmentVariable(RelaunchGuardEnvVar),
            Environment.GetEnvironmentVariable("DISPLAY"),
            Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
        {
            return false;
        }

        var positionalDir = args.FirstOrDefault(a => !string.IsNullOrWhiteSpace(a) && !a.StartsWith('-'));
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var workingDir = !string.IsNullOrWhiteSpace(positionalDir) && Directory.Exists(positionalDir)
            ? Path.GetFullPath(positionalDir)
            : (LastLocationStore.GetLastDirectory() ?? ResolveDefaultWorkingDirectory(
                Directory.GetCurrentDirectory(),
                processPath,
                homeDir));

        var currentDesktop = $"{Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP")} {Environment.GetEnvironmentVariable("DESKTOP_SESSION")}".Trim();
        var candidates = GetCandidateTerminals(Environment.GetEnvironmentVariable("TERMINAL"), currentDesktop);

        foreach (var candidate in candidates)
        {
            var resolvedTerminal = ResolveExecutableInPath(candidate);
            if (resolvedTerminal is null)
                continue;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = resolvedTerminal,
                    UseShellExecute = false,
                    WorkingDirectory = workingDir
                };

                foreach (var arg in BuildTerminalArguments(resolvedTerminal, processPath!, workingDir, args))
                {
                    psi.ArgumentList.Add(arg);
                }

                psi.Environment[RelaunchGuardEnvVar] = "1";

                using var process = Process.Start(psi);
                if (process is not null)
                    return true;
            }
            catch
            {
                // Try the next terminal candidate if launching this one fails
            }
        }

        return false;
    }

    private static bool IsSystemBinDirectory(string dir, string? homeDir)
    {
        var normalized = NormalizeDirectoryPath(dir);
        if (string.IsNullOrEmpty(normalized))
            return false;

        string[] systemDirs = ["/usr/bin", "/usr/local/bin", "/bin", "/sbin", "/usr/sbin"];
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        foreach (var systemDir in systemDirs)
        {
            if (string.Equals(normalized, systemDir, comparison))
                return true;
        }

        if (!string.IsNullOrWhiteSpace(homeDir))
        {
            var userLocalBin = NormalizeDirectoryPath(Path.Combine(homeDir, ".local", "bin"));
            var userBin = NormalizeDirectoryPath(Path.Combine(homeDir, "bin"));
            if (string.Equals(normalized, userLocalBin, comparison) ||
                string.Equals(normalized, userBin, comparison))
            {
                return true;
            }
        }

        return false;
    }

    private static bool PathsEqual(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;

        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return string.Equals(NormalizeDirectoryPath(left), NormalizeDirectoryPath(right), comparison);
    }

    private static string NormalizeDirectoryPath(string path)
    {
        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        }
        catch
        {
            return Path.TrimEndingDirectorySeparator(path);
        }
    }
}
