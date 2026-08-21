[CmdletBinding()]
param([string]$MetaEditorPath)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$source = Join-Path $root 'mt5\ScrapperTradeEA\ScrapperTradeEA.mq5'
$runtime = Join-Path $root 'runtime'
$log = Join-Path $runtime 'metaeditor-compile.log'
New-Item -ItemType Directory -Force $runtime | Out-Null

if (-not $MetaEditorPath) {
    $candidates = @(
        'C:\Program Files\MetaTrader\metaeditor64.exe',
        'C:\Program Files\MetaTrader 5\metaeditor64.exe',
        'C:\Program Files (x86)\MetaTrader 5\metaeditor64.exe'
    )
    $MetaEditorPath = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (-not $MetaEditorPath -or -not (Test-Path -LiteralPath $MetaEditorPath)) {
    throw 'MetaEditor was not found. Pass -MetaEditorPath with the full metaeditor64.exe path.'
}

Remove-Item -LiteralPath $log -Force -ErrorAction SilentlyContinue
$process = Start-Process -FilePath $MetaEditorPath -ArgumentList @("/compile:$source", "/log:$log") -Wait -PassThru
if (-not (Test-Path -LiteralPath $log)) { throw "MetaEditor did not create $log." }
$output = Get-Content -Raw -LiteralPath $log
Write-Host $output
if ($output -notmatch 'Result:\s+0 errors,\s+0 warnings') {
    throw 'EA compilation did not complete warning-free. Review the compiler output above.'
}
Write-Host 'ScrapperTrade EA compiled with 0 errors and 0 warnings.' -ForegroundColor Green

