$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path "$PSScriptRoot/../..").Path
$resume = Test-Path "$PSScriptRoot/runtime-pointer.txt"
if ($resume) { $root=(Get-Content "$PSScriptRoot/runtime-pointer.txt").Trim(); $run=(Get-Content "$root/ownership.json" -Raw | ConvertFrom-Json).runId }
else {
 $run = [Guid]::NewGuid().ToString('N'); $root = "D:\temp\opencode\uc17-$run"
 New-Item -ItemType Directory -Path $root | Out-Null
 @{ runId=$run; database="UC17_Scenario_$run"; root=$root; instance='(localdb)\MSSQLLocalDB' } | ConvertTo-Json | Set-Content "$root/ownership.json"
 Set-Content "$PSScriptRoot/runtime-pointer.txt" $root
}
$env:UC17_RUNTIME=$root
if (!$resume) { dotnet run --project "$PSScriptRoot/provision/Provision.csproj" -- setup; if ($LASTEXITCODE -ne 0) { throw 'Provision failed; ownership-safe cleanup required' } }
$bytes=New-Object byte[] 48; $rng=[Security.Cryptography.RandomNumberGenerator]::Create(); $rng.GetBytes($bytes); $rng.Dispose(); $key=[Convert]::ToBase64String($bytes)
$base=@{ Logging=@{LogLevel=@{Default='Warning'}}; DevelopmentAccounts=@{Enabled=$false}; Jwt=@{Issuer='UC17';Audience='UC17';Key=$key}; AccountProvisioning=@{DefaultStaffPassword="Uc17!$run"}; AiService=@{BaseUrl='http://127.0.0.1:1/';TimeoutSeconds=1}; Smtp=@{Host='127.0.0.1';Port=1}; MailSettings=@{Host='127.0.0.1';Port=1}; ConnectionStrings=@{DefaultConnection=(Get-Content "$root/connection.txt" -Raw).Trim()}; RiversideApi=@{BaseUrl='https://localhost:17401/'}; Google=@{ClientId='';ClientSecret=''}; Kestrel=@{Certificates=@{Default=@{Subject='localhost';Store='My';Location='CurrentUser';AllowInvalid=$true}}} }
foreach($kind in @('api','web')) { New-Item -ItemType Directory "$root/$kind" | Out-Null; $base | ConvertTo-Json -Depth 8 | Set-Content "$root/$kind/appsettings.json" }
# This is the entire configuration root; no repository appsettings or user-secrets are loaded in ScenarioTesting.
# Reject inherited application overrides rather than accidentally using them.
$overrides=Get-ChildItem Env: | Where-Object { $_.Name -match '^(ASPNETCORE_|DOTNET_|ConnectionStrings__|Jwt__|RiversideApi__|Kestrel__|DevelopmentAccounts__|Google__|Smtp__|MailSettings__|AiService__)' }
if($overrides) { throw 'Inherited application overrides detected. Do not startup.' }
$env:ASPNETCORE_ENVIRONMENT='ScenarioTesting'
$env:DOTNET_ENVIRONMENT='ScenarioTesting'
$env:LOCALAPPDATA="$root/localappdata"
$env:APPDATA="$root/appdata"
New-Item -ItemType Directory $env:LOCALAPPDATA,$env:APPDATA | Out-Null
$api=Start-Process dotnet -ArgumentList @("$repo/Backend/HeThongDatTiecCuoi_API/bin/Debug/net8.0/HeThongDatTiecCuoi_API.dll",'--contentRoot',"$root/api",'--urls','https://localhost:17401') -PassThru -RedirectStandardOutput "$root/api.log" -RedirectStandardError "$root/api.err"
$web=Start-Process dotnet -ArgumentList @("$repo/Frontend/HeThongDatTiecCuoi_WEB/bin/Debug/net8.0/HeThongDatTiecCuoi_WEB.dll",'--contentRoot',"$root/web",'--webroot',"$repo/Frontend/HeThongDatTiecCuoi_WEB/wwwroot",'--urls','https://localhost:17402') -PassThru -RedirectStandardOutput "$root/web.log" -RedirectStandardError "$root/web.err"
@{api=$api.Id;web=$web.Id;root=$root} | ConvertTo-Json | Set-Content "$root/processes.json"
Write-Output "RUNTIME=$root"
