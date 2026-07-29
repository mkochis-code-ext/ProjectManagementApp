namespace ProjectManagementApp.HealthChecks;

public enum HealthState
{
    Unknown,
    Healthy,
    Degraded,
    Unreachable
}

/// <summary>Status of a single monitored component, shown in the UI dashboard.</summary>
public record ComponentHealth(
    string Name,
    HealthState State,
    string? Detail = null,
    double? LatencyMs = null,
    DateTime? CheckedAt = null);
