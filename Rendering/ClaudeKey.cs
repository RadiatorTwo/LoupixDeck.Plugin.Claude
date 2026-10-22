namespace LoupixDeck.Plugin.Claude.Rendering;

/// <summary>
/// The shared Claude look: a Claude-orange background with white icons and text, so Claude keys
/// stand out from the rest of the deck. Commands declare it as ordinary layers and a background
/// colour, which the user can restyle in the button editor.
/// </summary>
internal static class ClaudeKey
{
    public const string OrangeHex = "#FF551C";
    public const string WhiteHex = "#FFFFFF";

    /// <summary>Indicator track: black at 20 % over the orange.</summary>
    public const string TrackHex = "#33000000";

    /// <summary>Secondary text: white at 70 %.</summary>
    public const string FadedHex = "#B3FFFFFF";

    // Status key backgrounds: amber (degraded), red (outage), slate (maintenance).
    public const string AmberHex = "#F5A623";
    public const string RedHex = "#D32F2F";
    public const string SlateHex = "#546E7A";

    /// <summary>Dark text for the amber background, where white would be hard to read.</summary>
    public const string InkHex = "#1A1A1A";

    /// <summary>Secondary dark text: ink at 70 %.</summary>
    public const string FadedInkHex = "#B31A1A1A";
}
