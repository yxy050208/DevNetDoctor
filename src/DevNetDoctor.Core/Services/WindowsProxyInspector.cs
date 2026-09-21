using System.Net.NetworkInformation;
using Microsoft.Win32;
using DevNetDoctor.Core.Models;

namespace DevNetDoctor.Core.Services;

public static class WindowsProxyInspector
{
    public static SystemProxyInfo GetSystemProxy()
    {
        const string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(keyPath);
            var enabled = Convert.ToInt32(key?.GetValue("ProxyEnable") ?? 0) == 1;
            var raw = key?.GetValue("ProxyServer") as string;
            var autoConfig = key?.GetValue("AutoConfigURL") as string;
            var (http, https) = ProxyParser.ParseWinInetProxyServer(raw);
            var endpoint = https ?? http;

            return new SystemProxyInfo
            {
                Enabled = enabled,
                RawProxyServer = raw,
                AutoConfigUrl = autoConfig,
                HttpProxy = http,
                HttpsProxy = https,
                IsListening = endpoint is not null && IsEndpointListening(endpoint),
                Source = "WinINET (HKCU Internet Settings)"
            };
        }
        catch
        {
            return new SystemProxyInfo { Source = "WinINET (read failed)" };
        }
    }

    public static EnvironmentProxyInfo GetEnvironmentProxy()
    {
        string? P(string name) => Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process)
                                ?? Environment.GetEnvironmentVariable(name.ToLowerInvariant(), EnvironmentVariableTarget.Process);
        string? U(string name) => Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User)
                                ?? Environment.GetEnvironmentVariable(name.ToLowerInvariant(), EnvironmentVariableTarget.User);

        var https = P("HTTPS_PROXY");
        var http = P("HTTP_PROXY");
        var all = P("ALL_PROXY");

        return new EnvironmentProxyInfo
        {
            HttpProxy = http,
            HttpsProxy = https,
            AllProxy = all,
            NoProxy = P("NO_PROXY"),
            UserHttpProxy = U("HTTP_PROXY"),
            UserHttpsProxy = U("HTTPS_PROXY"),
            UserAllProxy = U("ALL_PROXY"),
            UserNoProxy = U("NO_PROXY"),
            EffectiveHttpsProxy = ProxyParser.ParseSingle(new[] { https, http, all }.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)))
        };
    }

    public static bool IsEndpointListening(ProxyEndpoint endpoint)
    {
        if (!ProxyParser.IsLoopback(endpoint)) return true;
        try
        {
            var listeners = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
            return listeners.Any(ep => ep.Port == endpoint.Port);
        }
        catch { return false; }
    }

    public static async Task<string> GetWinHttpProxyAsync(CancellationToken cancellationToken = default)
    {
        var result = await CommandRunner.RunAsync("netsh", "winhttp show proxy", 5000, cancellationToken);
        if (result.ExitCode != 0) return "Unavailable";
        var text = SecretRedactor.Redact(result.StdOut.Trim());
        return string.IsNullOrWhiteSpace(text) ? "Unknown" : text;
    }
}
