namespace DevNetDoctor.Core.Models;

public enum Severity
{
    Info = 0,
    Success = 1,
    Warning = 2,
    Error = 3
}

public sealed record Finding(
    Severity Severity,
    string Title,
    string Detail,
    string? SuggestedAction = null,
    string? Code = null);

public sealed class NetworkProbeResult
{
    public required string RouteName { get; init; }
    public string Endpoint { get; init; } = "oauth/token";
    public string Method { get; init; } = "POST";
    public string? HttpVersion { get; init; }
    public string? RemoteAddress { get; init; }
    public string? Proxy { get; init; }
    public bool TransportSucceeded { get; init; }
    public int? HttpStatus { get; init; }
    public string Classification { get; init; } = "Unknown";
    public string Detail { get; init; } = string.Empty;
    public long ElapsedMilliseconds { get; init; }
    public string? CertificateSubject { get; init; }
    public string? CertificateIssuer { get; init; }
    public string? CertificateThumbprint { get; init; }
    public string? CertificateChainStatus { get; init; }
    public string? TlsPolicyErrors { get; init; }
}

public sealed class CodexInfo
{
    public bool Installed { get; init; }
    public string? Version { get; init; }
    public string DesktopVersion { get; init; } = "Unknown (check About Codex)";
    public IReadOnlyList<string> Executables { get; init; } = Array.Empty<string>();
    public string CodexHome { get; init; } = string.Empty;
    public string ConfigPath { get; init; } = string.Empty;
    public bool ConfigExists { get; init; }
    public bool AuthFileExists { get; init; }
    public DateTime? AuthFileLastWrite { get; init; }
    public string LoginStatus { get; init; } = "Unknown";
    public bool? RespectSystemProxy { get; init; }
    public IReadOnlyList<int> RunningProcessIds { get; init; } = Array.Empty<int>();
}

public sealed class CcSwitchInfo
{
    public bool Running { get; init; }
    public IReadOnlyList<int> ProcessIds { get; init; } = Array.Empty<int>();
    public IReadOnlyList<int> ListeningPorts { get; init; } = Array.Empty<int>();
    public IReadOnlyList<string> CandidateConfigFiles { get; init; } = Array.Empty<string>();
}

public sealed class DiagnosticSnapshot
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public required SystemProxyInfo SystemProxy { get; init; }
    public required EnvironmentProxyInfo EnvironmentProxy { get; init; }
    public string WinHttpProxy { get; init; } = "Unknown";
    public IReadOnlyList<NetworkProbeResult> Probes { get; init; } = Array.Empty<NetworkProbeResult>();
    public required CodexInfo Codex { get; init; }
    public required CcSwitchInfo CcSwitch { get; init; }
    public IReadOnlyList<Finding> Findings { get; init; } = Array.Empty<Finding>();
}
