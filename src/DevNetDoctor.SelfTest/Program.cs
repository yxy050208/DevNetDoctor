using DevNetDoctor.Core.Services;

namespace DevNetDoctor.SelfTest;

internal static class Program
{
    private static int _failures;

    private static void Main()
    {
        TestProxyParser();
        TestRedactor();
        TestCodexConfigEditor();
        TestRouteClassification();
        TestRedactorExtended();

        if (_failures == 0)
        {
            Console.WriteLine("All self-tests passed.");
            return;
        }

        Console.Error.WriteLine($"{_failures} self-test(s) failed.");
        Environment.ExitCode = 1;
    }

    private static void TestProxyParser()
    {
        var single = ProxyParser.ParseSingle("127.0.0.1:22626");
        Assert(single is not null && single.Host == "127.0.0.1" && single.Port == 22626, "Parse simple proxy endpoint");

        var (_, https) = ProxyParser.ParseWinInetProxyServer("http=127.0.0.1:8080;https=127.0.0.1:8443");
        Assert(https is not null && https.Port == 8443, "Parse WinINET scheme-specific endpoint");
    }

    private static void TestRedactor()
    {
        var sample = "Authorization: Bearer super-secret-token\n{\"refresh_token\":\"abc123\"}\nhttps://x.test/cb?code=secret-code&state=ok";
        var redacted = SecretRedactor.Redact(sample);
        Assert(!redacted.Contains("super-secret-token") && !redacted.Contains("abc123") && !redacted.Contains("secret-code"), "Redact common secrets");
    }

    private static void TestCodexConfigEditor()
    {
        var dir = Path.Combine(Path.GetTempPath(), "DevNetDoctor-SelfTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "config.toml");
            File.WriteAllText(path, "model = \"gpt-test\"\n[features]\ngoals = true\n");
            var backup = CodexConfigEditor.EnableRespectSystemProxy(path);
            var text = File.ReadAllText(path);
            Assert(File.Exists(backup), "Create config backup");
            Assert(text.Contains("respect_system_proxy = true"), "Insert Codex proxy feature");
            Assert(CodexConfigEditor.ReadRespectSystemProxy(path) == true, "Read Codex proxy feature");
            var before = File.ReadAllText(path);
            var secondBackup = CodexConfigEditor.EnableRespectSystemProxy(path);
            Assert(File.Exists(secondBackup) && File.ReadAllText(path) == before, "Idempotent Codex repair");
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    private static void TestRouteClassification()
    {
        Assert(NetworkProbeService.Classify(400, "{\"error\":{\"code\":\"unsupported_grant_type\",\"message\":\"grant_type is invalid\"}}") == "OAuthReachable", "Classify OAuth validation response");
        Assert(NetworkProbeService.Classify(403, "{\"error\":\"unsupported_country_region_territory\"}") == "RegionBlocked", "Classify region block");
        Assert(NetworkProbeService.Classify(200, "ok") == "HttpResponse", "Classify generic reachable response");
        Assert(NetworkProbeService.Classify(400, "grant_type appears in an HTML error page") != "OAuthReachable", "Do not classify arbitrary text as OAuth");
        Assert(NetworkProbeService.ClassifyDeviceAuth(400) == "Http400", "Classify device-auth transport response");
        Assert(NetworkProbeService.AcceptCertificate(System.Net.Security.SslPolicyErrors.None), "Accept valid TLS policy");
        Assert(!NetworkProbeService.AcceptCertificate(System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch), "Reject invalid TLS policy");
    }

    private static void TestRedactorExtended()
    {
        var sample = "https://u:p@example.test/cb?code=abc&x=1 sk-secret123 {\"api_key\":\"xyz\"}";
        var redacted = SecretRedactor.Redact(sample);
        Assert(!redacted.Contains("u:p") && !redacted.Contains("abc") && !redacted.Contains("sk-secret123") && !redacted.Contains("xyz"), "Redact URL and API secrets");
    }

    private static void Assert(bool condition, string name)
    {
        if (condition)
        {
            Console.WriteLine($"PASS: {name}");
            return;
        }

        _failures++;
        Console.Error.WriteLine($"FAIL: {name}");
    }
}
