using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ProjectManagementApp.Api;

/// <summary>
/// Writes health-check results as JSON so clients (e.g. the UI dashboard) can
/// parse per-check status, description, and timing.
/// </summary>
public static class HealthResponseWriter
{
    public static Task WriteJson(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                durationMs = e.Value.Duration.TotalMilliseconds,
                data = e.Value.Data
            })
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
