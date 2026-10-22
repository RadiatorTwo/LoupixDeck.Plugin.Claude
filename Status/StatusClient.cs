using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Claude.Status;

/// <summary>
/// Polls the public Statuspage summary behind status.claude.com: the overall indicator, each
/// service's status and the open incidents. No sign-in is involved. A 429 or a server error backs
/// the poll off, and any failure leaves the previous status on the keys with an error note.
/// Runs only while a status key is in demand (see <see cref="DemandGate"/>).
/// </summary>
internal sealed class StatusClient : IDisposable
{
    public const string PageUrl = "https://status.claude.com/";
    private const string Endpoint = "https://status.claude.com/api/v2/summary.json";
    private static readonly TimeSpan BaseInterval = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan MaxInterval = TimeSpan.FromMinutes(15);

    private readonly IPluginLogger _logger;
    private readonly HttpClient _http;
    private readonly object _lock = new();
    private CancellationTokenSource? _loop;
    private TimeSpan _interval = BaseInterval;
    private StatusSnapshot _snapshot = StatusSnapshot.Empty;

    public event Action? Changed;

    public StatusClient(IPluginLogger logger)
    {
        _logger = logger;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("LoupixDeck.Plugin.Claude/0.1 (+https://github.com/web-engineer/LoupixDeck.Plugin.Claude)");
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public StatusSnapshot Snapshot { get { lock (_lock) return _snapshot; } }

    public void Start()
    {
        lock (_lock)
        {
            if (_loop is not null) return;
            _loop = new CancellationTokenSource();
            _interval = BaseInterval;
            _ = RunAsync(_loop.Token);
        }
    }

    public void Stop()
    {
        CancellationTokenSource? loop;
        lock (_lock)
        {
            loop = _loop;
            _loop = null;
        }

        if (loop is null) return;
        loop.Cancel();
        loop.Dispose();
    }

    /// <summary>Fetches now, outside the loop's schedule (a tap on the key, or the settings Test button).</summary>
    public Task<StatusSnapshot> RefreshAsync(CancellationToken ct = default) => FetchAsync(ct);

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await FetchAsync(ct).ConfigureAwait(false);
                TimeSpan wait;
                lock (_lock) wait = _interval;
                await Task.Delay(wait, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.Error("Status poll loop stopped", ex);
        }
    }

    private async Task<StatusSnapshot> FetchAsync(CancellationToken ct)
    {
        var previous = Snapshot;
        StatusSnapshot next;
        try
        {
            using var response = await _http.GetAsync(Endpoint, ct).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500)
            {
                TimeSpan backoff;
                lock (_lock)
                {
                    _interval = _interval * 2 > MaxInterval ? MaxInterval : _interval * 2;
                    backoff = _interval;
                }

                _logger.Warn($"Status page returned HTTP {(int)response.StatusCode}; next poll in {backoff.TotalMinutes:0} min");
                next = previous with { Error = $"HTTP {(int)response.StatusCode}", FetchedAt = DateTimeOffset.Now };
            }
            else if (!response.IsSuccessStatusCode)
            {
                next = previous with { Error = $"HTTP {(int)response.StatusCode}", FetchedAt = DateTimeOffset.Now };
            }
            else
            {
                var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                var parsed = Parse(body);
                next = parsed.HasData ? parsed : previous with { Error = parsed.Error, FetchedAt = parsed.FetchedAt };
                lock (_lock) _interval = BaseInterval;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            next = previous with { Error = ex is HttpRequestException or TaskCanceledException ? "Offline" : ex.Message, FetchedAt = DateTimeOffset.Now };
        }

        bool changed;
        lock (_lock)
        {
            changed = !next.SameAs(_snapshot);
            _snapshot = next;
        }

        if (next.Overall != previous.Overall && next.Error is null)
        {
            _logger.Info($"Claude status: {next.Description ?? next.Overall.ToString()}");
        }

        if (changed) Changed?.Invoke();
        return next;
    }

    /// <summary>
    /// Reads <c>status.indicator</c> / <c>status.description</c>, each <c>components[]</c> entry's
    /// <c>name</c> and <c>status</c>, the open <c>incidents[]</c> and any in-progress maintenance.
    /// Anything missing is simply absent; a body without an indicator is "Unexpected response".
    /// </summary>
    internal static StatusSnapshot Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("status", out var status) || status.ValueKind != JsonValueKind.Object
                || !status.TryGetProperty("indicator", out var indicator) || indicator.ValueKind != JsonValueKind.String)
            {
                return StatusSnapshot.Empty with { FetchedAt = DateTimeOffset.Now, Error = "Unexpected response" };
            }

            var overall = indicator.GetString() switch
            {
                "none" => ServiceHealth.Operational,
                "minor" => ServiceHealth.Degraded,
                "major" => ServiceHealth.PartialOutage,
                "critical" => ServiceHealth.MajorOutage,
                "maintenance" => ServiceHealth.Maintenance,
                _ => ServiceHealth.Unknown
            };
            if (overall == ServiceHealth.Operational && MaintenanceInProgress(root)) overall = ServiceHealth.Maintenance;

            var description = status.TryGetProperty("description", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() : null;
            return new StatusSnapshot(overall, description, Components(root), Names(root, "incidents"), DateTimeOffset.Now, null);
        }
        catch (JsonException)
        {
            return StatusSnapshot.Empty with { FetchedAt = DateTimeOffset.Now, Error = "Unexpected response" };
        }
    }

    private static Dictionary<string, ServiceHealth> Components(JsonElement root)
    {
        var result = new Dictionary<string, ServiceHealth>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty("components", out var components) || components.ValueKind != JsonValueKind.Array) return result;

        foreach (var component in components.EnumerateArray())
        {
            if (component.ValueKind != JsonValueKind.Object
                || !component.TryGetProperty("name", out var name) || name.GetString() is not { Length: > 0 } componentName
                || !component.TryGetProperty("status", out var s) || s.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            result[componentName] = s.GetString() switch
            {
                "operational" => ServiceHealth.Operational,
                "degraded_performance" => ServiceHealth.Degraded,
                "partial_outage" => ServiceHealth.PartialOutage,
                "major_outage" => ServiceHealth.MajorOutage,
                "under_maintenance" => ServiceHealth.Maintenance,
                _ => ServiceHealth.Unknown
            };
        }

        return result;
    }

    private static bool MaintenanceInProgress(JsonElement root) =>
        root.TryGetProperty("scheduled_maintenances", out var list) && list.ValueKind == JsonValueKind.Array
        && list.EnumerateArray().Any(m => m.ValueKind == JsonValueKind.Object
                                          && m.TryGetProperty("status", out var s) && s.GetString() == "in_progress");

    private static List<string> Names(JsonElement root, string property)
    {
        var result = new List<string>();
        if (!root.TryGetProperty(property, out var list) || list.ValueKind != JsonValueKind.Array) return result;
        foreach (var item in list.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("name", out var n) && n.GetString() is { Length: > 0 } name)
                result.Add(name);
        }

        return result;
    }

    public void Dispose()
    {
        Stop();
        _http.Dispose();
    }
}
