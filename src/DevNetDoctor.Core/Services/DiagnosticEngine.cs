using DevNetDoctor.Core.Models;

namespace DevNetDoctor.Core.Services;

public sealed class DiagnosticEngine
{
    private readonly NetworkProbeService _networkProbe = new();
    private readonly CodexAdapter _codex = new();
    private readonly CcSwitchAdapter _ccSwitch = new();

    public async Task<DiagnosticSnapshot> ScanAsync(CancellationToken cancellationToken = default)
    {
        var systemProxy = WindowsProxyInspector.GetSystemProxy();
        var envProxy = WindowsProxyInspector.GetEnvironmentProxy();

        var winHttpTask = WindowsProxyInspector.GetWinHttpProxyAsync(cancellationToken);
        var codexTask = _codex.InspectAsync(cancellationToken);
        var ccSwitchTask = _ccSwitch.InspectAsync(cancellationToken);
        var probesTask = _networkProbe.ProbeOpenAiRoutesAsync(systemProxy, envProxy, cancellationToken);

        await Task.WhenAll(winHttpTask, codexTask, ccSwitchTask, probesTask);

        var snapshot = new DiagnosticSnapshot
        {
            Timestamp = DateTime.Now,
            SystemProxy = systemProxy,
            EnvironmentProxy = envProxy,
            WinHttpProxy = await winHttpTask,
            Probes = await probesTask,
            Codex = await codexTask,
            CcSwitch = await ccSwitchTask,
            Findings = Array.Empty<Finding>()
        };

        return new DiagnosticSnapshot
        {
            Timestamp = snapshot.Timestamp,
            SystemProxy = snapshot.SystemProxy,
            EnvironmentProxy = snapshot.EnvironmentProxy,
            WinHttpProxy = snapshot.WinHttpProxy,
            Probes = snapshot.Probes,
            Codex = snapshot.Codex,
            CcSwitch = snapshot.CcSwitch,
            Findings = Analyze(snapshot)
        };
    }

    private static IReadOnlyList<Finding> Analyze(DiagnosticSnapshot s)
    {
        var findings = new List<Finding>();
        var sys = s.SystemProxy.Enabled ? s.SystemProxy.HttpsProxy ?? s.SystemProxy.HttpProxy : null;
        var env = s.EnvironmentProxy.EffectiveHttpsProxy;

        if (s.SystemProxy.Enabled && sys is not null)
        {
            findings.Add(new Finding(
                s.SystemProxy.IsListening ? Severity.Success : Severity.Error,
                "Windows system proxy",
                s.SystemProxy.IsListening
                    ? $"Enabled at {sys}; local listener detected when applicable."
                    : $"Enabled at {sys}, but no local listener was detected on that port.",
                s.SystemProxy.IsListening ? null : "Start the proxy application or update the Windows proxy port.",
                "SYSTEM_PROXY"));
        }
        else if (!string.IsNullOrWhiteSpace(s.SystemProxy.AutoConfigUrl))
        {
            findings.Add(new Finding(Severity.Info, "PAC configuration detected",
                $"AutoConfigURL is configured: {s.SystemProxy.AutoConfigUrl}. v0.1 reports PAC but does not execute PAC scripts.",
                "Use the route matrix to confirm which path works.", "PAC_DETECTED"));
        }
        else
        {
            findings.Add(new Finding(Severity.Info, "No manual Windows proxy",
                "WinINET manual proxy is disabled or no proxy endpoint was found.", Code: "NO_SYSTEM_PROXY"));
        }

        if (sys is not null && env is not null && (!sys.Host.Equals(env.Host, StringComparison.OrdinalIgnoreCase) || sys.Port != env.Port))
        {
            findings.Add(new Finding(Severity.Warning, "Proxy mismatch / possible stale port",
                $"Windows proxy is {sys}, while the current process proxy is {env}.",
                "Prefer system-proxy discovery for apps that support it, or deliberately synchronize environment variables after verifying the active port.",
                "STALE_PROXY"));
        }

        if (env is not null && ProxyParser.IsLoopback(env) && !WindowsProxyInspector.IsEndpointListening(env))
        {
            findings.Add(new Finding(Severity.Error, "Environment proxy points to a closed local port",
                $"The effective HTTPS proxy is {env}, but nothing is listening on port {env.Port}.",
                "Remove the stale proxy variable or update it to the current local proxy port.",
                "ENV_PROXY_CLOSED"));
        }

        var direct = s.Probes.FirstOrDefault(p => p.RouteName == "Direct");
        var system = s.Probes.FirstOrDefault(p => p.RouteName == "Windows system proxy");
        var environment = s.Probes.FirstOrDefault(p => p.RouteName == "Environment proxy");
        var workingProxy = new[] { system, environment }.FirstOrDefault(p => p?.Classification == "OAuthReachable");

        foreach (var probe in s.Probes.Where(p => p.Classification != "OAuthReachable"))
            findings.Add(new Finding(Severity.Warning, $"{probe.RouteName}: OAuth not confirmed",
                $"HTTP {probe.HttpStatus?.ToString() ?? "n/a"}; {probe.Classification}. {probe.Detail}",
                "Inspect the route matrix. An HTTP response alone does not confirm OAuth service availability.", "ROUTE_UNCONFIRMED"));

        if (direct?.Classification == "RegionBlocked" && workingProxy is not null)
        {
            findings.Add(new Finding(Severity.Warning, "Direct route differs from proxy route",
                $"Direct OpenAI OAuth probe was region-blocked, while {workingProxy.RouteName} reached the OAuth service normally.",
                "For software that is expected to use your configured proxy, make sure it actually inherits or respects that proxy. Do not use the tool to bypass service availability rules.",
                "DIRECT_PROXY_DIVERGENCE"));
        }

        if (s.Codex.Installed)
        {
            if (s.Codex.RespectSystemProxy != true && s.SystemProxy.Enabled && sys is not null)
            {
                findings.Add(new Finding(Severity.Warning, "Codex system-proxy support is not enabled",
                    "Codex is installed and a Windows system proxy is enabled, but [features] respect_system_proxy = true was not found.",
                    "Use the Codex repair action to back up config.toml and enable respect_system_proxy.",
                    "CODEX_RESPECT_PROXY"));
            }
            else if (s.Codex.RespectSystemProxy == true)
            {
                findings.Add(new Finding(Severity.Success, "Codex proxy feature enabled",
                    "[features] respect_system_proxy = true is present in config.toml.", Code: "CODEX_RESPECT_PROXY_OK"));
            }

            if (s.Codex.LoginStatus.Contains("Logged in using ChatGPT", StringComparison.OrdinalIgnoreCase) && s.Codex.AuthFileExists)
            {
                findings.Add(new Finding(Severity.Success, "Codex authentication persisted",
                    $"CLI reports ChatGPT login and auth.json exists (last modified {s.Codex.AuthFileLastWrite:g}). The file contents were not read.", Code: "CODEX_AUTH_OK"));
            }
            else if (s.Codex.LoginStatus.Contains("Not logged in", StringComparison.OrdinalIgnoreCase))
            {
                findings.Add(new Finding(Severity.Warning, "Codex is not logged in",
                    "Codex CLI reports that no login is active.",
                    "Run Codex login after confirming a working network route.", "CODEX_NOT_LOGGED_IN"));
            }
        }

        if (s.CcSwitch.Running)
        {
            var ports = s.CcSwitch.ListeningPorts.Count == 0 ? "none detected" : string.Join(", ", s.CcSwitch.ListeningPorts);
            findings.Add(new Finding(Severity.Info, "CCSwitch detected",
                $"CCSwitch appears to be running. Listening TCP ports associated with its process: {ports}.",
                "v0.1 does not rewrite CCSwitch provider files; use the report to compare its local endpoint with the active Windows proxy.",
                "CCSWITCH_DETECTED"));
        }

        if (findings.All(f => f.Severity < Severity.Warning))
        {
            findings.Add(new Finding(Severity.Success, "No obvious proxy mismatch found",
                "The first-pass checks did not detect a common stale-port or route-divergence problem.", Code: "SCAN_CLEAN"));
        }

        return findings;
    }
}
