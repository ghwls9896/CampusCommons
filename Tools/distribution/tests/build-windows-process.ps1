$ErrorActionPreference = "Stop"
$testRoot = Join-Path $env:TEMP ("campus-build-test-" + [Guid]::NewGuid().ToString("N"))
$project = Join-Path $testRoot "project with spaces"
$scriptDir = Join-Path $project "Tools/distribution"
$bin = Join-Path $testRoot "bin with spaces"
New-Item -ItemType Directory -Force $scriptDir, $bin | Out-Null
Copy-Item (Join-Path $PSScriptRoot "../build-windows.ps1") (Join-Path $scriptDir "build-windows.ps1")
[IO.File]::WriteAllText((Join-Path $bin "uv.cmd"), "@exit /b 0`r`n")
$fakeUnity = Join-Path $bin "Fake Unity.exe"
Add-Type -OutputAssembly $fakeUnity -OutputType WindowsApplication -TypeDefinition @'
using System;
using System.IO;
using System.Threading;
public static class FakeUnity {
    public static int Main(string[] args) {
        Thread.Sleep(1500);
        File.WriteAllLines(Environment.GetEnvironmentVariable("CAMPUS_FAKE_RESULT"), args);
        return int.Parse(Environment.GetEnvironmentVariable("CAMPUS_FAKE_EXIT"));
    }
}
'@
$oldPath = $env:PATH
$env:PATH = "$bin;$oldPath"
try {
    foreach ($exit in @(0, 7)) {
        $env:CAMPUS_FAKE_EXIT = "$exit"
        $env:CAMPUS_FAKE_RESULT = Join-Path $testRoot ("unity-$exit.txt")
        $ErrorActionPreference = "Continue"
        $output = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $scriptDir "build-windows.ps1") -UnityEditor $fakeUnity 2>&1
        $actual = $LASTEXITCODE
        $ErrorActionPreference = "Stop"
        if (!(Test-Path $env:CAMPUS_FAKE_RESULT)) { throw "Build returned before Unity finished (exit $exit)." }
        $argsSeen = @(Get-Content $env:CAMPUS_FAKE_RESULT)
        $projectIndex = [Array]::IndexOf($argsSeen, "-projectPath")
        if ($projectIndex -lt 0 -or $argsSeen[$projectIndex + 1] -ne $project) { throw "Project path with spaces was not preserved." }
        if ($exit -eq 0 -and ($actual -ne 0 -or !($output -match "Ready:"))) { throw "Successful build was not reported." }
        if ($exit -ne 0 -and ($actual -eq 0 -or ($output -match "Ready:"))) { throw "Failed build was reported as successful." }
    }
    Write-Output "BUILD_PROCESS_WAIT_AND_FAILURE_PASS"
} finally {
    $env:PATH = $oldPath
    Remove-Item Env:CAMPUS_FAKE_EXIT, Env:CAMPUS_FAKE_RESULT -ErrorAction SilentlyContinue
}
