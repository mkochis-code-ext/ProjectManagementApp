using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ProjectManagementApp.Mcp;
using ProjectManagementApp.Services;
using ProjectManagementApp;

var builder = WebApplication.CreateBuilder(args);

var configuredApi = builder.Configuration["Endpoints:Api"] ?? "http://localhost:5180";
var apiBaseUrl = RuntimeIdentity.CanonicalApi(configuredApi) ?? configuredApi;

// All MCP tools access data through the API (the sole owner of the JSON file).
builder.Services.AddHttpClient<IBoardService, BoardApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});

// Dedicated client for the health check to ping the API's /health.
builder.Services.AddHttpClient("api-health", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(2);
});

builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<BoardTools>();

builder.Services.AddHealthChecks()
    .AddCheck<ApiHealthCheck>("api");

var app = builder.Build();
var runtime = new ProjectManagementApp.RuntimeIdentity("mcp", apiBaseUrl);
app.MapArcFlowRuntime(runtime);

app.MapMcp();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = (context, report) =>
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
});

app.Run();
