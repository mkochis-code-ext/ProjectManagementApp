using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ProjectManagementApp.Models;

namespace ProjectManagementApp.Api;

/// <summary>
/// Verifies that the board data file is present and parseable. The check reads
/// and deserializes the file independently of the shared <c>BoardService</c>
/// singleton so it never mutates live application state.
/// </summary>
public class BoardFileHealthCheck : IHealthCheck
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public BoardFileHealthCheck()
    {
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        _filePath = Path.Combine(documentsPath, "BoardCollection.json");
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object> { ["path"] = _filePath };

        if (!File.Exists(_filePath))
        {
            // No file yet is a valid first-run state, not a failure.
            data["exists"] = false;
            return HealthCheckResult.Healthy("Data file not created yet (first run).", data);
        }

        data["exists"] = true;
        try
        {
            var json = await File.ReadAllTextAsync(_filePath, cancellationToken);
            var collection = JsonSerializer.Deserialize<BoardCollection>(json, _jsonOptions);
            data["boardCount"] = collection?.Boards.Count ?? 0;
            return HealthCheckResult.Healthy("Data file is readable and valid.", data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Data file is unreadable or corrupt.", ex, data);
        }
    }
}
