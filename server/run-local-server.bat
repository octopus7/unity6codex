@echo off
setlocal

set "SERVER_ROOT=%~dp0"
set "PROJECT_PATH=%SERVER_ROOT%TopdownShooter.Server\TopdownShooter.Server.csproj"
set "APPSETTINGS_PATH=%SERVER_ROOT%TopdownShooter.Server\appsettings.json"
set "PORT=7777"

if not exist "%PROJECT_PATH%" (
    echo [ERROR] Server project file not found:
    echo %PROJECT_PATH%
    exit /b 1
)

if exist "%APPSETTINGS_PATH%" (
    for /f "usebackq delims=" %%p in (`powershell -NoProfile -Command ^
        "$port=7777; try{ $cfg=Get-Content -Raw '%APPSETTINGS_PATH%' ^| ConvertFrom-Json; if($cfg.listenPort){ $port=[int]$cfg.listenPort } } catch {}; Write-Output $port"`) do (
        set "PORT=%%p"
    )
)

powershell -NoProfile -Command "if(Get-NetTCPConnection -LocalPort %PORT% -State Listen -ErrorAction SilentlyContinue){exit 0}else{exit 1}"
if %ERRORLEVEL% EQU 0 (
    echo [INFO] Server already running on port %PORT%.
    exit /b 0
)

echo [INFO] Starting TopdownShooter.Server on port %PORT%...
start "TopdownShooter.Server" cmd /k "cd /d ""%SERVER_ROOT%"" && dotnet run --project ""%PROJECT_PATH%"""
exit /b 0

