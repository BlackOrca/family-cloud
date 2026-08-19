<#
    Starts the Android emulator (AVD "ourlive-test") in the background and waits until
    Android has fully booted, so the emulator is immediately ready for `dotnet build -t:Run`
    or deploying the APK from build.ps1.
#>

[CmdletBinding()]
param(
    [string] $AvdName = "ourlive-test"
)

$ErrorActionPreference = "Stop"

$sdkRoot = if ($env:ANDROID_HOME) { $env:ANDROID_HOME } elseif ($env:ANDROID_SDK_ROOT) { $env:ANDROID_SDK_ROOT } else { Join-Path $env:LOCALAPPDATA "Android\Sdk" }
$emulatorExe = Join-Path $sdkRoot "emulator\emulator.exe"
$adbExe = Join-Path $sdkRoot "platform-tools\adb.exe"

if (-not (Test-Path $emulatorExe)) {
    throw "emulator.exe not found at $emulatorExe - set ANDROID_HOME/ANDROID_SDK_ROOT if the SDK lives elsewhere."
}

$avds = & $emulatorExe -list-avds
if ($avds -notcontains $AvdName) {
    throw "AVD '$AvdName' not found. Available AVDs: $($avds -join ', ')"
}

$alreadyRunning = & $adbExe devices | Select-String "emulator-"
if ($alreadyRunning) {
    Write-Host "An emulator is already running:" -ForegroundColor Yellow
    Write-Host $alreadyRunning
    exit 0
}

Write-Host ">>> Starting emulator '$AvdName'..." -ForegroundColor Cyan
Start-Process -FilePath $emulatorExe -ArgumentList "-avd", $AvdName, "-netdelay", "none", "-netspeed", "full" -WindowStyle Minimized

Write-Host ">>> Waiting for device..." -ForegroundColor Cyan
& $adbExe wait-for-device

Write-Host ">>> Waiting for Android to finish booting..." -ForegroundColor Cyan
do {
    Start-Sleep -Seconds 2
    $bootCompleted = (& $adbExe shell getprop sys.boot_completed 2>$null).Trim()
} while ($bootCompleted -ne "1")

Write-Host "=== Emulator '$AvdName' is ready ===" -ForegroundColor Green
