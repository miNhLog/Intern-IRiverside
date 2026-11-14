# ==============================================================================
# KHOI DONG RIVERSIDE PALACE
# AI Service: http://127.0.0.1:8000 | Backend: https://localhost:7201
# Frontend: https://localhost:7102
# ==============================================================================

$ErrorActionPreference = "Stop"

$projectRoot = $PSScriptRoot
$aiDirectory = Join-Path $projectRoot "AI_Service"
$backendDirectory = Join-Path $projectRoot "Backend\HeThongDatTiecCuoi_API"
$frontendDirectory = Join-Path $projectRoot "Frontend\HeThongDatTiecCuoi_WEB"
$aiRequirements = Join-Path $aiDirectory "requirements.txt"
$aiApplication = Join-Path $aiDirectory "app\main.py"
$aiVirtualEnvironment = Join-Path $aiDirectory ".venv"
$aiPython = Join-Path $aiVirtualEnvironment "Scripts\python.exe"
$backendProject = Join-Path $backendDirectory "HeThongDatTiecCuoi_API.csproj"
$frontendProject = Join-Path $frontendDirectory "HeThongDatTiecCuoi_WEB.csproj"
$pidFile = Join-Path $projectRoot ".riverside-dev.pids"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "[Loi] Khong tim thay .NET SDK trong PATH." -ForegroundColor Red
    exit 1
}

if (-not (Test-Path -LiteralPath $aiRequirements) -or
    -not (Test-Path -LiteralPath $aiApplication) -or
    -not (Test-Path -LiteralPath $backendProject) -or
    -not (Test-Path -LiteralPath $frontendProject)) {
    Write-Host "[Loi] Khong tim thay AI Service, Backend hoac Frontend." -ForegroundColor Red
    exit 1
}

Write-Host ">>> [1/5] Kiem tra moi truong va thu vien AI Service..." -ForegroundColor Yellow
if (-not (Test-Path -LiteralPath $aiPython)) {
    Write-Host "    Chua co AI_Service\.venv, dang tao moi truong Python 3.10..." -ForegroundColor Yellow
    if (Get-Command py -ErrorAction SilentlyContinue) {
        & py -3.10 -m venv $aiVirtualEnvironment
    }
    elseif (Get-Command python -ErrorAction SilentlyContinue) {
        & python -m venv $aiVirtualEnvironment
    }
    else {
        Write-Host "[Loi] Khong tim thay Python Launcher (py) hoac python trong PATH." -ForegroundColor Red
        exit 1
    }

    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $aiPython)) {
        Write-Host "[Loi] Khong the tao moi truong Python tai AI_Service\.venv." -ForegroundColor Red
        exit 1
    }
}

$aiDependencyCheck = "import importlib.util, sys; modules = ('fastapi', 'uvicorn', 'pydantic', 'numpy', 'pandas', 'sklearn', 'scipy', 'requests', 'chromadb', 'sentence_transformers', 'dotenv'); missing = [name for name in modules if importlib.util.find_spec(name) is None]; print('Missing AI modules: ' + ', '.join(missing)) if missing else None; sys.exit(1 if missing else 0)"
& $aiPython -c $aiDependencyCheck
if ($LASTEXITCODE -ne 0) {
    Write-Host "    Dang cai cac thu vien tu AI_Service\requirements.txt..." -ForegroundColor Yellow
    $savedErrorActionPreference = $ErrorActionPreference
    try {
        # Windows PowerShell 5 chuyen stderr cua pip thanh NativeCommandError.
        # Cho phep pip hien warning, sau do danh gia ket qua bang exit code.
        $ErrorActionPreference = "Continue"
        & $aiPython -m pip install -r $aiRequirements
        $pipExitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $savedErrorActionPreference
    }

    if ($pipExitCode -ne 0) {
        Write-Host "[Loi] Cai thu vien AI Service khong thanh cong." -ForegroundColor Red
        exit 1
    }

    & $aiPython -c $aiDependencyCheck
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[Loi] Thu vien AI Service van chua san sang sau khi cai dat." -ForegroundColor Red
        exit 1
    }
}

Write-Host ">>> [2/5] Dung cac tien trinh cu cua workspace nay..." -ForegroundColor Cyan
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
$escapedAiDirectory = [Regex]::Escape($aiDirectory)
Get-CimInstance Win32_Process |
    Where-Object {
        ($_.Name -match '^(dotnet|HeThongDatTiecCuoi_(API|WEB))(.exe)?$' -and
            $_.CommandLine -match $escapedRoot) -or
        ($_.Name -match '^python(.exe)?$' -and
            $_.CommandLine -match $escapedAiDirectory -and
            $_.CommandLine -match 'uvicorn')
    } |
    ForEach-Object {
        Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue
    }

Write-Host ">>> [3/5] Khoi dong AI Service (port 8000)..." -ForegroundColor Yellow
$aiEnvironmentCommand = ""
if (-not (Test-Path -LiteralPath (Join-Path $aiDirectory ".env"))) {
    Write-Host "    [Canh bao] Chua co AI_Service\.env; chatbot se chay offline (rule-based)." -ForegroundColor Yellow
    # rag_engine ho tro san che do TF-IDF offline qua RAG_EMBEDDINGS.
    # Khong tai SentenceTransformer tu Internet khi du an chua co .env.
    $aiEnvironmentCommand = "`$env:RAG_EMBEDDINGS = 'tfidf'; "
}
$aiCommand = "$aiEnvironmentCommand& '$aiPython' -m uvicorn app.main:app --host 127.0.0.1 --port 8000 --reload"
$aiProcess = Start-Process powershell `
    -WindowStyle Hidden `
    -WorkingDirectory $aiDirectory `
    -ArgumentList "-NoProfile", "-Command", $aiCommand `
    -PassThru

$aiReady = $false
for ($attempt = 1; $attempt -le 30; $attempt++) {
    Start-Sleep -Seconds 1
    try {
        $health = Invoke-RestMethod -Uri "http://127.0.0.1:8000/health" -TimeoutSec 2
        if ($health.status -eq "ok" -or $health.status -eq "degraded") {
            $aiReady = $true
            break
        }
    }
    catch {
        # AI Service van dang khoi dong.
    }
}

if (-not $aiReady) {
    if (Get-Process -Id $aiProcess.Id -ErrorAction SilentlyContinue) {
        & taskkill.exe /PID $aiProcess.Id /T /F 2>$null | Out-Null
    }
    Write-Host "[Loi] AI Service khong san sang tai http://127.0.0.1:8000/health." -ForegroundColor Red
    exit 1
}

Write-Host ">>> [4/5] Khoi dong Backend API (port 7201)..." -ForegroundColor Green
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

Write-Host ">>> [5/5] Khoi dong Frontend Web (port 7102)..." -ForegroundColor Magenta
$frontendProcess = Start-Process powershell `
    -WindowStyle Hidden `
    -WorkingDirectory $frontendDirectory `
    -ArgumentList "-NoProfile", "-Command", "dotnet watch run --non-interactive --launch-profile https" `
    -PassThru

@($aiProcess.Id, $backendProcess.Id, $frontendProcess.Id) | Set-Content -LiteralPath $pidFile

Write-Host "==============================================================================" -ForegroundColor Cyan
Write-Host " Riverside Palace da khoi dong AI Service, Backend va Frontend." -ForegroundColor Cyan
Write-Host " - Swagger AI:      http://127.0.0.1:8000/docs" -ForegroundColor Yellow
Write-Host " - Swagger Backend: https://localhost:7201/swagger" -ForegroundColor Green
Write-Host " - Trang chu Web:   https://localhost:7102" -ForegroundColor Magenta
Write-Host "==============================================================================" -ForegroundColor Cyan
