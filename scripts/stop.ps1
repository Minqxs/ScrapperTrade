[CmdletBinding()] param()
$run=Join-Path $env:LOCALAPPDATA 'ScrapperTrade\run'
foreach($name in 'web','host'){ $file=Join-Path $run "$name.pid"; if(Test-Path $file){ $id=[int](Get-Content $file); $process=Get-Process -Id $id -ErrorAction SilentlyContinue; if($process){ Stop-Process -Id $id; Write-Host "Stopped $name (PID $id)." } Remove-Item -LiteralPath $file -Force } }
Write-Host 'ScrapperTrade stopped cleanly.' -ForegroundColor Green
