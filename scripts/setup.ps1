[CmdletBinding()] param([switch]$SkipFrontend)
$ErrorActionPreference='Stop'
$root = Split-Path $PSScriptRoot -Parent
Write-Host 'Setting up ScrapperTrade (simulator-first)...' -ForegroundColor Cyan
& "$PSScriptRoot\doctor.ps1"
if(-not $SkipFrontend){ Push-Location "$root\web\scrappertrade-ui"; try { npm.cmd install; npm.cmd run build } finally { Pop-Location } }
$solution = Get-ChildItem $root -Filter '*.sln' | Select-Object -First 1
if($solution){ dotnet restore $solution.FullName; dotnet build $solution.FullName --no-restore }
$runtime = Join-Path $env:LOCALAPPDATA 'ScrapperTrade'
New-Item -ItemType Directory -Force -Path $runtime, (Join-Path $runtime 'logs') | Out-Null
Write-Host "`nSetup complete. Runtime data: $runtime" -ForegroundColor Green
Write-Host 'Start with: .\scripts\start.ps1'
