[CmdletBinding()] param([switch]$NoBrowser)
$ErrorActionPreference='Stop'; $root=Split-Path $PSScriptRoot -Parent; $run=Join-Path $env:LOCALAPPDATA 'ScrapperTrade\run'; New-Item -ItemType Directory -Force $run | Out-Null
$hostProject = Get-ChildItem "$root\src" -Recurse -Filter '*Host.csproj' -ErrorAction SilentlyContinue | Select-Object -First 1
if($hostProject){
  $hostArgs = "run --project `"$($hostProject.FullName)`" --no-launch-profile --urls http://127.0.0.1:5178"
  $hostProcess=Start-Process dotnet -ArgumentList $hostArgs -WorkingDirectory $root -PassThru -WindowStyle Hidden
  $hostProcess.Id | Set-Content (Join-Path $run 'host.pid')
  Write-Host "Host started (PID $($hostProcess.Id), API http://127.0.0.1:5178)."
}
$webDir=Join-Path $root 'web\scrappertrade-ui'; if(-not(Test-Path "$webDir\node_modules")){ Push-Location $webDir; try{ npm.cmd install }finally{Pop-Location} }
$webProcess=Start-Process npm.cmd -ArgumentList @('run','dev','--','--port','5173') -WorkingDirectory $webDir -PassThru -WindowStyle Hidden; $webProcess.Id | Set-Content (Join-Path $run 'web.pid')
$url='http://127.0.0.1:5173'; Start-Sleep -Seconds 2; Write-Host "ScrapperTrade control centre: $url" -ForegroundColor Green
if(-not $NoBrowser){ Start-Process $url }
