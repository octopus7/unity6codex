@echo off
setlocal

set "SERVER_ROOT=%~dp0"
set "PROJECT_PATH=%SERVER_ROOT%TopdownShooter.Server\TopdownShooter.Server.csproj"
set "APPSETTINGS_PATH=%SERVER_ROOT%TopdownShooter.Server\appsettings.json"
set "PORT=7777"
set "HEADLESS=%TOPDOWN_HEADLESS%"
set "LOG_PATH=%TOPDOWN_LOG_PATH%"

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
    if "%HEADLESS%"=="1" (
        if not defined LOG_PATH (
            set "LOG_PATH=%SERVER_ROOT%.logs\server.log"
        )

        for %%I in ("%LOG_PATH%") do set "LOG_DIR=%%~dpI"
        if not exist "%LOG_DIR%" (
            mkdir "%LOG_DIR%" >nul 2>&1
        )

        if "%TOPDOWN_TRUNCATE_LOG%"=="1" (
            type nul > "%LOG_PATH%"
        )

        call :LogLine "[INFO] Server already running on port %PORT%."
    ) else (
        echo [INFO] Server already running on port %PORT%.
    )

    exit /b 0
)

if "%HEADLESS%"=="1" (
    if not defined LOG_PATH (
        set "LOG_PATH=%SERVER_ROOT%.logs\server.log"
    )

    for %%I in ("%LOG_PATH%") do set "LOG_DIR=%%~dpI"
    if not exist "%LOG_DIR%" (
        mkdir "%LOG_DIR%" >nul 2>&1
    )

    if "%TOPDOWN_TRUNCATE_LOG%"=="1" (
        type nul > "%LOG_PATH%"
    )

    call :LogLine "[INFO] Starting TopdownShooter.Server on port %PORT% (headless)..."
    pushd "%SERVER_ROOT%"
    dotnet run --project "%PROJECT_PATH%" >> "%LOG_PATH%" 2>&1
    set "EXIT_CODE=%ERRORLEVEL%"
    popd
    call :LogLine "[INFO] TopdownShooter.Server exited with code %EXIT_CODE%."
    exit /b %EXIT_CODE%
)

echo [INFO] Starting TopdownShooter.Server on port %PORT%...
start "TopdownShooter.Server" cmd /k "cd /d ""%SERVER_ROOT%"" && dotnet run --project ""%PROJECT_PATH%"""
exit /b 0

:LogLine
if not defined LOG_PATH (
    exit /b 0
)

echo [%date% %time%] %~1>> "%LOG_PATH%"
exit /b 0
