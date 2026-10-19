using System.Diagnostics;
using LoupixDeck.PluginSdk;
using Microsoft.Win32;

namespace LoupixDeck.Plugin.Claude.Platform;

/// <summary>
/// What this machine has of Claude: the desktop app (macOS / Windows) with its <c>claude://</c>
/// deep links, or only the web app. Everything the launcher commands do funnels through here so
/// the per-OS branches live in one place.
/// </summary>
internal static class ClaudeApp
{
    /// <summary>Deep-link paths the desktop app handles (documented ones plus the app's own Dock actions).</summary>
    public const string NewChatPath = "claude.ai/new";
    public const string NewCodeSessionPath = "code/new";
    public const string ContinueLastCodePath = "code/continue?session=last";
    public const string CodeNeedsInputPath = "code/needs-input";

    /// <summary>
    /// App pages opened through the desktop app's claude.ai route (any claude.ai path loads in the
    /// app). Scheduled tasks live under the app's own Claude Code path, /epitaxy; claude.ai on the
    /// web redirects that to /code. Design is the sidebar's Artifacts view filtered to Claude Design;
    /// plain /design is the standalone homepage, which the app does not route (it lands on Routines).
    /// </summary>
    public const string DesignPath = "claude.ai/artifacts/design";
    public const string CustomizePath = "claude.ai/customize";
    public const string ScheduledPath = "claude.ai/epitaxy/scheduled";

    private static readonly Lazy<bool> DesktopInstalled = new(DetectDesktop);

    public static bool IsDesktopInstalled => DesktopInstalled.Value;

    /// <summary>The key-combination string the host's macro engine understands for Quick entry on this OS.</summary>
    public static string DefaultQuickEntryShortcut =>
        OperatingSystem.IsMacOS() ? "Alt+Space" : "Ctrl+Alt+Space";

    /// <summary>
    /// Opens a Claude deep link in the desktop app, or the nearest web page when the app is not
    /// installed. Returns false when nothing could be launched.
    /// </summary>
    public static bool OpenLink(IPluginHost host, string path)
    {
        var url = IsDesktopInstalled ? "claude://" + path : WebFallback(path);
        if (url is null)
        {
            host.Logger.Warn($"Claude desktop app not installed and '{path}' has no web equivalent");
            return false;
        }

        return host.OpenBrowser(url);
    }

    private static string? WebFallback(string path)
    {
        if (path.StartsWith("claude.ai/", StringComparison.Ordinal)) return "https://" + path;
        if (path.StartsWith("code/", StringComparison.Ordinal)) return "https://claude.ai/code";
        return null;
    }

    private static bool DetectDesktop()
    {
        try
        {
            if (OperatingSystem.IsMacOS())
            {
                return Directory.Exists("/Applications/Claude.app")
                       || Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Applications", "Claude.app"));
            }

            if (OperatingSystem.IsWindows())
            {
                // The Store / MSIX build lives under WindowsApps, which is not listable, so the
                // registered claude:// handler is the signal; the folder checks cover old installers.
                using var protocol = Registry.ClassesRoot.OpenSubKey("claude");
                if (protocol?.GetValue("URL Protocol") is not null) return true;

                var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Directory.Exists(Path.Combine(local, "AnthropicClaude"))
                       || File.Exists(Path.Combine(local, "Programs", "Claude", "Claude.exe"));
            }
        }
        catch
        {
            // detection is best effort; fall through to "web only"
        }

        return false;
    }

    internal static void Run(string file, params string[] args)
    {
        var psi = new ProcessStartInfo(file) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi);
        p?.WaitForExit(3000);
    }

    /// <summary>Runs a command and returns its stdout (trimmed), or null on failure / timeout.</summary>
    internal static string? Capture(string file, params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo(file)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var a in args) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi);
            if (p is null) return null;
            var output = p.StandardOutput.ReadToEnd();
            if (!p.WaitForExit(5000)) { try { p.Kill(); } catch { } return null; }
            return p.ExitCode == 0 ? output.Trim() : null;
        }
        catch
        {
            return null;
        }
    }
}
