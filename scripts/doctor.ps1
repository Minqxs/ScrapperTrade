[CmdletBinding()]
param([switch]$Json)
$ErrorActionPreference = 'SilentlyContinue'
$checks = @()
function Add-Check($Name, $Required, $Command, $Hint) {
    $resolved = Get-Command $Command
    $checks += [pscustomobject]@{ Name=$Name; Required=$Required; Found=[bool]$resolved; Version=if($resolved){ (& $Command --version 2>$null | Select-Object -First 1) }else{ $null }; Hint=$Hint }
    Set-Variable -Name checks -Value $checks -Scope 1
}
Add-Check '.NET SDK' $true 'dotnet' 'Install the current .NET SDK from https://dotnet.microsoft.com/download'
Add-Check 'Node.js' $true 'node' 'Install Node.js LTS from https://nodejs.org/'
Add-Check 'npm' $true 'npm.cmd' 'npm is included with Node.js.'
Add-Check 'Git' $true 'git' 'Install Git for Windows from https://git-scm.com/'
$mt5Paths = @("$env:ProgramFiles\MetaTrader 5\terminal64.exe", "${env:ProgramFiles(x86)}\MetaTrader 5\terminal64.exe")
$mt5 = $mt5Paths | Where-Object { Test-Path $_ } | Select-Object -First 1
$checks += [pscustomobject]@{ Name='MetaTrader 5'; Required=$false; Found=[bool]$mt5; Version=$null; Hint='Optional: install MT5 and sign in to a DEMO account. Simulator mode works without it.' }
$checks += [pscustomobject]@{ Name='MT5 account verification'; Required=$false; Found=$false; Version=$null; Hint='Checked at runtime by the EA. REAL, unknown, and disconnected accounts are rejected.' }
if ($Json) { $checks | ConvertTo-Json; exit 0 }
Write-Host "ScrapperTrade machine doctor" -ForegroundColor Cyan
Write-Host "Safety: simulator is the default; real-money execution remains locked.`n"
foreach ($check in $checks) {
    $mark = if($check.Found){'[OK]'}elseif($check.Required){'[MISSING]'}else{'[OPTIONAL]'}
    $colour = if($check.Found){'Green'}elseif($check.Required){'Red'}else{'Yellow'}
    $versionText = if($check.Version){ " - $($check.Version)" } else { '' }
    Write-Host ($mark.PadRight(11) + $check.Name + $versionText) -ForegroundColor $colour
    if(-not $check.Found){ Write-Host "            $($check.Hint)" -ForegroundColor DarkGray }
}
$missing = @($checks | Where-Object { $_.Required -and -not $_.Found })
if($missing.Count){ Write-Host "`nInstall the missing required tools, then run this command again." -ForegroundColor Red; exit 1 }
Write-Host "`nCore prerequisites are ready. MT5 can be configured later in the first-run wizard." -ForegroundColor Green
