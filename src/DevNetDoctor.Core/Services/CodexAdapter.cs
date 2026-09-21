using System.Diagnostics;
using DevNetDoctor.Core.Models;

namespace DevNetDoctor.Core.Services;

public sealed class CodexAdapter
{
    public async Task<CodexInfo> InspectAsync(CancellationToken cancellationToken = default)
    {
        var where = await CommandRunner.RunAsync("where.exe", "codex", 5000, cancellationToken);
        var paths = where.ExitCode == 0
            ? where.StdOut.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            : Array.Empty<string>();

        var installed = paths.Length > 0;
        var version = installed ? (await CommandRunner.RunAsync("codex", "--version", 5000, cancellationToken)).Combined.Trim() : null;
        var loginStatus = installed ? (await CommandRunner.RunAsync("codex", "login status", 7000, cancellationToken)).Combined.Trim() : "Not installed";

        var codexHome = Environment.GetEnvironmentVariable("CODEX_HOME", EnvironmentVariableTarget.Process);
        if (string.IsNullOrWhiteSpace(codexHome))
            codexHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");

        var configPath = Path.Combine(codexHome, "config.toml");
        var authPath = Path.Combine(codexHome, "auth.json");
        DateTime? authWrite = null;
        if (File.Exists(authPath)) authWrite = File.GetLastWriteTime(authPath);

        var pids = Process.GetProcesses()
            .Where(p => p.ProcessName.Contains("codex", StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Id)
            .OrderBy(x => x)
            .ToArray();

        return new CodexInfo
        {
            Installed = installed,
            Version = SecretRedactor.Redact(version),
            Executables = paths,
            CodexHome = codexHome,
            ConfigPath = configPath,
            ConfigExists = File.Exists(configPath),
            AuthFileExists = File.Exists(authPath),
            AuthFileLastWrite = authWrite,
            LoginStatus = SecretRedactor.Redact(loginStatus),
            RespectSystemProxy = CodexConfigEditor.ReadRespectSystemProxy(configPath),
            RunningProcessIds = pids
        };
    }

    public string EnableRespectSystemProxy(string configPath)
        => CodexConfigEditor.EnableRespectSystemProxy(configPath);

    public int StopCodexProcesses()
    {
        var stopped = 0;
        foreach (var process in Process.GetProcesses().Where(p => p.ProcessName.Contains("codex", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                process.Kill(entireProcessTree: true);
                stopped++;
            }
            catch { }
        }
        return stopped;
    }
}
