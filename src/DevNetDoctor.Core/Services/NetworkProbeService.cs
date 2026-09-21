using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using DevNetDoctor.Core.Models;

namespace DevNetDoctor.Core.Services;

public sealed class NetworkProbeService
{
    private static readonly Uri OpenAiTokenEndpoint = new("https://auth.openai.com/oauth/token");

    public async Task<IReadOnlyList<NetworkProbeResult>> ProbeOpenAiRoutesAsync(
        SystemProxyInfo systemProxy,
        EnvironmentProxyInfo environmentProxy,
        CancellationToken cancellationToken = default)
    {
        var results = new List<NetworkProbeResult>
        {
            await ProbeOpenAiOAuthAsync("Direct", null, useProxy: false, cancellationToken)
        };

        var sys = systemProxy.Enabled ? systemProxy.HttpsProxy ?? systemProxy.HttpProxy : null;
        if (sys is not null)
            results.Add(await ProbeOpenAiOAuthAsync("Windows system proxy", sys, useProxy: true, cancellationToken));

        var env = environmentProxy.EffectiveHttpsProxy;
        if (env is not null)
            results.Add(await ProbeOpenAiOAuthAsync("Environment proxy", env, useProxy: true, cancellationToken));

        return results;
    }

    private static bool SameEndpoint(ProxyEndpoint? a, ProxyEndpoint? b)
        => a is not null && b is not null
        && a.Host.Equals(b.Host, StringComparison.OrdinalIgnoreCase)
        && a.Port == b.Port
        && a.Scheme.Equals(b.Scheme, StringComparison.OrdinalIgnoreCase);

    private static async Task<NetworkProbeResult> ProbeOpenAiOAuthAsync(
        string routeName,
        ProxyEndpoint? proxy,
        bool useProxy,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var handler = new HttpClientHandler
            {
                UseProxy = useProxy,
                Proxy = useProxy && proxy is not null ? new WebProxy(proxy.ToUri()) : null,
                AutomaticDecompression = DecompressionMethods.All,
                AllowAutoRedirect = false,
                UseCookies = false
            };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(8) };
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("DevNetDoctor", "0.1"));

            using var request = new HttpRequestMessage(HttpMethod.Post, OpenAiTokenEndpoint)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "test" })
            };
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
            var body = SecretRedactor.Redact(await response.Content.ReadAsStringAsync(cancellationToken));
            var classification = Classify((int)response.StatusCode, body);
            body = body.Length > 500 ? body[..500] + "…" : body;
            sw.Stop();

            return new NetworkProbeResult
            {
                RouteName = routeName,
                Proxy = proxy?.ToString(),
                TransportSucceeded = true,
                HttpStatus = (int)response.StatusCode,
                Classification = classification,
                Detail = Compact(body),
                ElapsedMilliseconds = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new NetworkProbeResult
            {
                RouteName = routeName,
                Proxy = proxy?.ToString(),
                TransportSucceeded = false,
                Classification = "TransportError",
                Detail = SecretRedactor.Redact(ex.Message),
                ElapsedMilliseconds = sw.ElapsedMilliseconds
            };
        }
    }

    public static string Classify(int status, string body)
    {
        if (body.Contains("unsupported_country_region_territory", StringComparison.OrdinalIgnoreCase)
            || body.Contains("Country, region, or territory not supported", StringComparison.OrdinalIgnoreCase))
            return "RegionBlocked";

        if (status == 400 && (body.Contains("invalid_value", StringComparison.OrdinalIgnoreCase)
            || body.Contains("grant_type", StringComparison.OrdinalIgnoreCase)))
            return "OAuthReachable";

        if (status is >= 200 and < 500) return "Reachable";
        return $"Http{status}";
    }

    private static string Compact(string text)
        => string.Join(" ", text.Split(new[] { '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
