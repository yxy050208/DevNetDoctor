using System.Text;
using DevNetDoctor.Core.Models;

namespace DevNetDoctor.Core.Services;

public static class DiagnosticReportBuilder
{
    public static string Build(DiagnosticSnapshot s)
    {
        var sb = new StringBuilder();
        sb.AppendLine("DevNet Doctor diagnostic report");
        sb.AppendLine("================================");
        sb.AppendLine($"Generated: {s.Timestamp:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine($"OS: {Environment.OSVersion}");
        sb.AppendLine($"64-bit process: {Environment.Is64BitProcess}");
        sb.AppendLine();

        sb.AppendLine("[Windows proxy]");
        sb.AppendLine($"Enabled: {s.SystemProxy.Enabled}");
        sb.AppendLine($"ProxyServer: {s.SystemProxy.RawProxyServer ?? "(none)"}");
        sb.AppendLine($"AutoConfigURL: {s.SystemProxy.AutoConfigUrl ?? "(none)"}");
        sb.AppendLine($"Parsed HTTPS: {s.SystemProxy.HttpsProxy?.ToString() ?? "(none)"}");
        sb.AppendLine($"Local listener: {s.SystemProxy.IsListening}");
        sb.AppendLine();

        sb.AppendLine("[Environment proxy]");
        sb.AppendLine($"HTTP_PROXY (process): {s.EnvironmentProxy.HttpProxy ?? "(none)"}");
        sb.AppendLine($"HTTPS_PROXY (process): {s.EnvironmentProxy.HttpsProxy ?? "(none)"}");
        sb.AppendLine($"ALL_PROXY (process): {s.EnvironmentProxy.AllProxy ?? "(none)"}");
        sb.AppendLine($"NO_PROXY (process): {s.EnvironmentProxy.NoProxy ?? "(none)"}");
        sb.AppendLine($"HTTP_PROXY (user): {s.EnvironmentProxy.UserHttpProxy ?? "(none)"}");
        sb.AppendLine($"HTTPS_PROXY (user): {s.EnvironmentProxy.UserHttpsProxy ?? "(none)"}");
        sb.AppendLine($"ALL_PROXY (user): {s.EnvironmentProxy.UserAllProxy ?? "(none)"}");
        sb.AppendLine($"NO_PROXY (user): {s.EnvironmentProxy.UserNoProxy ?? "(none)"}");
        sb.AppendLine();

        sb.AppendLine("[WinHTTP]");
        sb.AppendLine(s.WinHttpProxy);
        sb.AppendLine();

        sb.AppendLine("[Route matrix: OpenAI OAuth endpoint]");
        foreach (var probe in s.Probes)
        {
            sb.AppendLine($"{probe.RouteName}: status={probe.HttpStatus?.ToString() ?? "n/a"}, class={probe.Classification}, proxy={probe.Proxy ?? "(none)"}, elapsed={probe.ElapsedMilliseconds}ms");
            if (!string.IsNullOrWhiteSpace(probe.Detail)) sb.AppendLine($"  detail: {probe.Detail}");
        }
        sb.AppendLine("Note: the OAuth probe sends only grant_type=test and never sends account credentials or tokens.");
        sb.AppendLine();

        sb.AppendLine("[Codex]");
        sb.AppendLine($"Installed: {s.Codex.Installed}");
        sb.AppendLine($"Version: {s.Codex.Version ?? "(unknown)"}");
        sb.AppendLine($"Executables: {string.Join(" | ", s.Codex.Executables)}");
        sb.AppendLine($"CODEX_HOME resolved: {s.Codex.CodexHome}");
        sb.AppendLine($"Config: {s.Codex.ConfigPath} (exists={s.Codex.ConfigExists})");
        sb.AppendLine($"respect_system_proxy: {s.Codex.RespectSystemProxy?.ToString() ?? "not set"}");
        sb.AppendLine($"Auth file exists: {s.Codex.AuthFileExists}; last write: {s.Codex.AuthFileLastWrite?.ToString("s") ?? "n/a"}");
        sb.AppendLine($"Login status: {s.Codex.LoginStatus}");
        sb.AppendLine($"Running Codex PIDs: {(s.Codex.RunningProcessIds.Count == 0 ? "none" : string.Join(", ", s.Codex.RunningProcessIds))}");
        sb.AppendLine("auth.json contents are never read.");
        sb.AppendLine();

        sb.AppendLine("[CCSwitch]");
        sb.AppendLine($"Running: {s.CcSwitch.Running}");
        sb.AppendLine($"PIDs: {(s.CcSwitch.ProcessIds.Count == 0 ? "none" : string.Join(", ", s.CcSwitch.ProcessIds))}");
        sb.AppendLine($"Listening ports: {(s.CcSwitch.ListeningPorts.Count == 0 ? "none" : string.Join(", ", s.CcSwitch.ListeningPorts))}");
        sb.AppendLine($"Candidate config files: {(s.CcSwitch.CandidateConfigFiles.Count == 0 ? "none" : string.Join(" | ", s.CcSwitch.CandidateConfigFiles))}");
        sb.AppendLine();

        sb.AppendLine("[Findings]");
        foreach (var f in s.Findings)
        {
            sb.AppendLine($"{f.Severity.ToString().ToUpperInvariant()}: {f.Title}");
            sb.AppendLine($"  {f.Detail}");
            if (!string.IsNullOrWhiteSpace(f.SuggestedAction)) sb.AppendLine($"  Suggested: {f.SuggestedAction}");
            if (!string.IsNullOrWhiteSpace(f.Code)) sb.AppendLine($"  Code: {f.Code}");
        }

        sb.AppendLine();
        sb.AppendLine("Privacy: no auth.json contents, browser cookies, account tokens, or OAuth authorization codes are collected by this report.");
        return SecretRedactor.Redact(sb.ToString());
    }
}
