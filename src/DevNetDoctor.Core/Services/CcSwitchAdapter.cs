using System.Diagnostics;
using System.Text.RegularExpressions;
using DevNetDoctor.Core.Models;

namespace DevNetDoctor.Core.Services;

public sealed class CcSwitchAdapter
{
    public async Task<CcSwitchInfo> InspectAsync(CancellationToken cancellationToken = default)
    {
        var processes = Process.GetProcesses()
            .Where(p => p.ProcessName.Contains("ccswitch", StringComparison.OrdinalIgnoreCase)
                     || p.ProcessName.Contains("cc-switch", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var pids = processes.Select(p => p.Id).Distinct().OrderBy(x => x).ToArray();
        var ports = new HashSet<int>();

        if (pids.Length > 0)
        {
            var netstat = await CommandRunner.RunAsync("netstat", "-ano -p tcp", 7000, cancellationToken);
            if (netstat.ExitCode == 0)
            {
                foreach (var line in netstat.StdOut.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = Regex.Split(line.Trim(), @"\s+");
                    if (parts.Length < 5 || !parts[0].Equals("TCP", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!int.TryParse(parts[^1], out var pid) || !pids.Contains(pid)) continue;
                    if (!parts[3].Equals("LISTENING", StringComparison.OrdinalIgnoreCase)) continue;
                    var local = parts[1];
                    var colon = local.LastIndexOf(':');
                    if (colon > 0 && int.TryParse(local[(colon + 1)..], out var port)) ports.Add(port);
                }
            }
        }

        return new CcSwitchInfo
        {
            Running = pids.Length > 0,
            ProcessIds = pids,
            ListeningPorts = ports.OrderBy(x => x).ToArray(),
            CandidateConfigFiles = FindCandidateConfigs()
        };
    }

    private static IReadOnlyList<string> FindCandidateConfigs()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roots = new[]
        {
            Path.Combine(home, ".ccswitch"),
            Path.Combine(home, ".cc-switch"),
            Path.Combine(roaming, "CCSwitch"),
            Path.Combine(roaming, "cc-switch"),
            Path.Combine(local, "CCSwitch"),
            Path.Combine(local, "cc-switch")
        }.Distinct(StringComparer.OrdinalIgnoreCase);

        var results = new List<string>();
        foreach (var root in roots)
        {
            if (!Directory.Exists(root)) continue;
            try
            {
                results.AddRange(Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
                    .Where(path => path.EndsWith(".toml", StringComparison.OrdinalIgnoreCase)
                                || path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                                || path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)
                                || path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
                    .Take(20));
            }
            catch { }
        }
        return results.Distinct(StringComparer.OrdinalIgnoreCase).Take(30).ToArray();
    }
}
