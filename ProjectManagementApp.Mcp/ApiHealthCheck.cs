using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ProjectManagementApp.Mcp;

/// <summary>
/// Reports the MCP server as healthy only when it can reach the API process,
/// since all MCP tools depend on the API for data access.
/// </summary>
public class ApiHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _httpClientFactory;

    public ApiHealthCheck(IHttpClientFactory httpClientFactory) => _httpClientFactory = httpClientFactory;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient("api-health");
        var data = new Dictionary<string, object> { ["apiBaseUrl"] = client.BaseAddress?.ToString() ?? "" };
        try
        {
            var resp = await client.GetAsync("health", cancellationToken);
            data["apiStatusCode"] = (int)resp.StatusCode;
            return resp.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("API is reachable.", data)
                : HealthCheckResult.Degraded("API responded but is not healthy.", data: data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("API is unreachable.", ex, data);
        }
    }
}
