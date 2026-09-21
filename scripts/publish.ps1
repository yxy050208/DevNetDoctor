param(
    [ValidateSet("win-x64", "win-arm64")]
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root "artifacts\$Runtime"

Push-Location $root
try {
    dotnet publish .\src\DevNetDoctor.App\DevNetDoctor.App.csproj `
        -c Release `
        -r $Runtime `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -o $out
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }

    Write-Host "Published to $out"
} finally {
    Pop-Location
}
