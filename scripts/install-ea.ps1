[CmdletBinding(SupportsShouldProcess)]
param([string]$TerminalDataPath)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$sourceDirectory = Join-Path $root 'mt5\ScrapperTradeEA'

if (-not $TerminalDataPath) {
    $terminalRoot = Join-Path $env:APPDATA 'MetaQuotes\Terminal'
    $candidates = Get-ChildItem -LiteralPath $terminalRoot -Directory -ErrorAction SilentlyContinue |
        Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'MQL5\Experts') } |
        Sort-Object LastWriteTime -Descending
    if (-not $candidates) { throw 'No MT5 data directory found. In MT5 choose File > Open Data Folder, then pass that path with -TerminalDataPath.' }
    $TerminalDataPath = $candidates[0].FullName
}

$expertsRoot = Join-Path $TerminalDataPath 'MQL5\Experts'
if (-not (Test-Path -LiteralPath $expertsRoot)) { throw "Not an MT5 data folder: $TerminalDataPath" }
$destination = Join-Path $expertsRoot 'ScrapperTradeEA'
if ($PSCmdlet.ShouldProcess($destination, 'Install ScrapperTrade EA source and compiled binary')) {
    New-Item -ItemType Directory -Force $destination | Out-Null
    Copy-Item -LiteralPath (Join-Path $sourceDirectory 'ScrapperTradeEA.mq5') -Destination $destination -Force
    $binary = Join-Path $sourceDirectory 'ScrapperTradeEA.ex5'
    if (Test-Path -LiteralPath $binary) { Copy-Item -LiteralPath $binary -Destination $destination -Force }
    Write-Host "EA installed to $destination" -ForegroundColor Green
    Write-Host 'Attach it only to a verified DEMO account. EmergencyLocked remains true by default.' -ForegroundColor Yellow
}
