@echo off
setlocal

set "SERVER_ROOT=%~dp0"
set "PROJECT_PATH=%SERVER_ROOT%TopdownShooter.BotClient\TopdownShooter.BotClient.csproj"
set "BOT_ARGS=--bots=4 --threads=8 --connect-stagger-ms=100 --move-interval-ms=120"

if defined TOPDOWN_BOT_ARGS (
    set "BOT_ARGS=%TOPDOWN_BOT_ARGS%"
)

if not exist "%PROJECT_PATH%" (
    echo [ERROR] Bot client project file not found:
    echo %PROJECT_PATH%
    exit /b 1
)

echo [INFO] Starting TopdownShooter.BotClient...
echo [INFO] Args: %BOT_ARGS%
start "TopdownShooter.BotClient" cmd /k "cd /d ""%SERVER_ROOT%"" && dotnet run --project ""%PROJECT_PATH%"" -- %BOT_ARGS%"
exit /b 0

