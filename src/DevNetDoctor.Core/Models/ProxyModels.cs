namespace DevNetDoctor.Core.Models;

public sealed record ProxyEndpoint(string Scheme, string Host, int Port)
{
    public Uri ToUri() => new UriBuilder(Scheme, Host.Trim('[', ']'), Port).Uri;
    public override string ToString() => ToUri().GetLeftPart(UriPartial.Authority);
}

public sealed class SystemProxyInfo
{
    public bool Enabled { get; init; }
    public string? RawProxyServer { get; init; }
    public string? AutoConfigUrl { get; init; }
    public ProxyEndpoint? HttpsProxy { get; init; }
    public ProxyEndpoint? HttpProxy { get; init; }
    public bool IsListening { get; init; }
    public string Source { get; init; } = "WinINET";
}

public sealed class EnvironmentProxyInfo
{
    public string? HttpProxy { get; init; }
    public string? HttpsProxy { get; init; }
    public string? AllProxy { get; init; }
    public string? NoProxy { get; init; }
    public string? UserHttpProxy { get; init; }
    public string? UserHttpsProxy { get; init; }
    public string? UserAllProxy { get; init; }
    public string? UserNoProxy { get; init; }
    public ProxyEndpoint? EffectiveHttpsProxy { get; init; }
}
