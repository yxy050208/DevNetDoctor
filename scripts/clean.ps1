$root = Split-Path -Parent $PSScriptRoot
Get-ChildItem $root -Directory -Recurse -Force |
    Where-Object { $_.Name -in @('bin','obj') } |
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $root 'artifacts') -Recurse -Force -ErrorAction SilentlyContinue
