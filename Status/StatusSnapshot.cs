namespace LoupixDeck.Plugin.Claude.Status;

/// <summary>How a Claude service is doing, as status.claude.com reports it, from best to worst.</summary>
internal enum ServiceHealth
{
    Unknown,
    Operational,
    Maintenance,
    Degraded,
    PartialOutage,
    MajorOutage
}

/// <summary>
/// The last thing we learned from the status page. <see cref="Error"/> is set when the fetch failed;
/// the rest then holds the previous good values (if any) so the keys keep showing something useful.
/// </summary>
internal sealed record StatusSnapshot(
    ServiceHealth Overall,
    string? Description,
    IReadOnlyDictionary<string, ServiceHealth> Components,
    IReadOnlyList<string> Incidents,
    DateTimeOffset? FetchedAt,
    string? Error)
{
    public static readonly StatusSnapshot Empty =
        new(ServiceHealth.Unknown, null, new Dictionary<string, ServiceHealth>(), [], null, null);

    public bool HasData => Overall != ServiceHealth.Unknown;

    /// <summary>
    /// The health of the first component whose name starts with <paramref name="prefix"/>, ignoring
    /// case, so "Claude API" finds "Claude API (api.anthropic.com)". Null when the page lists none.
    /// </summary>
    public ServiceHealth? Component(string prefix)
    {
        foreach (var (name, health) in Components)
        {
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return health;
        }

        return null;
    }

    /// <summary>Value equality, including the collections (record equality compares them by reference).</summary>
    public bool SameAs(StatusSnapshot other) =>
        Overall == other.Overall
        && Description == other.Description
        && Error == other.Error
        && Incidents.SequenceEqual(other.Incidents)
        && Components.Count == other.Components.Count
        && Components.All(c => other.Components.TryGetValue(c.Key, out var h) && h == c.Value);
}
