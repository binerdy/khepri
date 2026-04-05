#Requires -Version 5.1
<#
.SYNOPSIS
    Launches the Khepri MAUI app on an Android emulator.
.DESCRIPTION
    Checks for Android SDK tools, lists available AVDs, creates one if none exist,
    starts an emulator, waits for it to boot, then deploys and runs the app.
.PARAMETER Avd
    Name of the AVD to launch or create. Defaults to 'Khepri_Pixel9Pro_API36'.
.PARAMETER ApiLevel
    API level used when creating a new AVD. Defaults to 36.
.PARAMETER NoLaunch
    Skip launching an emulator — deploy to whatever device is already connected.
#>
param(
    [string]$Avd      = 'Khepri_Pixel9Pro_API36',
    [int]   $ApiLevel = 36,
    [switch]$NoLaunch
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ProjectPath = Join-Path $PSScriptRoot '..\src\Khepri\Khepri.csproj'

# ── Helpers ───────────────────────────────────────────────────────────────────

function Write-Step([string]$msg)  { Write-Host "  $msg"        -ForegroundColor Cyan   }
function Write-Ok([string]$msg)    { Write-Host "  [OK] $msg"   -ForegroundColor Green  }
function Write-Warn([string]$msg)  { Write-Host "  [..] $msg"   -ForegroundColor Yellow }
function Write-Fail([string]$msg)  { Write-Host "  [!!] $msg"   -ForegroundColor Red    }

function Require-Command([string]$name) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
        Write-Fail "'$name' not found on PATH."
        return $false
    }
    return $true
}

# ── Banner ────────────────────────────────────────────────────────────────────

Write-Host ""
Write-Host "  Khepri — run on Android emulator" -ForegroundColor White
Write-Host "  ───────────────────────────────────" -ForegroundColor DarkGray
Write-Host ""

# ── 1. Locate Android SDK tools ───────────────────────────────────────────────

Write-Step "Locating Android SDK tools..."

$sdkRoot = $env:ANDROID_HOME ?? $env:ANDROID_SDK_ROOT

if (-not $sdkRoot) {
    # Check known install locations in priority order:
    #   1. Visual Studio / Component.Android.SDK.MAUI default
    #   2. Standalone SDK / Android Studio default
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

$emulatorExe    = Join-Path $sdkRoot 'emulator\emulator.exe'
$adbExe         = Join-Path $sdkRoot 'platform-tools\adb.exe'
$avdmanagerExe  = Join-Path $sdkRoot 'cmdline-tools\latest\bin\avdmanager.bat'
$sdkmanagerExe  = Join-Path $sdkRoot 'cmdline-tools\latest\bin\sdkmanager.bat'

# avdmanager may also live in an older cmdline-tools path
if (-not (Test-Path $avdmanagerExe)) {
    $avdmanagerExe = Get-ChildItem -Path $sdkRoot -Recurse -Filter 'avdmanager.bat' -ErrorAction SilentlyContinue |
        Select-Object -First 1 -ExpandProperty FullName
}
if (-not (Test-Path $sdkmanagerExe)) {
    $sdkmanagerExe = Get-ChildItem -Path $sdkRoot -Recurse -Filter 'sdkmanager.bat' -ErrorAction SilentlyContinue |
        Select-Object -First 1 -ExpandProperty FullName
}

foreach ($tool in @($emulatorExe, $adbExe)) {
    if (-not (Test-Path $tool)) {
        Write-Fail "Required tool not found: $tool"
        exit 1
    }
}

Write-Ok "Android SDK: $sdkRoot"

# ── 2. Check for running emulator / device ────────────────────────────────────

Write-Step "Checking for connected devices..."

$devices = @(& $adbExe devices 2>&1 | Select-Object -Skip 1 | Where-Object { $_ -match '\bdevice\b' })
$hasDevice = $devices.Count -gt 0

if ($hasDevice) {
    Write-Ok "Device already connected: $($devices[0])"
    $NoLaunch = $true
}

# ── 3. Start emulator if needed ───────────────────────────────────────────────

if (-not $NoLaunch) {
    Write-Step "Listing available AVDs..."
    $avds = @(& $emulatorExe -list-avds 2>&1 | Where-Object { $_.Trim() -ne '' })

    if ($avds.Count -gt 0) {
        Write-Host ""
        $avds | ForEach-Object { Write-Host "    · $_" -ForegroundColor DarkGray }
        Write-Host ""
    }

    if ($Avd -notin $avds) {
        Write-Warn "AVD '$Avd' not found — creating it..."

        # ── Ensure the system image is installed ──────────────────────────────
        $imagePackage = "system-images;android-$ApiLevel;google_apis_playstore;x86_64"
        if ($sdkmanagerExe -and (Test-Path $sdkmanagerExe)) {
            Write-Step "Checking system image: $imagePackage ..."
            $installed = & $sdkmanagerExe --list_installed 2>&1 | Out-String
            if ($installed -notmatch [regex]::Escape("system-images;android-$ApiLevel")) {
                Write-Step "Installing $imagePackage (this may take a few minutes)..."
                echo "y" | & $sdkmanagerExe $imagePackage
                if ($LASTEXITCODE -ne 0) {
                    Write-Fail "sdkmanager failed to install '$imagePackage'."
                    exit 1
                }
                Write-Ok "System image installed."
            } else {
                Write-Ok "System image already installed."
            }
        } else {
            Write-Warn "sdkmanager not found — assuming system image '$imagePackage' is already installed."
        }

        # ── Create the AVD ────────────────────────────────────────────────────
        if (-not ($avdmanagerExe -and (Test-Path $avdmanagerExe))) {
            Write-Fail "avdmanager not found. Install 'Android SDK Command-line Tools' via Android Studio SDK Manager."
            exit 1
        }

        Write-Step "Creating AVD '$Avd' (API $ApiLevel, Pixel 10 Pro)..."
        echo "no" | & $avdmanagerExe create avd `
            --name    $Avd `
            --package $imagePackage `
            --device  "pixel_9_pro" `
            --force

        if ($LASTEXITCODE -ne 0) {
            Write-Fail "avdmanager failed to create AVD '$Avd'."
            exit 1
        }

        Write-Ok "AVD '$Avd' created."
    } else {
        Write-Ok "AVD '$Avd' found."
    }

    Write-Step "Starting emulator: $Avd ..."
    Start-Process -FilePath $emulatorExe -ArgumentList "-avd `"$Avd`" -no-snapshot-load" -WindowStyle Normal

    # Wait for device to come online (adb reports 'device' state)
    Write-Step "Waiting for emulator to boot..."
    $timeout = [DateTime]::UtcNow.AddMinutes(3)
    $booted  = $false

    while ([DateTime]::UtcNow -lt $timeout) {
        $state = & $adbExe shell getprop sys.boot_completed 2>$null
        if ($state -match '1') {
            $booted = $true
            break
        }
        Write-Host "." -NoNewline
        Start-Sleep -Seconds 3
    }

    Write-Host ""

    if (-not $booted) {
        Write-Fail "Emulator did not boot within 3 minutes."
        exit 1
    }

    Write-Ok "Emulator booted."
}

# ── 4. Build and deploy ───────────────────────────────────────────────────────

Write-Step "Building and deploying Khepri (net10.0-android)..."
Write-Host ""

& dotnet build $ProjectPath -f net10.0-android -t:Run

if ($LASTEXITCODE -ne 0) {
    Write-Fail "Build or deploy failed (exit code $LASTEXITCODE)."
    exit $LASTEXITCODE
}

Write-Host ""
Write-Ok "App launched on emulator."
Write-Host ""
