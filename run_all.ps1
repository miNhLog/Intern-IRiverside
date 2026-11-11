# ==============================================================================
# KHOI DONG RIVERSIDE PALACE (KHONG CHAY AI SERVICE)
# Backend API: https://localhost:7201 | Frontend: https://localhost:7102
# ==============================================================================

$ErrorActionPreference = "Stop"

$projectRoot = $PSScriptRoot
$backendDirectory = Join-Path $projectRoot "Backend\HeThongDatTiecCuoi_API"
$frontendDirectory = Join-Path $projectRoot "Frontend\HeThongDatTiecCuoi_WEB"
$backendProject = Join-Path $backendDirectory "HeThongDatTiecCuoi_API.csproj"
$frontendProject = Join-Path $frontendDirectory "HeThongDatTiecCuoi_WEB.csproj"
$pidFile = Join-Path $projectRoot ".riverside-dev.pids"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "[Loi] Khong tim thay .NET SDK trong PATH." -ForegroundColor Red
    exit 1
}

if (-not (Test-Path -LiteralPath $backendProject) -or
    -not (Test-Path -LiteralPath $frontendProject)) {
    Write-Host "[Loi] Khong tim thay project Backend hoac Frontend." -ForegroundColor Red
    exit 1
}

Write-Host ">>> [1/3] Dung cac tien trinh cu cua workspace nay..." -ForegroundColor Cyan
$savedProcessIds = @()
if (Test-Path -LiteralPath $pidFile) {
    $savedProcessIds = Get-Content -LiteralPath $pidFile -ErrorAction SilentlyContinue |
        Where-Object { $_ -match '^\d+$' } |
        ForEach-Object { [int]$_ }
}

foreach ($savedProcessId in $savedProcessIds) {
    if (Get-Process -Id $savedProcessId -ErrorAction SilentlyContinue) {
        & taskkill.exe /PID $savedProcessId /T /F 2>$null | Out-Null
    }
}

Remove-Item -LiteralPath $pidFile -Force -ErrorAction SilentlyContinue

# Don dep cac ban chay cu duoc tao boi phien ban script truoc day.
$escapedRoot = [Regex]::Escape($projectRoot)
Get-CimInstance Win32_Process |
    Where-Object {
        $_.Name -match '^(dotnet|HeThongDatTiecCuoi_(API|WEB))(.exe)?$' -and
        $_.CommandLine -match $escapedRoot
    } |
    ForEach-Object {
        Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue
    }

Write-Host ">>> [2/3] Khoi dong Backend API (port 7201)..." -ForegroundColor Green
$backendCommand = @"
`$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet watch run --non-interactive --no-launch-profile -- --urls 'https://localhost:7201;http://localhost:5201'
"@
$backendProcess = Start-Process powershell `
    -WindowStyle Hidden `
    -WorkingDirectory $backendDirectory `
    -ArgumentList "-NoProfile", "-Command", $backendCommand `
    -PassThru

Start-Sleep -Seconds 3

Write-Host ">>> [3/3] Khoi dong Frontend Web (port 7102)..." -ForegroundColor Magenta
$frontendProcess = Start-Process powershell `
    -WindowStyle Hidden `
    -WorkingDirectory $frontendDirectory `
    -ArgumentList "-NoProfile", "-Command", "dotnet watch run --non-interactive --launch-profile https" `
    -PassThru

@($backendProcess.Id, $frontendProcess.Id) | Set-Content -LiteralPath $pidFile

Write-Host "==============================================================================" -ForegroundColor Cyan
Write-Host " Riverside Palace dang khoi dong voi che do tu dong cap nhat code." -ForegroundColor Cyan
Write-Host " - Swagger Backend: https://localhost:7201/swagger" -ForegroundColor Green
Write-Host " - Trang chu Web:   https://localhost:7102" -ForegroundColor Magenta
Write-Host " - AI Service:      Khong khoi dong" -ForegroundColor DarkGray
Write-Host "==============================================================================" -ForegroundColor Cyan
