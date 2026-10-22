using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Claude.Platform;

/// <summary>
/// Starts the Claude Code CLI in the user's default terminal, in the folder of the active file
/// manager window (Explorer on Windows, Finder on macOS). Linux has no common way to ask the file
/// manager for its folder, so it, and any failed lookup, falls back to the home folder.
/// </summary>
internal static class ClaudeCli
{
    private const string CliCommand = "claude";

    /// <summary>Folder the CLI should start in: the active file manager window's, else home.</summary>
    public static string ResolveFolder(IPluginLogger logger)
    {
        string? folder = null;
        try
        {
            if (OperatingSystem.IsWindows()) folder = ExplorerFolder();
            else if (OperatingSystem.IsMacOS()) folder = FinderFolder();
        }
        catch (Exception ex)
        {
            logger.Warn($"Could not read the active file manager folder: {ex.Message}");
        }

        return folder is not null && Directory.Exists(folder)
            ? folder
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    /// <summary>Opens a terminal running the CLI in <paramref name="folder"/>. Returns false when no terminal could be started.</summary>
    public static bool Launch(IPluginLogger logger, string folder)
    {
        try
        {
            if (OperatingSystem.IsWindows()) return LaunchWindows(folder);
            if (OperatingSystem.IsMacOS()) return LaunchMac(folder);
            return LaunchLinux(logger, folder);
        }
        catch (Exception ex)
        {
            logger.Error("Starting the Claude Code CLI failed", ex);
            return false;
        }
    }

    // ---- Windows ----

    /// <summary>
    /// A new console started from a GUI process opens in the terminal chosen under Windows
    /// "Default terminal application" (Windows Terminal or the console host). <c>cmd /k</c> keeps the
    /// window open after <c>claude</c> exits and resolves both <c>claude.exe</c> and npm's <c>claude.cmd</c>.
    /// </summary>
    private static bool LaunchWindows(string folder)
    {
        using Process? p = Process.Start(new ProcessStartInfo("cmd.exe", "/k " + CliCommand)
        {
            UseShellExecute = true,
            WorkingDirectory = folder
        });
        return p is not null;
    }

    /// <summary>
    /// The topmost Explorer window and, on Windows 11, its selected tab. Shell COM needs an STA
    /// thread, so the lookup runs on its own and gives up after a few seconds.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static string? ExplorerFolder()
    {
        string? result = null;
        Thread thread = new(() => result = ExplorerFolderSta()) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return thread.Join(TimeSpan.FromSeconds(3)) ? result : null;
    }

    [SupportedOSPlatform("windows")]
    private static string? ExplorerFolderSta()
    {
        // FindWindowEx walks top-level windows in z-order, so the first visible hit is the most recent one.
        IntPtr window = IntPtr.Zero;
        do
        {
            window = FindWindowEx(IntPtr.Zero, window, "CabinetWClass", null);
        } while (window != IntPtr.Zero && !IsWindowVisible(window));

        if (window == IntPtr.Zero) return null;

        // The selected tab is the first tab child; older Explorer versions have exactly one.
        IntPtr tab = FindWindowEx(window, IntPtr.Zero, "ShellTabWindowClass", null);

        Type? shellType = Type.GetTypeFromProgID("Shell.Application");
        if (shellType is null) return null;

        dynamic shell = Activator.CreateInstance(shellType)!;
        try
        {
            dynamic windows = shell.Windows();
            int count = windows.Count;
            for (int i = 0; i < count; i++)
            {
                try
                {
                    dynamic? item = windows.Item(i);
                    if (item is null || new IntPtr((long)item.HWND) != window) continue;
                    if (tab != IntPtr.Zero && TabWindow(item) != tab) continue;

                    return (string)item.Document.Folder.Self.Path;
                }
                catch (Exception)
                {
                    // not a folder view (or the window closed meanwhile); try the next one
                }
            }
        }
        finally
        {
            Marshal.FinalReleaseComObject(shell);
        }

        return null;
    }

    [SupportedOSPlatform("windows")]
    private static IntPtr TabWindow(object browser)
    {
        if (browser is not IServiceProvider provider) return IntPtr.Zero;

        Guid service = TopLevelBrowserService;
        Guid riid = typeof(IShellBrowser).GUID;
        if (provider.QueryService(ref service, ref riid, out IntPtr ptr) != 0 || ptr == IntPtr.Zero) return IntPtr.Zero;

        try
        {
            IShellBrowser shellBrowser = (IShellBrowser)Marshal.GetObjectForIUnknown(ptr);
            return shellBrowser.GetWindow(out IntPtr hwnd) == 0 ? hwnd : IntPtr.Zero;
        }
        finally
        {
            Marshal.Release(ptr);
        }
    }

    /// <summary>SID_STopLevelBrowser.</summary>
    private static readonly Guid TopLevelBrowserService = new("4C96BE40-915C-11CF-99D3-00AA004AE837");

    [ComImport, Guid("6D5140C1-7436-11CE-8034-00AA006009FA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IServiceProvider
    {
        [PreserveSig]
        int QueryService(ref Guid guidService, ref Guid riid, out IntPtr ppvObject);
    }

    /// <summary>Only IOleWindow::GetWindow, the first slot of IShellBrowser's vtable, is used.</summary>
    [ComImport, Guid("000214E2-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellBrowser
    {
        [PreserveSig]
        int GetWindow(out IntPtr phwnd);
    }

    [DllImport("user32.dll", EntryPoint = "FindWindowExW", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string? className, string? windowName);

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    // ---- macOS ----

    private static string? FinderFolder() =>
        ClaudeApp.Capture("osascript",
            "-e", "tell application \"Finder\"",
            "-e", "if (count of Finder windows) is 0 then return \"\"",
            "-e", "return POSIX path of (target of front Finder window as alias)",
            "-e", "end tell") is { Length: > 0 } path ? path : null;

    /// <summary>Terminal.app is the macOS default; the folder is passed as argv so it needs no escaping.</summary>
    private static bool LaunchMac(string folder)
    {
        ClaudeApp.Run("osascript",
            "-e", "on run argv",
            "-e", "tell application \"Terminal\"",
            "-e", "activate",
            "-e", $"do script \"cd \" & quoted form of (item 1 of argv) & \" && {CliCommand}\"",
            "-e", "end tell",
            "-e", "end run",
            folder);
        return true;
    }

    // ---- Linux ----

    /// <summary>
    /// $TERMINAL first, then the desktop's own terminal, then Debian's x-terminal-emulator and common
    /// fallbacks. The user's login shell runs the CLI so ~/.local/bin is on PATH, then stays open.
    /// </summary>
    private static bool LaunchLinux(IPluginLogger logger, string folder)
    {
        string[] shell = ["sh", "-c", $"\"${{SHELL:-/bin/sh}}\" -lc '{CliCommand}'; exec \"${{SHELL:-/bin/sh}}\" -l"];

        foreach (string terminal in LinuxTerminals())
        {
            string? path = FindOnPath(terminal);
            if (path is null) continue;

            ProcessStartInfo psi = new(path) { UseShellExecute = false, WorkingDirectory = folder };
            foreach (string arg in TerminalArgs(Path.GetFileName(path), folder, shell)) psi.ArgumentList.Add(arg);

            using Process? p = Process.Start(psi);
            if (p is not null) return true;
        }

        logger.Warn("No terminal emulator found for the Claude Code CLI");
        return false;
    }

    private static IEnumerable<string> LinuxTerminals()
    {
        if (Environment.GetEnvironmentVariable("TERMINAL") is { Length: > 0 } preferred) yield return preferred;

        string desktop = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? string.Empty;
        if (desktop.Contains("KDE", StringComparison.OrdinalIgnoreCase)) yield return "konsole";
        if (desktop.Contains("GNOME", StringComparison.OrdinalIgnoreCase))
        {
            yield return "kgx";
            yield return "gnome-terminal";
        }

        if (desktop.Contains("XFCE", StringComparison.OrdinalIgnoreCase)) yield return "xfce4-terminal";

        yield return "x-terminal-emulator";
        foreach (string fallback in (string[])["konsole", "gnome-terminal", "xfce4-terminal", "kitty", "alacritty", "foot", "xterm"])
        {
            yield return fallback;
        }
    }

    private static IEnumerable<string> TerminalArgs(string terminal, string folder, string[] command) => terminal switch
    {
        "gnome-terminal" or "kgx" => ["--working-directory=" + folder, "--", .. command],
        "konsole" => ["--workdir", folder, "-e", .. command],
        "xfce4-terminal" => ["--working-directory=" + folder, "-x", .. command],
        "kitty" => ["--directory", folder, .. command],
        "alacritty" => ["--working-directory", folder, "-e", .. command],
        "foot" => ["--working-directory=" + folder, .. command],
        _ => ["-e", .. command]
    };

    private static string? FindOnPath(string name)
    {
        if (Path.IsPathRooted(name)) return File.Exists(name) ? name : null;

        string[] dirs = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        return dirs.Select(dir => Path.Combine(dir, name)).FirstOrDefault(File.Exists);
    }
}
