#Requires -Version 5.1
<#
.SYNOPSIS
    Builds Khepri and deploys it to a physical Android device over USB.
.DESCRIPTION
    Locates the Android SDK, waits for exactly one non-emulator device to appear
    via adb, then builds and installs the app using the device's serial number so
    the deploy targets the right device even if an emulator is also running.
.PARAMETER Configuration
    MSBuild configuration. Defaults to 'Debug'.
.PARAMETER Wait
    Keep polling for a USB device instead of failing immediately when none is found.
#>
param(
    [string][ValidateSet('Debug','Release')]$Configuration = 'Debug',
    [switch]$Wait,
    [switch]$Cleanup
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ProjectPath = Join-Path $PSScriptRoot '..\src\Khepri\Khepri.csproj'

# ── Helpers ───────────────────────────────────────────────────────────────────

function Write-Step([string]$msg)  { Write-Host "  $msg"        -ForegroundColor Cyan   }
function Write-Ok([string]$msg)    { Write-Host "  [OK] $msg"   -ForegroundColor Green  }
function Write-Warn([string]$msg)  { Write-Host "  [..] $msg"   -ForegroundColor Yellow }
function Write-Fail([string]$msg)  { Write-Host "  [!!] $msg"   -ForegroundColor Red    }

# ── Banner ────────────────────────────────────────────────────────────────────

Write-Host ""
Write-Host "  Khepri — deploy to USB device" -ForegroundColor White
Write-Host "  ──────────────────────────────" -ForegroundColor DarkGray
Write-Host ""

# ── 1. Locate Android SDK tools ───────────────────────────────────────────────

Write-Step "Locating Android SDK tools..."

$sdkRoot = $env:ANDROID_HOME ?? $env:ANDROID_SDK_ROOT

if (-not $sdkRoot) {
    $candidates = @(
        "${env:ProgramFiles(x86)}\Android\android-sdk",
        (Join-Path $env:LOCALAPPDATA 'Android\Sdk')
    )
    $sdkRoot = $candidates | Where-Object { Test-Path (Join-Path $_ 'platform-tools') } | Select-Object -First 1
}

if (-not $sdkRoot -or -not (Test-Path $sdkRoot)) {
    Write-Fail "Android SDK not found. Run .\scripts\install.ps1 or set ANDROID_HOME."
    exit 1
}

$adbExe = Join-Path $sdkRoot 'platform-tools\adb.exe'

if (-not (Test-Path $adbExe)) {
    Write-Fail "adb not found at: $adbExe"
    exit 1
}

Write-Ok "Android SDK: $sdkRoot"

# ── 2. Wait for / detect a physical USB device ────────────────────────────────

Write-Step "Looking for a USB-connected device (not emulator)..."

function Get-UsbDevices {
    # adb devices output looks like:
    #   List of devices attached
    #   R5CTA1BQPVL    device
    #   emulator-5554  device
    @(& $adbExe devices 2>&1 |
        Select-Object -Skip 1 |
        Where-Object { $_ -match '\bdevice\b' -and $_ -notmatch '^emulator-' } |
        ForEach-Object { ($_ -split '\s+')[0] })
}

$serial = $null
$timeout = [DateTime]::UtcNow.AddSeconds(30)

do {
    $usb = @(Get-UsbDevices)
    if ($usb.Count -eq 1) {
        $serial = $usb[0]
        break
    }
    if ($usb.Count -gt 1) {
        Write-Fail "Multiple USB devices found. Disconnect all but your Pixel and retry."
        Write-Host ""
        $usb | ForEach-Object { Write-Host "    · $_" -ForegroundColor DarkGray }
        Write-Host ""
        exit 1
    }
    if (-not $Wait -and [DateTime]::UtcNow -ge $timeout) { break }
    if ($Wait) {
        Write-Host "." -NoNewline
        Start-Sleep -Seconds 2
    }
} while ($Wait -or [DateTime]::UtcNow -lt $timeout)

Write-Host ""

if (-not $serial) {
    Write-Fail "No USB device found. Connect your Pixel with USB debugging enabled and retry."
    Write-Host ""
    Write-Host "  To enable USB debugging on your Pixel:" -ForegroundColor DarkGray
    Write-Host "    Settings → About phone → tap Build number 7 times" -ForegroundColor DarkGray
    Write-Host "    Settings → System → Developer options → USB debugging → ON" -ForegroundColor DarkGray
    Write-Host ""
    exit 1
}

Write-Ok "Target device: $serial"

# ── 3. Check adb authorisation ────────────────────────────────────────────────

Write-Step "Checking adb authorisation..."

$deviceState = & $adbExe -s $serial get-state 2>&1
if ($deviceState -ne 'device') {
    if ($deviceState -match 'unauthorized') {
        Write-Fail "Device is unauthorised. Accept the 'Allow USB debugging' prompt on your Pixel."
    } else {
        Write-Fail "Unexpected device state: $deviceState"
    }
    exit 1
}

Write-Ok "Device authorised."

# ── 4. Uninstall old build (clears FastDev assembly cache) ───────────────────

if ($Cleanup) {
    Write-Step "Uninstalling previous build (if any)..."
    & $adbExe -s $serial uninstall com.companyname.khepri 2>&1 | Out-Null
    Write-Ok "Uninstall done (or app was not installed)."
}

# Clear logcat now so the buffer is clean before the build starts.
# Any crash during launch will be preserved in the stream below.
& $adbExe -s $serial logcat -c 2>$null

# ── 5. Build and deploy ───────────────────────────────────────────────────────

Write-Step "Building and deploying Khepri ($Configuration, net10.0-android)..."
Write-Host ""

# AndroidFastDeployment=false bundles all assemblies into the APK so there
# are no stale FastDev assemblies left over from previous deploys.
& dotnet build $ProjectPath `
    -f net10.0-android `
    -c $Configuration `
    -t:Run `
    -p:AndroidDeviceSerial=$serial `
    -p:AndroidFastDeployment=false

if ($LASTEXITCODE -ne 0) {
    Write-Fail "Build or deploy failed (exit code $LASTEXITCODE)."
    exit $LASTEXITCODE
}

Write-Host ""
Write-Ok "App launched on $serial."
Write-Host ""

# ── 6. Live logcat — crash-relevant tags only ───────────────────────────────

Write-Host "  Streaming logcat (crashes + .NET errors only). Press Ctrl+C to stop." -ForegroundColor DarkGray
Write-Host ""

# *:S silences everything by default.
# Named tags are then selectively enabled at Error or Fatal level:
#   AndroidRuntime  — Java/JNI unhandled exception header + stack
#   mono-rt         — .NET FATAL UNHANDLED EXCEPTION
#   mono            — general Mono runtime errors
#   monodroid       — Xamarin/MAUI Android bridge errors
#   DOTNET          — .NET runtime errors
#   libc            — native abort/SIGABRT (signals the actual crash line)
#   DEBUG           — tombstone / native crash output
& $adbExe -s $serial logcat -v threadtime `
    '*:S' `
    'AndroidRuntime:E' `
    'mono-rt:F' `
    'mono:E' `
    'monodroid:E' `
    'DOTNET:E' `
    'libc:F' `
    'DEBUG:E' `
    2>&1 | ForEach-Object {
    if ($_ -match '\bF\b|FATAL|SIGABRT|signal') {
        Write-Host $_ -ForegroundColor Magenta
    } elseif ($_ -match '\bE\b') {
        Write-Host $_ -ForegroundColor Red
    } else {
        Write-Host $_
    }
}
