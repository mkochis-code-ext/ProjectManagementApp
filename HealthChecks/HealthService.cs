using System.Diagnostics;
using System.Text.Json;

namespace ProjectManagementApp.HealthChecks;

/// <summary>
/// Polls the health endpoints of the other processes (API, MCP) so the UI can
/// display the status of every component in the system. The UI itself is always
/// reported healthy because this code only runs when the UI is up.
/// </summary>
public class HealthService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public HealthService(IHttpClientFactory httpClientFactory) => _httpClientFactory = httpClientFactory;

    public async Task<IReadOnlyList<ComponentHealth>> CheckAllAsync(CancellationToken ct = default)
    {
        var now = DateTime.Now;
        var results = new List<ComponentHealth>
        {
            new("UI", HealthState.Healthy, "This app is running.", 0, now)
        };

        var (api, dataFile) = await CheckApiAsync(ct);
        results.Add(api);
        results.Add(dataFile);
        results.Add(await CheckEndpointAsync("MCP Server", "mcp", ct));

        return results;
    }

    private async Task<(ComponentHealth api, ComponentHealth dataFile)> CheckApiAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var client = _httpClientFactory.CreateClient("api");
            using var resp = await client.GetAsync("health", ct);
            sw.Stop();
            var body = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            var apiState = MapState(GetString(root, "status"), resp.IsSuccessStatusCode);
            var api = new ComponentHealth("API", apiState,
                resp.IsSuccessStatusCode ? "API is responding." : $"HTTP {(int)resp.StatusCode}",
                sw.Elapsed.TotalMilliseconds, DateTime.Now);

            // Pull the data-file sub-check out of the API report.
            var dataFile = ExtractDataFileCheck(root);
            return (api, dataFile);
        }
        catch (Exception ex)
        {
            sw.Stop();
            var when = DateTime.Now;
            return (
                new ComponentHealth("API", HealthState.Unreachable, ex.Message, sw.Elapsed.TotalMilliseconds, when),
                new ComponentHealth("Data File", HealthState.Unknown, "Cannot determine (API unreachable).", null, when));
        }
    }

    private async Task<ComponentHealth> CheckEndpointAsync(string displayName, string clientName, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var client = _httpClientFactory.CreateClient(clientName);
            using var resp = await client.GetAsync("health", ct);
            sw.Stop();
            var body = await resp.Content.ReadAsStringAsync(ct);
            string? status = null;
            try
            {
                using var doc = JsonDocument.Parse(body);
                status = GetString(doc.RootElement, "status");
            }
            catch { /* non-JSON body; fall back to status code */ }

            return new ComponentHealth(displayName,
                MapState(status, resp.IsSuccessStatusCode),
                resp.IsSuccessStatusCode ? $"{displayName} is responding." : $"HTTP {(int)resp.StatusCode}",
                sw.Elapsed.TotalMilliseconds, DateTime.Now);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ComponentHealth(displayName, HealthState.Unreachable, ex.Message, sw.Elapsed.TotalMilliseconds, DateTime.Now);
        }
    }

    private static ComponentHealth ExtractDataFileCheck(JsonElement root)
    {
        if (root.TryGetProperty("checks", out var checks) && checks.ValueKind == JsonValueKind.Array)
        {
            foreach (var check in checks.EnumerateArray())
            {
                if (string.Equals(GetString(check, "name"), "data-file", StringComparison.OrdinalIgnoreCase))
                {
                    var detail = GetString(check, "description");
                    if (check.TryGetProperty("data", out var data) &&
                        data.TryGetProperty("path", out var path))
                    {
                        detail = $"{detail} ({path.GetString()})";
                    }
                    return new ComponentHealth("Data File", MapState(GetString(check, "status"), true), detail, null, DateTime.Now);
                }
            }
        }
        return new ComponentHealth("Data File", HealthState.Unknown, "No data-file check reported.", null, DateTime.Now);
    }

    private static HealthState MapState(string? status, bool httpSuccess) => status?.ToLowerInvariant() switch
    {
        "healthy" => HealthState.Healthy,
        "degraded" => HealthState.Degraded,
        "unhealthy" => HealthState.Degraded,
        _ => httpSuccess ? HealthState.Healthy : HealthState.Unreachable
    };

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) ? value.GetString() : null;
}
