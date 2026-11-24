using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace ProjectManagementApp;

// Shared protocol only: no board service, file paths, payloads or persisted credentials.
public sealed class RuntimeIdentity
{
    public const string SecretVariable = "ARCFLOW_OWNER_KEY";
    public const string InstanceVariable = "ARCFLOW_INSTANCE_ID";
    public const string Header = "X-ArcFlow-Owner";
    private readonly string? _ownerKey;
    public string App => "ArcFlow";
    public string Role { get; }
    public int Protocol => 1;
    public int Pid { get; } = Environment.ProcessId;
    public string InstanceId { get; }
    public string? ApiBaseUrl { get; }

    public RuntimeIdentity(string role, string? apiBaseUrl = null)
        : this(role, apiBaseUrl, Environment.GetEnvironmentVariable(SecretVariable),
            Environment.GetEnvironmentVariable(InstanceVariable)) { }

    public RuntimeIdentity(string role, string? apiBaseUrl, string? ownerKey, string? instanceId)
    {
        Role = role;
        ApiBaseUrl = role == "mcp" ? CanonicalApi(apiBaseUrl) : null;
        _ownerKey = ownerKey;
        InstanceId = Guid.TryParseExact(instanceId, "N", out var parsed) ? parsed.ToString("N") : Guid.NewGuid().ToString("N");
    }

    public static string? CanonicalApi(string? value)
    {
        if (value is null || !System.Text.RegularExpressions.Regex.IsMatch(value,
                @"\Ahttp://(?:127\.0\.0\.1|localhost):5180/?\z",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase |
                System.Text.RegularExpressions.RegexOptions.CultureInvariant)) return null;
        return "http://127.0.0.1:5180";
    }

    public bool LocalRequest(IPAddress? remote, string? host, string? origin)
    {
        var port = Role == "api" ? 5180 : 5190;
        return remote is not null && host is not null && IPAddress.IsLoopback(remote) && string.IsNullOrEmpty(origin) &&
            (host.Equals($"127.0.0.1:{port}", StringComparison.OrdinalIgnoreCase) ||
             host.Equals($"localhost:{port}", StringComparison.OrdinalIgnoreCase));
    }

    public bool AuthorizeShutdown(string? key, string? instance, string? pid)
    {
        if (_ownerKey is not { Length: 64 } || key is not { Length: 64 } ||
            instance != InstanceId || pid != Pid.ToString(System.Globalization.CultureInfo.InvariantCulture))
            return false;
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(_ownerKey));
    }
}
