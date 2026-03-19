param(
    [string]$UnityPath = "C:\Program Files\Unity\Hub\Editor\6000.3.8f1\Editor\Unity.exe",
    [string]$ProjectPath = (Join-Path $PSScriptRoot "PackageTest"),
    [string]$ExecuteMethod = "PackageTestSceneGenerator.CreateRequestPipelineTestScene",
    [string]$LogPath = (Join-Path $PSScriptRoot "PackageTest\Logs\RequestPipelineSceneLaunch.log")
)

if (-not (Test-Path $UnityPath)) {
    throw "Unity executable not found: $UnityPath"
}

if (-not (Test-Path $ProjectPath)) {
    throw "Unity project not found: $ProjectPath"
}

$arguments = @(
    "-projectPath", $ProjectPath,
    "-executeMethod", $ExecuteMethod,
    "-logFile", $LogPath
)

New-Item -ItemType Directory -Force -Path (Split-Path $LogPath -Parent) | Out-Null
Start-Process -FilePath $UnityPath -ArgumentList $arguments -WorkingDirectory $ProjectPath
