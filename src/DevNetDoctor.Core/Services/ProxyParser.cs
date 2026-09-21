using DevNetDoctor.Core.Models;

namespace DevNetDoctor.Core.Services;

public static class ProxyParser
{
    public static ProxyEndpoint? ParseSingle(string? raw, string defaultScheme = "http")
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var value = raw.Trim();

        if (!value.Contains("://", StringComparison.Ordinal))
            value = $"{defaultScheme}://{value}";

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return null;
        if (string.IsNullOrWhiteSpace(uri.Host) || uri.Port <= 0) return null;
        if (uri.Scheme is not ("http" or "https")) return null;
        if (!string.IsNullOrEmpty(uri.UserInfo)) return null;

        return new ProxyEndpoint(uri.Scheme, uri.Host, uri.Port);
    }

    public static (ProxyEndpoint? http, ProxyEndpoint? https) ParseWinInetProxyServer(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return (null, null);
        var value = raw.Trim();

        if (!value.Contains('='))
        {
            var endpoint = ParseSingle(value);
            return (endpoint, endpoint);
        }

        ProxyEndpoint? http = null;
        ProxyEndpoint? https = null;
        foreach (var item in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var pair = item.Split('=', 2, StringSplitOptions.TrimEntries);
            if (pair.Length != 2) continue;
            var scheme = pair[0].ToLowerInvariant();
            var endpoint = ParseSingle(pair[1]);
            if (scheme == "http") http = endpoint;
            if (scheme == "https") https = endpoint;
        }

        return (http, https ?? http);
    }

    public static bool IsLoopback(ProxyEndpoint? endpoint)
    {
        if (endpoint is null) return false;
        return endpoint.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || (System.Net.IPAddress.TryParse(endpoint.Host.Trim('[', ']'), out var address)
                && System.Net.IPAddress.IsLoopback(address));
    }
}
