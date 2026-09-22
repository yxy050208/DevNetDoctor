using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using DevNetDoctor.Core.Models;

namespace DevNetDoctor.Core.Services;

public sealed class NetworkProbeService
{
    private const int MaxBodyBytes = 32768;
    private static readonly Uri TokenEndpoint = new("https://auth.openai.com/oauth/token");
    private static readonly Uri DeviceEndpoint = new("https://auth.openai.com/api/accounts/deviceauth/usercode");

    public async Task<IReadOnlyList<NetworkProbeResult>> ProbeOpenAiRoutesAsync(
        SystemProxyInfo systemProxy, EnvironmentProxyInfo environmentProxy, CancellationToken cancellationToken = default)
    {
        var routes = new List<(string Name, ProxyEndpoint? Proxy, AddressFamily? Family)>
        {
            ("Direct", null, null),
            ("Direct IPv4", null, AddressFamily.InterNetwork),
            ("Direct IPv6", null, AddressFamily.InterNetworkV6)
        };
        var sys = systemProxy.Enabled ? systemProxy.HttpsProxy ?? systemProxy.HttpProxy : null;
        if (sys is not null) routes.Add(("Windows system proxy", sys, null));
        if (environmentProxy.EffectiveHttpsProxy is { } env) routes.Add(("Environment proxy", env, null));

        // A separate client per probe prevents connection reuse from concealing route differences.
        var tasks = routes.SelectMany(route => new[]
        {
            ProbeAsync(route.Name, TokenEndpoint, false, route.Proxy, route.Family, cancellationToken),
            ProbeAsync(route.Name, DeviceEndpoint, true, route.Proxy, route.Family, cancellationToken)
        });
        return await Task.WhenAll(tasks);
    }

    // Internal endpoint injection supports deterministic loopback tests without touching real login flows.
    internal static async Task<NetworkProbeResult> ProbeAsync(
        string routeName, Uri uri, bool deviceAuth, ProxyEndpoint? proxy, AddressFamily? family,
        CancellationToken cancellationToken = default, TimeSpan? timeout = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sw = Stopwatch.StartNew();
        string? subject = null, issuer = null, thumbprint = null, chainStatus = null, peer = null;
        SslPolicyErrors? tlsErrors = null;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout ?? TimeSpan.FromSeconds(8));
        var token = deadline.Token;
        var endpoint = deviceAuth ? "api/accounts/deviceauth/usercode" : "oauth/token";

        NetworkProbeResult Result(bool connected, int? status, string classification, string detail, string? protocol = null) => new()
        {
            RouteName = routeName, Endpoint = endpoint, Method = deviceAuth ? "HEAD" : "POST",
            Proxy = proxy?.ToString(), TransportSucceeded = connected, HttpStatus = status,
            Classification = classification, Detail = SecretRedactor.Redact(detail),
            ElapsedMilliseconds = sw.ElapsedMilliseconds, HttpVersion = protocol, RemoteAddress = peer,
            CertificateSubject = subject, CertificateIssuer = issuer, CertificateThumbprint = thumbprint,
            CertificateChainStatus = chainStatus, TlsPolicyErrors = tlsErrors?.ToString()
        };

        try
        {
            using var handler = new SocketsHttpHandler
            {
                UseProxy = proxy is not null,
                Proxy = proxy is null ? null : new WebProxy(proxy.ToUri()),
                AllowAutoRedirect = false, UseCookies = false,
                AutomaticDecompression = DecompressionMethods.All,
                ConnectTimeout = timeout ?? TimeSpan.FromSeconds(8)
            };
            if (family.HasValue)
                handler.ConnectCallback = async (context, ct) =>
                {
                    var stream = await ConnectWithFamilyAsync(context.DnsEndPoint, family.Value, ct);
                    peer = stream.Socket.RemoteEndPoint?.ToString();
                    return stream;
                };

            handler.SslOptions = new SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = (_, certificate, chain, errors) =>
                {
                    tlsErrors = errors;
                    if (certificate is not null)
                    {
                        using var parsed = new X509Certificate2(certificate);
                        subject = SecretRedactor.Redact(parsed.Subject);
                        issuer = SecretRedactor.Redact(parsed.Issuer);
                        thumbprint = parsed.Thumbprint;
                    }
                    chainStatus = chain is null ? null : string.Join(", ", chain.ChainStatus.Select(s => s.Status.ToString()));
                    // Observe the platform verdict, never trust an invalid certificate or install a root.
                    return AcceptCertificate(errors);
                }
            };
            using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("DevNetDoctor", "0.1.1"));
            // HEAD only checks device-auth transport; it cannot initiate device authorization.
            using var request = new HttpRequestMessage(deviceAuth ? HttpMethod.Head : HttpMethod.Post, uri);
            if (!deviceAuth)
                request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "test" });
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            var status = (int)response.StatusCode;
            var challenge = response.Headers.TryGetValues("cf-mitigated", out var values)
                && values.Any(v => v.Equals("challenge", StringComparison.OrdinalIgnoreCase));
            string classification;
            if (deviceAuth)
                classification = ClassifyDeviceAuth(status);
            else
            {
                await using var bodyStream = await response.Content.ReadAsStreamAsync(token);
                var bytes = new byte[MaxBodyBytes + 1];
                var count = 0;
                while (count < bytes.Length)
                {
                    var read = await bodyStream.ReadAsync(bytes.AsMemory(count), token);
                    if (read == 0) break;
                    count += read;
                }
                classification = count > MaxBodyBytes ? "ResponseTooLarge"
                    : Classify(status, System.Text.Encoding.UTF8.GetString(bytes, 0, count));
            }
            if (challenge) classification = "HttpChallenge";
            // Export a fixed summary rather than server bodies which can echo private data.
            return Result(true, status, classification,
                deviceAuth ? "HEAD response only; device authorization and POST behavior are not verified."
                    : classification == "OAuthReachable" ? "Structured OAuth rejection of the synthetic grant received; no real token exchange was tested."
                    : "HTTP response received, but the expected synthetic OAuth rejection was not confirmed.",
                response.Version.ToString());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return Result(false, null, "Timeout", "The probe exceeded its time limit.");
        }
        catch (Exception ex) when (ex is HttpRequestException or SocketException or IOException or AuthenticationException)
        {
            sw.Stop();
            var classification = tlsErrors.HasValue && tlsErrors.Value != SslPolicyErrors.None
                ? "TlsCertificateError" : "TransportError";
            return Result(false, null, classification, string.Join(" -> ", ExceptionMessages(ex)));
        }
    }

    internal static bool AcceptCertificate(SslPolicyErrors errors) => errors == SslPolicyErrors.None;

    private static IEnumerable<string> ExceptionMessages(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException) yield return current.Message;
    }

    internal static async ValueTask<NetworkStream> ConnectWithFamilyAsync(
        DnsEndPoint endpoint, AddressFamily family, CancellationToken token)
    {
        var addresses = await Dns.GetHostAddressesAsync(endpoint.Host, family, token);
        if (addresses.Length == 0) throw new SocketException((int)SocketError.HostNotFound);
        Exception? last = null;
        foreach (var address in addresses)
        {
            token.ThrowIfCancellationRequested();
            var socket = new Socket(family, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, endpoint.Port), token);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (OperationCanceledException) { socket.Dispose(); throw; }
            catch (SocketException ex) { last = ex; socket.Dispose(); }
        }
        throw new HttpRequestException($"No reachable {family} address for {endpoint.Host}.", last);
    }

    public static string Classify(int status, string body)
    {
        // Generic text/HTML containing 'grant_type' is not evidence of an OAuth response.
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("error", out var error))
            {
                var code = error.ValueKind == JsonValueKind.String ? error.GetString() : Field(error, "code") ?? Field(error, "type");
                var message = Field(error, "message") ?? Field(root, "error_description") ?? "";
                if (code == "unsupported_country_region_territory") return "RegionBlocked";
                if (status == 400 && (code == "unsupported_grant_type"
                    || (code is "invalid_value" or "invalid_request") && message.Contains("grant_type", StringComparison.OrdinalIgnoreCase)))
                    return "OAuthReachable";
            }
        }
        catch (JsonException) { }
        return ClassifyHttp(status);
    }

    private static string? Field(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    public static string ClassifyDeviceAuth(int status) => ClassifyHttp(status);

    private static string ClassifyHttp(int status) => status switch
    {
        >= 200 and < 300 => "HttpResponse",
        >= 300 and < 400 => "HttpRedirect",
        401 or 403 => "HttpRejected",
        404 => "HttpNotFound",
        405 => "MethodNotAllowed",
        407 => "ProxyAuthenticationRequired",
        429 => "RateLimited",
        _ => $"Http{status}"
    };
}
