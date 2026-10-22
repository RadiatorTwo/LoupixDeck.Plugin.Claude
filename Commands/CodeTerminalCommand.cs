using LoupixDeck.Plugin.Claude.Platform;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Claude.Commands;

/// <summary>Starts the Claude Code CLI in the default terminal, in the active file manager window's folder.</summary>
internal sealed class CodeTerminalCommand(ClaudePlugin plugin) : LauncherCommandBase(plugin)
{
    public const string Name = "Claude.CodeTerminal";

    public override CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = Name,
        DisplayName = "Claude CLI",
        Group = ClaudePlugin.GroupName,
        Icon = "\U000F018D", // mdi-console
        Description = "Open the Claude Code CLI in the default terminal, in the folder of the active file manager window.",
        ButtonLayout = Layout("Claude CLI")
    };

    // The folder lookup talks to the file manager and can take a moment, so keep it off the caller's thread.
    public override Task Execute(CommandContext ctx) => GuardAsync(ctx, "terminal", () => Task.Run(() =>
    {
        string folder = ClaudeCli.ResolveFolder(ctx.Host.Logger);
        if (!ClaudeCli.Launch(ctx.Host.Logger, folder)) Hint(ctx, "No terminal");
    }));
}
