using System.Text.RegularExpressions;

namespace DevNetDoctor.Core.Services;

public static class SecretRedactor
{
    // Apply before truncating any diagnostic output, so a partial secret cannot escape.
    public static string Redact(string? input)
    {
        var text = input ?? string.Empty;
        text = Regex.Replace(text, @"(?i)([a-z][a-z0-9+.-]*://)[^\s/@]+@", "$1[REDACTED]@");
        text = Regex.Replace(text, @"(?i)(\b(?:Bearer|Basic)\s+)[A-Za-z0-9._~+/=-]+", "$1[REDACTED]");
        text = Regex.Replace(text, "(?i)(\"(?:access_token|refresh_token|id_token|token|authorization_code|code|api_key|password|client_secret)\"\\s*:\\s*)\"(?:\\\\.|[^\"\\\\])*\"", "$1\"[REDACTED]\"");
        text = Regex.Replace(text, @"(?i)((?:set-cookie|cookie)\s*:\s*)[^\r\n]+", "$1[REDACTED]");
        text = Regex.Replace(text, @"\beyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\b", "[REDACTED_JWT]");
        text = Regex.Replace(text, @"(?i)([?&](?:code|access_token|refresh_token|id_token|token|api_key|key|password|client_secret)=)[^&#\s]+", "$1[REDACTED]");
        text = Regex.Replace(text, @"\bsk-[A-Za-z0-9_-]+", "[REDACTED_API_KEY]");
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(profile)) text = text.Replace(profile, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
        return text;
    }
}
