@echo off
setlocal

set "SERVER_ROOT=%~dp0"
set "PROJECT_PATH=%SERVER_ROOT%TopdownShooter.BotClient\TopdownShooter.BotClient.csproj"
set "BOT_ARGS=--bots=4 --threads=8 --connect-stagger-ms=100 --move-interval-ms=120"
set "HEADLESS=%TOPDOWN_HEADLESS%"
set "LOG_PATH=%TOPDOWN_LOG_PATH%"

if defined TOPDOWN_BOT_ARGS (
    set "BOT_ARGS=%TOPDOWN_BOT_ARGS%"
)

if not exist "%PROJECT_PATH%" (
    echo [ERROR] Bot client project file not found:
    echo %PROJECT_PATH%
    exit /b 1
)

if "%HEADLESS%"=="1" (
    if not defined LOG_PATH (
        set "LOG_PATH=%SERVER_ROOT%.logs\bot.log"
    )

    for %%I in ("%LOG_PATH%") do set "LOG_DIR=%%~dpI"
    if not exist "%LOG_DIR%" (
        mkdir "%LOG_DIR%" >nul 2>&1
    )

    if "%TOPDOWN_TRUNCATE_LOG%"=="1" (
        type nul > "%LOG_PATH%"
    )

    call :LogLine "[INFO] Starting TopdownShooter.BotClient (headless)..."
    call :LogLine "[INFO] Args: %BOT_ARGS%"
    pushd "%SERVER_ROOT%"
    dotnet run --project "%PROJECT_PATH%" -- %BOT_ARGS% >> "%LOG_PATH%" 2>&1
    set "EXIT_CODE=%ERRORLEVEL%"
    popd
    call :LogLine "[INFO] TopdownShooter.BotClient exited with code %EXIT_CODE%."
    exit /b %EXIT_CODE%
)

echo [INFO] Starting TopdownShooter.BotClient...
echo [INFO] Args: %BOT_ARGS%
start "TopdownShooter.BotClient" cmd /k "cd /d ""%SERVER_ROOT%"" && dotnet run --project ""%PROJECT_PATH%"" -- %BOT_ARGS%"
exit /b 0

:LogLine
if not defined LOG_PATH (
    exit /b 0
)

echo [%date% %time%] %~1>> "%LOG_PATH%"
exit /b 0
