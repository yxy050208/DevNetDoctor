$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$temp = Join-Path ([IO.Path]::GetTempPath()) ('devnet-doctor-portable-' + [guid]::NewGuid().ToString('N') + '.txt')
try {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'DevNetDoctor.ps1') -ReportPath $temp | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Portable scanner returned a non-zero exit code.' }
    $report = Get-Content -LiteralPath $temp -Raw
    foreach ($secret in @('super-secret-token','sk-secret123','refresh_token":"abc123')) {
        if ($report.Contains($secret)) { throw "Report contains test secret: $secret" }
    }
    if (-not $report.Contains('Route=')) { throw 'Portable report is missing route results.' }
    Write-Host 'Portable self-test passed.'
} finally { Remove-Item -LiteralPath $temp -Force -ErrorAction SilentlyContinue }
