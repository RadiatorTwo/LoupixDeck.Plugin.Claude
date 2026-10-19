using LoupixDeck.Plugin.Claude.Rendering;
using LoupixDeck.Plugin.Claude.Status;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Claude.Commands;

/// <summary>
/// A status key for Claude as a whole or for one service (claude.ai, the API, Claude Code…), as
/// status.claude.com reports it. Its button state follows the status (operational, degraded,
/// outage, maintenance, unknown) and each state brings its own editable layers and colour.
/// Every service has its own command id rather than a parameter, because the host switches the
/// state of all keys that share a command name. Tap to open the status page.
/// </summary>
internal sealed class StatusCommand : ClaudeCommandBase, IValueDisplayCommand
{
    public const string OverallName = "Claude.Status";
    public const string ClaudeAiName = "Claude.Status.ClaudeAi";
    public const string ApiName = "Claude.Status.Api";
    public const string CodeName = "Claude.Status.Code";
    public const string ConsoleName = "Claude.Status.Console";
    public const string CoworkName = "Claude.Status.Cowork";

    public const string OperationalState = "operational";
    public const string DegradedState = "degraded";
    public const string OutageState = "outage";
    public const string MaintenanceState = "maintenance";
    public const string UnknownState = "unknown";

    public static readonly IReadOnlyList<string> Names = [OverallName, ClaudeAiName, ApiName, CodeName, ConsoleName, CoworkName];

    private readonly string _name;

    /// <summary>The start of the service's name on the status page; null for the overall status.</summary>
    private readonly string? _component;

    private StatusCommand(ClaudePlugin plugin, string name, string displayName, string caption, string? component,
        string description) : base(plugin)
    {
        _name = name;
        _component = component;
        Descriptor = new CommandDescriptor
        {
            CommandName = name,
            DisplayName = displayName,
            Group = ClaudePlugin.GroupName,
            Icon = "\U000F0430", // mdi-pulse
            Description = description + " Tap to open status.claude.com.",
            States =
            [
                State(OperationalState, "The service is working normally", ClaudeKey.OrangeHex, ClaudeKey.WhiteHex, ClaudeKey.FadedHex,
                    "Check", "\U000F05E1"), // mdi-check-circle-outline
                // Dark text: white is hard to read on amber.
                State(DegradedState, "Degraded performance", ClaudeKey.AmberHex, ClaudeKey.InkHex, ClaudeKey.FadedInkHex,
                    "Alert", "\U000F002A"), // mdi-alert-outline
                State(OutageState, "A partial or major outage", ClaudeKey.RedHex, ClaudeKey.WhiteHex, ClaudeKey.FadedHex,
                    "Outage", "\U000F0CE6"), // mdi-alert-octagon-outline
                State(MaintenanceState, "Maintenance is in progress", ClaudeKey.SlateHex, ClaudeKey.WhiteHex, ClaudeKey.FadedHex,
                    "Wrench", "\U000F0BE0"), // mdi-wrench-outline
                State(UnknownState, "No status yet, the lookup failed, or status keys are off", ClaudeKey.OrangeHex, ClaudeKey.WhiteHex,
                    ClaudeKey.FadedHex, "Unknown", "\U000F0A39") // mdi-cloud-question
            ],
            // Every state brings its own layers.
            ButtonLayout = new ButtonLayoutDescriptor { Mode = ButtonLayoutMode.None }
        };

        ButtonStateDescriptor State(string state, string stateDescription, string background, string color, string faded,
            string iconName, string glyph) => new()
        {
            Name = state,
            Description = stateDescription,
            Layout = new ButtonLayoutDescriptor
            {
                Mode = ButtonLayoutMode.Custom,
                BackgroundColor = background,
                Layers =
                [
                    new ButtonLayerDescriptor
                    {
                        Kind = ButtonLayerKind.Symbol, Name = iconName, Glyph = glyph, IconScale = 0.4, OffsetY = -14, Color = color
                    },
                    new ButtonLayerDescriptor
                    {
                        Kind = ButtonLayerKind.Text, Name = "Service", Text = caption, TextSize = 10,
                        OffsetY = 13, BoxWidth = 84, BoxHeight = 13, Color = faded
                    },
                    new ButtonLayerDescriptor
                    {
                        Kind = ButtonLayerKind.Text, Name = "Status", TextSource = ButtonTextSource.Value, TextSize = 14,
                        OffsetY = 28, BoxWidth = 84, BoxHeight = 16, Color = color
                    }
                ]
            }
        };
    }

    public override CommandDescriptor Descriptor { get; }

    public override ButtonTargets SupportedTargets => ButtonTargets.TouchButton;

    /// <summary>Keeps the demand gate alive; real updates arrive by push from the status poller.</summary>
    public TimeSpan UpdateInterval => TimeSpan.FromSeconds(30);

    public static IEnumerable<StatusCommand> All(ClaudePlugin plugin) =>
    [
        new(plugin, OverallName, "Status: overall", "Claude", null,
            "Shows Claude's overall status from status.claude.com."),
        new(plugin, ClaudeAiName, "Status: claude.ai", "claude.ai", "claude.ai",
            "Shows the status of claude.ai and the Claude apps."),
        new(plugin, ApiName, "Status: API", "API", "Claude API",
            "Shows the status of the Claude API (api.anthropic.com)."),
        new(plugin, CodeName, "Status: Claude Code", "Code", "Claude Code",
            "Shows the status of Claude Code."),
        new(plugin, ConsoleName, "Status: Console", "Console", "Claude Console",
            "Shows the status of the Claude Console (platform.claude.com)."),
        new(plugin, CoworkName, "Status: Cowork", "Cowork", "Claude Cowork",
            "Shows the status of Claude Cowork.")
    ];

    public AdjustmentValue? GetValue(CommandContext ctx)
    {
        if (!Plugin.StatusEnabled)
        {
            Show(ctx, UnknownState);
            return new AdjustmentValue(double.NaN, "Off");
        }

        Plugin.StatusGate.Touch();
        var snapshot = Plugin.Status.Snapshot;
        if (!snapshot.HasData)
        {
            Show(ctx, UnknownState);
            return new AdjustmentValue(double.NaN, "—");
        }

        var health = _component is null ? snapshot.Overall : snapshot.Component(_component) ?? ServiceHealth.Unknown;
        var (state, text) = health switch
        {
            ServiceHealth.Operational => (OperationalState, "OK"),
            ServiceHealth.Degraded => (DegradedState, "Degraded"),
            ServiceHealth.PartialOutage => (OutageState, "Partial"),
            ServiceHealth.MajorOutage => (OutageState, "Outage"),
            ServiceHealth.Maintenance => (MaintenanceState, "Maint."),
            _ => (UnknownState, "—")
        };
        Show(ctx, state);
        return new AdjustmentValue(double.NaN, text);
    }

    /// <summary>Switches the key to <paramref name="state"/> when it shows another one (see <c>WaitingCommand</c>).</summary>
    private void Show(CommandContext ctx, string state)
    {
        if (!string.Equals(ctx.StateName, state, StringComparison.OrdinalIgnoreCase))
            ctx.Host.SetActiveButtonState(_name, state);
    }

    public override Task Execute(CommandContext ctx) => GuardAsync(ctx, "status page", () =>
    {
        if (!ctx.Host.OpenBrowser(StatusClient.PageUrl)) Hint(ctx, "No browser");

        // Fetch now so the key is current when the user looks back at the deck.
        if (Plugin.StatusEnabled)
        {
            Plugin.StatusGate.Touch();
            _ = Plugin.Status.RefreshAsync();
        }

        return Task.CompletedTask;
    });
}
