[CmdletBinding()]
param(
    [switch]$RepairCodexProxy,
    [switch]$StopCodex,
    [string]$ReportPath
)

$ErrorActionPreference = 'Stop'

function Write-Section([string]$Title) {
    Write-Host "`n=== $Title ===" -ForegroundColor Cyan
}

function Secret-Redact([string]$Text) {
    if ($null -eq $Text) { return '' }
    $Text = [regex]::Replace($Text, '(?i)([a-z][a-z0-9+.-]*://)[^\s/@]+@', '$1[REDACTED]@')
    $Text = [regex]::Replace($Text, '(?i)(\b(?:Bearer|Basic)\s+)[A-Za-z0-9._~+/=-]+', '$1[REDACTED]')
    $Text = [regex]::Replace($Text, '(?i)("(?:access_token|refresh_token|id_token|token|authorization_code|code|api_key|password|client_secret)"\s*:\s*)"(?:\\.|[^"\\])*"', '$1"[REDACTED]"')
    $Text = [regex]::Replace($Text, '(?i)((?:set-cookie|cookie)\s*:\s*)[^\r\n]+', '$1[REDACTED]')
    $Text = [regex]::Replace($Text, '\beyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\b', '[REDACTED_JWT]')
    $Text = [regex]::Replace($Text, '(?i)([?&](?:code|access_token|refresh_token|id_token|token|api_key|key|password|client_secret)=)[^&#\s]+', '$1[REDACTED]')
    $Text = [regex]::Replace($Text, '\bsk-[A-Za-z0-9_-]+', '[REDACTED_API_KEY]')
    return $Text
}

function Get-WinInetProxy {
    $key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings'
    $p = Get-ItemProperty $key -ErrorAction SilentlyContinue
    [pscustomobject]@{
        Enabled       = ($p.ProxyEnable -eq 1)
        ProxyServer   = $p.ProxyServer
        AutoConfigURL = $p.AutoConfigURL
    }
}

function Parse-Proxy([string]$Raw) {
    if ([string]::IsNullOrWhiteSpace($Raw)) { return $null }
    $value = $Raw.Trim()
    if ($value.Contains('=')) {
        $pairs = @{}
        foreach ($part in $value.Split(';')) {
            $kv = $part.Split('=', 2)
            if ($kv.Count -eq 2) { $pairs[$kv[0].Trim().ToLowerInvariant()] = $kv[1].Trim() }
        }
        $value = if ($pairs.ContainsKey('https')) { $pairs['https'] } elseif ($pairs.ContainsKey('http')) { $pairs['http'] } else { $null }
    }
    if (-not $value) { return $null }
    if ($value -notmatch '^[a-zA-Z][a-zA-Z0-9+.-]*://') { $value = "http://$value" }
    try { return [uri]$value } catch { return $null }
}

function Test-LocalProxy([uri]$Proxy) {
    if (-not $Proxy) { return $false }
    if ($Proxy.Host -notin @('localhost','127.0.0.1','::1')) { return $true }
    return [bool](Get-NetTCPConnection -State Listen -LocalPort $Proxy.Port -ErrorAction SilentlyContinue)
}

function Invoke-OAuthProbe([string]$Name, [uri]$Proxy, [switch]$Direct, [ValidateSet('Any','IPv4','IPv6')][string]$AddressFamily = 'Any') {
    $tmp = [System.IO.Path]::GetTempFileName()
    try {
        $args = @('-sS','-o',$tmp,'-w','%{http_code}','-X','POST','https://auth.openai.com/oauth/token','-H','Content-Type: application/x-www-form-urlencoded','-d','grant_type=test')
        if ($AddressFamily -eq 'IPv4') { $args = @('-4') + $args }
        if ($AddressFamily -eq 'IPv6') { $args = @('-6') + $args }
        if ($Direct) { $args = @('--noproxy','*') + $args }
        elseif ($Proxy) { $args = @('-x', $Proxy.AbsoluteUri.TrimEnd('/')) + $args }
        $oldErrorAction = $ErrorActionPreference
        try { $ErrorActionPreference = 'Continue'; $status = (& curl.exe @args 2>$null | Out-String).Trim() }
        finally { $ErrorActionPreference = $oldErrorAction }
        $body = Get-Content $tmp -Raw -ErrorAction SilentlyContinue
        $class = if ($body -match 'unsupported_country_region_territory|Country, region, or territory not supported') { 'RegionBlocked' }
                 elseif ($status -eq '400' -and $body -match 'invalid_value|grant_type') { 'OAuthReachable' }
                 elseif ($status -match '^[1-5]\d{2}$') { 'Reachable' }
                 else { 'TransportError' }
        [pscustomobject]@{ Route=$Name; Proxy=if($Proxy){$Proxy.AbsoluteUri}else{'Direct'}; HTTP=$status; Class=$class; Detail=($body -replace '[\r\n]+',' ') }
    } finally {
        Remove-Item $tmp -Force -ErrorAction SilentlyContinue
    }
}

function Invoke-DeviceAuthTransportProbe([string]$Name, [uri]$Proxy) {
    $args = @('-sS','-o','NUL','-w','%{http_code}','-I','https://auth.openai.com/api/accounts/deviceauth/usercode')
    if ($Proxy) { $args = @('-x', $Proxy.AbsoluteUri.TrimEnd('/')) + $args }
    $oldErrorAction = $ErrorActionPreference
    try { $ErrorActionPreference = 'Continue'; $status = (& curl.exe @args 2>$null | Out-String).Trim() }
    finally { $ErrorActionPreference = $oldErrorAction }
    $class = if ($status -match '^[1-5]\d{2}$') { 'HttpResponse' } else { 'TransportError' }
    [pscustomobject]@{ Route=$Name; Proxy=if($Proxy){$Proxy.AbsoluteUri}else{'Direct'}; HTTP=$status; Class=$class; Detail='HEAD only; device authorization was not attempted.' }
}

function Get-CodexConfigPath {
    $codexHome = $env:CODEX_HOME
    if ([string]::IsNullOrWhiteSpace($codexHome)) { $codexHome = Join-Path $HOME '.codex' }
    Join-Path $codexHome 'config.toml'
}

function Get-RespectSystemProxy([string]$Path) {
    if (-not (Test-Path $Path)) { return $null }
    $inFeatures = $false
    foreach ($line in Get-Content $Path) {
        if ($line -match '^\s*\[features\]\s*(?:#.*)?$') { $inFeatures = $true; continue }
        if ($line -match '^\s*\[[^\]]+\]') { $inFeatures = $false; continue }
        if ($inFeatures -and $line -match '^\s*respect_system_proxy\s*=\s*(true|false)') { return ($Matches[1] -eq 'true') }
    }
    return $null
}

function Enable-RespectSystemProxy([string]$Path) {
    $dir = Split-Path -Parent $Path
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    if (-not (Test-Path $Path)) { New-Item -ItemType File -Path $Path -Force | Out-Null }
    $backup = "$Path.bak-$(Get-Date -Format yyyyMMdd-HHmmss-fffffff)-$([guid]::NewGuid().ToString('N').Substring(0,8))"
    Copy-Item $Path $backup
    $lines = [System.Collections.Generic.List[string]](Get-Content $Path)
    $features = -1
    for ($i=0; $i -lt $lines.Count; $i++) { if ($lines[$i] -match '^\s*\[features\]\s*(?:#.*)?$') { $features=$i; break } }
    if ($features -lt 0) {
        if ($lines.Count -gt 0 -and $lines[$lines.Count-1].Trim()) { $lines.Add('') }
        $lines.Add('[features]')
        $lines.Add('respect_system_proxy = true')
    } else {
        $end = $lines.Count
        for ($i=$features+1; $i -lt $lines.Count; $i++) { if ($lines[$i] -match '^\s*\[[^\]]+\]') { $end=$i; break } }
        $found = -1
        for ($i=$features+1; $i -lt $end; $i++) { if ($lines[$i] -match '^\s*respect_system_proxy\s*=') { $found=$i; break } }
        if ($found -ge 0) { $lines[$found] = 'respect_system_proxy = true' } else { $lines.Insert($features+1,'respect_system_proxy = true') }
    }
    $temporary = "$Path.tmp-$([guid]::NewGuid().ToString('N'))"
    try {
        Set-Content -Path $temporary -Value $lines -Encoding utf8
        Move-Item -LiteralPath $temporary -Destination $Path -Force
    } finally { Remove-Item -LiteralPath $temporary -Force -ErrorAction SilentlyContinue }
    return $backup
}

if ($StopCodex) {
    Get-Process | Where-Object { $_.ProcessName -match 'codex' } | Stop-Process -Force
    Write-Host 'Stopped Codex processes.'
}

$configPath = Get-CodexConfigPath
if ($RepairCodexProxy) {
    $backup = Enable-RespectSystemProxy $configPath
    Write-Host "Enabled respect_system_proxy. Backup: $backup" -ForegroundColor Green
}

$system = Get-WinInetProxy
$systemProxy = Parse-Proxy $system.ProxyServer

$proxyRaw = $env:HTTPS_PROXY
if ([string]::IsNullOrWhiteSpace($proxyRaw)) { $proxyRaw = $env:HTTP_PROXY }
if ([string]::IsNullOrWhiteSpace($proxyRaw)) { $proxyRaw = $env:ALL_PROXY }
$processProxy = Parse-Proxy $proxyRaw

Write-Section 'Windows proxy'
$system | Format-List
Write-Host "Parsed proxy: $systemProxy"
if ($systemProxy) { Write-Host "Listener detected: $(Test-LocalProxy $systemProxy)" }

Write-Section 'Proxy environment'
[pscustomobject]@{
    HTTP_PROXY=$env:HTTP_PROXY; HTTPS_PROXY=$env:HTTPS_PROXY; ALL_PROXY=$env:ALL_PROXY; NO_PROXY=$env:NO_PROXY
    User_HTTP_PROXY=[Environment]::GetEnvironmentVariable('HTTP_PROXY','User')
    User_HTTPS_PROXY=[Environment]::GetEnvironmentVariable('HTTPS_PROXY','User')
} | Format-List

Write-Section 'WinHTTP'
netsh winhttp show proxy

Write-Section 'OpenAI OAuth route matrix'
$probes = @()
$probes += Invoke-OAuthProbe -Name 'Direct' -Direct
$probes += Invoke-OAuthProbe -Name 'Direct IPv4' -Direct -AddressFamily IPv4
$probes += Invoke-OAuthProbe -Name 'Direct IPv6' -Direct -AddressFamily IPv6
$probes += Invoke-DeviceAuthTransportProbe -Name 'Device auth (direct)' -Proxy $null
if ($system.Enabled -and $systemProxy) { $probes += Invoke-OAuthProbe -Name 'Windows system proxy' -Proxy $systemProxy }
if ($processProxy -and $processProxy.AbsoluteUri -ne $systemProxy.AbsoluteUri) { $probes += Invoke-OAuthProbe -Name 'Environment proxy' -Proxy $processProxy }
$probes | Format-Table Route,Proxy,HTTP,Class -AutoSize

Write-Section 'Codex'
$codex = Get-Command codex -ErrorAction SilentlyContinue
if ($codex) {
    codex --version
    codex login status
    Write-Host "Config: $configPath"
    Write-Host "respect_system_proxy: $(Get-RespectSystemProxy $configPath)"
    $auth = Join-Path (Split-Path -Parent $configPath) 'auth.json'
    if (Test-Path $auth) {
        $item = Get-Item $auth
        Write-Host "auth.json: exists; length=$($item.Length); lastWrite=$($item.LastWriteTime) (contents not read)"
    } else { Write-Host 'auth.json: not found' }
} else { Write-Host 'Codex not found.' }

Write-Section 'CCSwitch'
$cc = Get-Process | Where-Object { $_.ProcessName -match 'ccswitch|cc-switch' }
if ($cc) {
    $cc | Select-Object ProcessName,Id,Path | Format-Table -AutoSize
    $ids = $cc.Id
    netstat -ano -p tcp | Select-String 'LISTENING' | ForEach-Object {
        $line = $_.Line.Trim()
        $parts = $line -split '\s+'
        if ($parts.Count -ge 5 -and [int]$parts[-1] -in $ids) { $line }
    }
} else { Write-Host 'CCSwitch process not detected.' }

Write-Section 'Diagnosis'
$direct = $probes | Where-Object Route -eq 'Direct' | Select-Object -First 1
$workingProxy = $probes | Where-Object { $_.Route -ne 'Direct' -and $_.Class -eq 'OAuthReachable' } | Select-Object -First 1
if ($direct.Class -eq 'RegionBlocked' -and $workingProxy) {
    Write-Warning "Direct route and proxy route differ. Software expected to use the configured proxy may be bypassing it."
}
if ($systemProxy -and $processProxy -and ($systemProxy.Host -ne $processProxy.Host -or $systemProxy.Port -ne $processProxy.Port)) {
    Write-Warning "Windows proxy ($systemProxy) differs from process proxy ($processProxy): possible stale proxy port."
}
if ($codex -and (Get-RespectSystemProxy $configPath) -ne $true -and $system.Enabled) {
    Write-Warning "Codex system proxy support is not enabled. Run: .\DevNetDoctor.ps1 -RepairCodexProxy"
}

if ($ReportPath) {
    $report = @()
    $report += "DevNet Doctor portable report - $(Get-Date -Format s)"
    $report += "WindowsProxy=$($system.ProxyServer); Enabled=$($system.Enabled); PAC=$($system.AutoConfigURL)"
    $report += "ProcessHTTPS_PROXY=$env:HTTPS_PROXY; ProcessHTTP_PROXY=$env:HTTP_PROXY; NO_PROXY=$env:NO_PROXY"
    foreach ($p in $probes) { $report += "Route=$($p.Route); Proxy=$($p.Proxy); HTTP=$($p.HTTP); Class=$($p.Class); Detail=$($p.Detail)" }
    if ($codex) { $report += "Codex=$(codex --version); Login=$(codex login status); respect_system_proxy=$(Get-RespectSystemProxy $configPath)" }
    $reportText = Secret-Redact (($report -join [Environment]::NewLine))
    Set-Content -Path $ReportPath -Value $reportText -Encoding utf8
    Write-Host "Report saved to $ReportPath" -ForegroundColor Green
}
