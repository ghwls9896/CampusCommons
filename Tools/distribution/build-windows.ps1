param(
    [string]$UnityEditor = "C:\Program Files\Unity\Hub\Editor\6000.4.5f1\Editor\Unity.exe",
    [string]$ModelSource = ""
)
$ErrorActionPreference = "Stop"
Push-Location (Join-Path $PSScriptRoot "../..")
try {
    if (!(Get-Command uv -ErrorAction SilentlyContinue)) { throw "Build machine requires uv. Players do not." }
    if (!(Test-Path $UnityEditor)) { throw "Unity 6000.4.5f1 with Windows Build Support is required." }
    $prepareArgs = @("run", "--no-project", "--python", "3.12.11", "python", "Tools/distribution/prepare_runtime.py")
    if ($ModelSource) { $prepareArgs += @("--model-source", $ModelSource) }
    & uv @prepareArgs
    if ($LASTEXITCODE -ne 0) { throw "Runtime preparation failed." }
    $log = Join-Path (Get-Location) ".distribution-work/unity-windows-build.log"
    # Unity on Windows is a GUI executable: wait for it explicitly and inspect its exit code.
    $unityArgs = @("-batchmode", "-quit", "-projectPath", ('"' + (Get-Location).Path + '"'),
        "-executeMethod", "CampusCommons.Webcam.Editor.DistributionBuild.Windows", "-logFile", ('"' + $log + '"'))
    $unity = Start-Process -FilePath $UnityEditor -ArgumentList $unityArgs -WindowStyle Hidden -PassThru -Wait
    if ($unity.ExitCode -ne 0) { throw "Unity build failed (exit $($unity.ExitCode)). Check $log" }
    Write-Output "Ready: Builds/CampusCommons-windows-x64 (distribute the entire folder)."
} finally { Pop-Location }
