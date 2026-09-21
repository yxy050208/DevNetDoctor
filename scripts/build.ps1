$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    dotnet restore .\DevNetDoctor.sln
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
    dotnet build .\DevNetDoctor.sln -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    dotnet run --project .\src\DevNetDoctor.SelfTest\DevNetDoctor.SelfTest.csproj -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Self-tests failed.' }
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-portable.ps1
    if ($LASTEXITCODE -ne 0) { throw 'Portable self-tests failed.' }
} finally {
    Pop-Location
}
