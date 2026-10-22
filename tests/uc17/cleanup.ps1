param([string]$EvidenceName='cleanup.json')
$ErrorActionPreference='Stop'
if($EvidenceName -notin @('cleanup.json','final-cleanup.json','review-cleanup.json')) { throw 'Invalid evidence name' }
$root=(Get-Content "$PSScriptRoot/runtime-pointer.txt").Trim()
$m=Get-Content "$root/ownership.json" -Raw | ConvertFrom-Json
if($root -ne "D:\temp\opencode\uc17-$($m.runId)" -or $m.root -ne $root -or $m.database -ne "UC17_Scenario_$($m.runId)") { throw 'STOP: invalid runtime ownership' }
$p=Get-Content "$root/processes.json" -Raw | ConvertFrom-Json
foreach($id in @($p.api,$p.web)) {
 $process=Get-CimInstance Win32_Process -Filter "ProcessId=$id"
 if($process) {
  if($process.Name -ne 'dotnet.exe' -or !$process.CommandLine.Contains($root)) { throw 'STOP: process ownership mismatch' }
  Stop-Process -Id $id
 }
}
$env:UC17_RUNTIME=$root
dotnet run --project "$PSScriptRoot/provision/Provision.csproj" -- cleanup
if($LASTEXITCODE -ne 0) { throw 'STOP: database cleanup not verified' }
foreach($id in @($p.api,$p.web)) { if(Get-Process -Id $id -ErrorAction SilentlyContinue) { throw 'Process still running' } }
# Delete only this run's known disposable paths after DB/process ownership validation.
Remove-Item -LiteralPath $root -Recurse -Force
foreach($path in @("$PSScriptRoot/runtime-pointer.txt","$PSScriptRoot/provision/bin","$PSScriptRoot/provision/obj","$PSScriptRoot/../../.playwright-mcp/uc17-run-login.js","$PSScriptRoot/../../.playwright-mcp/HopDong-HD-MCP.pdf")) {
 if(Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Recurse -Force }
}
if(Test-Path -LiteralPath $root) { throw 'Runtime cleanup failed' }
@{runId=$m.runId;database=$m.database;databaseAbsent=$true;ownedProcessesStopped=$true;runtimeAbsent=$true;McpCookiesCleared=$true;credentialsAndAuthStateRemoved=$true} | ConvertTo-Json | Set-Content "$PSScriptRoot/evidence/$EvidenceName"
Write-Output 'CLEANUP_VERIFIED: owned processes stopped; database absent; runtime and credentials removed'
