using System.Text;
using System.Text.RegularExpressions;

namespace DevNetDoctor.Core.Services;

public static class CodexConfigEditor
{
    private static readonly Regex FeatureHeader = new(@"^[ \t]*\[features\][ \t]*(?:#.*)?$", RegexOptions.Compiled);
    private static readonly Regex RespectLine = new(@"^([ \t]*respect_system_proxy[ \t]*=[ \t]*)(true|false)([ \t]*(?:#.*)?)$", RegexOptions.Compiled);

    // Only edit the simple table form. Refuse ambiguous TOML instead of risking other settings.
    public static string EnableInText(string text)
    {
        if (text.Contains("\"\"\"") || text.Contains("'''"))
            throw new InvalidOperationException("Multiline TOML strings require a manual edit; configuration was not changed.");
        var lines = Regex.Split(text, "\r\n|\n|\r");
        var newline = text.Contains("\r\n") ? "\r\n" : "\n";
        var featureIndex = -1;
        var sectionEnd = lines.Length;
        var respectIndex = -1;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimStart();
            if (line.StartsWith('#')) continue;
            if (FeatureHeader.IsMatch(lines[i]))
            {
                if (featureIndex >= 0) throw new InvalidOperationException("Duplicate features tables require a manual edit.");
                featureIndex = i;
                continue;
            }
            if (line.StartsWith('[') && featureIndex >= 0 && sectionEnd == lines.Length) sectionEnd = i;
            if (Regex.IsMatch(line, @"^(?:\[.*[\""']features[\""']|features\s*[.=]|[\""']features[\""']\s*[.=]|\[features\.)"))
                throw new InvalidOperationException("Dotted, quoted or inline features require a manual edit.");
            if (featureIndex >= 0 && i < sectionEnd && Regex.IsMatch(line, @"^[\""']?respect_system_proxy\b"))
            {
                if (respectIndex >= 0 || !RespectLine.IsMatch(lines[i]))
                    throw new InvalidOperationException("Ambiguous respect_system_proxy setting requires a manual edit.");
                respectIndex = i;
            }
        }
        if (respectIndex >= 0)
        {
            lines[respectIndex] = RespectLine.Replace(lines[respectIndex], "${1}true${3}");
            return string.Join(newline, lines);
        }
        var list = lines.ToList();
        if (featureIndex >= 0) list.Insert(featureIndex + 1, "respect_system_proxy = true");
        else
        {
            if (list.Count > 0 && list[^1].Length != 0) list.Add("");
            list.Add("[features]");
            list.Add("respect_system_proxy = true");
            list.Add("");
        }
        return string.Join(newline, list);
    }

    public static bool? ReadRespectSystemProxy(string configPath)
    {
        if (!File.Exists(configPath)) return null;
        var text = File.ReadAllText(configPath);
        try { EnableInText(text); } catch (InvalidOperationException) { return null; }
        var inFeatures = false;
        foreach (var line in Regex.Split(text, "\r\n|\n|\r"))
        {
            if (FeatureHeader.IsMatch(line)) { inFeatures = true; continue; }
            if (line.TrimStart().StartsWith('[')) { inFeatures = false; continue; }
            if (!inFeatures) continue;
            var match = RespectLine.Match(line);
            if (match.Success) return bool.Parse(match.Groups[2].Value);
        }
        return null;
    }

    public static string EnableRespectSystemProxy(string configPath)
    {
        configPath = Path.GetFullPath(configPath);
        var exists = File.Exists(configPath);
        var original = exists ? File.ReadAllBytes(configPath) : [];
        var hasBom = original.Length >= 3 && original[0] == 0xef && original[1] == 0xbb && original[2] == 0xbf;
        var encoding = new UTF8Encoding(hasBom, true);
        var text = encoding.GetString(original, hasBom ? 3 : 0, original.Length - (hasBom ? 3 : 0));
        var updated = EnableInText(text);
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        var backupPath = configPath + ".bak-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fffffff") + "-" + Guid.NewGuid().ToString("N")[..8];
        File.WriteAllBytes(backupPath, original);
        if (updated == text) return backupPath;
        var temporary = configPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(temporary, updated, encoding);
            if (exists) File.Replace(temporary, configPath, null);
            else File.Move(temporary, configPath);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        return backupPath;
    }
}
