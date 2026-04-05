#Requires -Version 5.1
<#
.SYNOPSIS
    Verifies prerequisites for the Khepri .NET MAUI project and installs missing components.
.DESCRIPTION
    Checks that the required .NET SDK version is installed and that the maui workload
    is present. Installs the workload if missing. Uses a spinner to indicate progress.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# VS installer --quiet requires elevation. Re-launch as admin if needed.
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "  Elevating to Administrator (required for VS installer)..." -ForegroundColor Cyan
    $argList = '-NoProfile -ExecutionPolicy Bypass -File "{0}"' -f $PSCommandPath
    Start-Process pwsh -Verb RunAs -ArgumentList $argList -Wait
    exit $LASTEXITCODE
}

$RequiredDotNetMajor = 10

# ── Helpers ──────────────────────────────────────────────────────────────────

function Write-Step([string]$Message) {
    Write-Host "  $Message" -ForegroundColor Cyan
}

function Write-Ok([string]$Message) {
    Write-Host "  [OK] $Message" -ForegroundColor Green
}

function Write-Warn([string]$Message) {
    Write-Host "  [..] $Message" -ForegroundColor Yellow
}

function Write-Fail([string]$Message) {
    Write-Host "  [!!] $Message" -ForegroundColor Red
}

function Start-Spinner([string]$Message, [scriptblock]$Action) {
    $frames = @('|', '/', '-', '\')
    $i = 0

    $job = Start-Job -ScriptBlock $Action

    while ($job.State -eq 'Running') {
        $frame = $frames[$i % $frames.Length]
        Write-Host "`r  [$frame] $Message" -NoNewline -ForegroundColor Cyan
        Start-Sleep -Milliseconds 120
        $i++
    }

    Write-Host "`r" -NoNewline  # clear spinner line

    $result = Receive-Job -Job $job -Wait -AutoRemoveJob 2>&1
    if ($job.State -eq 'Failed' -or ($result | Where-Object { $_ -is [System.Management.Automation.ErrorRecord] })) {
        throw ($result | Out-String).Trim()
    }

    return $result
}

# ── Banner ────────────────────────────────────────────────────────────────────

Write-Host ""
Write-Host "  Khepri — prerequisite installer" -ForegroundColor White
Write-Host "  ─────────────────────────────────" -ForegroundColor DarkGray
Write-Host ""

# ── 1. Check dotnet CLI is on PATH ────────────────────────────────────────────

Write-Step "Checking for dotnet CLI..."

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Fail "dotnet CLI not found on PATH."
    Write-Host ""
    Write-Host "  Install the .NET SDK from: https://dot.net" -ForegroundColor DarkGray
    exit 1
}

# ── 2. Check required .NET SDK version ───────────────────────────────────────

Write-Step "Checking .NET SDK version (required: $RequiredDotNetMajor.x)..."

$rawVersion = dotnet --version 2>&1
if ($rawVersion -match '^(\d+)\.') {
    $installedMajor = [int]$Matches[1]
} else {
    Write-Fail "Could not parse dotnet version: $rawVersion"
    exit 1
}

if ($installedMajor -lt $RequiredDotNetMajor) {
    Write-Fail "Installed .NET SDK version is $rawVersion. Version $RequiredDotNetMajor.x or higher is required."
    Write-Host ""
    Write-Host "  Download the latest SDK from: https://dot.net" -ForegroundColor DarkGray
    exit 1
}

Write-Ok "Installed .NET SDK: $rawVersion"

# ── 3. Check maui workload ────────────────────────────────────────────────────

Write-Step "Checking for 'maui' workload..."

$workloads = dotnet workload list 2>&1 | Out-String
$mauiInstalled = $workloads -match '(?m)^\s*maui\b'

if ($mauiInstalled) {
    Write-Ok "'maui' workload is already installed."
} else {
    Write-Warn "'maui' workload not found. Installing..."
    Write-Host ""

    try {
        Start-Spinner -Message "Running: dotnet workload install maui" -Action {
            dotnet workload install maui 2>&1
        }
        Write-Ok "'maui' workload installed successfully."
    } catch {
        Write-Fail "Failed to install 'maui' workload:"
        Write-Host ("  " + $_.ToString()) -ForegroundColor Red
        Write-Host ""
        Write-Host "  Try running manually: dotnet workload install maui" -ForegroundColor DarkGray
        exit 1
    }
}

# ── 4. Check / install Android SDK + Emulator (via Visual Studio installer) ───

# VS Component IDs needed for Android emulator development.
# Note: In VS 2026, the emulator is bundled inside Component.Android.SDK.MAUI —
# Microsoft.VisualStudio.Component.Android.Emulator no longer exists.
$VsAndroidComponents = @(
    'Component.Android.SDK.MAUI'
)

Write-Step "Locating Android SDK..."

# Android SDK can be at ANDROID_HOME, ANDROID_SDK_ROOT, the VS default, or the
# standalone SDK default. Check all of them.
$candidateRoots = @(
    $env:ANDROID_HOME,
    $env:ANDROID_SDK_ROOT,
    "${env:ProgramFiles(x86)}\Android\android-sdk",
    "$env:LOCALAPPDATA\Android\Sdk"
) | Where-Object { $_ }

$sdkRoot = $candidateRoots | Where-Object { Test-Path (Join-Path $_ 'platform-tools') } | Select-Object -First 1

if ($sdkRoot) {
    Write-Ok "Android SDK found: $sdkRoot"
} else {
    Write-Warn "Android SDK not found. Installing via Visual Studio installer..."

    $vsInstaller = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vs_installer.exe"
    if (-not (Test-Path $vsInstaller)) {
        Write-Fail "Visual Studio Installer not found at: $vsInstaller"
        Write-Host "  Install Visual Studio with the '.NET Multi-platform App UI development' workload." -ForegroundColor DarkGray
        exit 1
    }

    # Find the VS install path via vswhere
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    $vsInstallPath = & $vswhere -latest -property installationPath 2>$null
    if (-not $vsInstallPath) {
        Write-Fail "Could not locate a Visual Studio installation via vswhere."
        exit 1
    }
    Write-Step "Modifying Visual Studio at: $vsInstallPath"

    $addArgs = $VsAndroidComponents | ForEach-Object { "--add $_" }
    $argList = (@(
        'modify',
        "--installPath `"$vsInstallPath`"",
        '--quiet', '--norestart'
    ) + $addArgs) -join ' '
    $proc = Start-Process -FilePath $vsInstaller -ArgumentList $argList -Wait -PassThru

    if ($proc.ExitCode -notin @(0, 3010)) {
        Write-Fail "VS installer exited with code $($proc.ExitCode)."
        exit 1
    }

    # Re-check after install
    $sdkRoot = $candidateRoots | Where-Object { Test-Path (Join-Path $_ 'platform-tools') } | Select-Object -First 1
    if (-not $sdkRoot) {
        # VS default location for Component.Android.SDK.MAUI
        $sdkRoot = "${env:ProgramFiles(x86)}\Android\android-sdk"
    }

    Write-Ok "Android SDK base installed at: $sdkRoot"

    if ($proc.ExitCode -eq 3010) {
        Write-Warn "A restart is required to complete the installation."
    }
}

# Persist ANDROID_HOME so run-android.ps1 finds the right SDK
[System.Environment]::SetEnvironmentVariable('ANDROID_HOME', $sdkRoot, 'User')
$env:ANDROID_HOME = $sdkRoot
Write-Ok "ANDROID_HOME set to: $sdkRoot"

# ── 5. Install emulator + system image via sdkmanager ─────────────────────────

$AndroidSdkPackages = @(
    'emulator'
    "system-images;android-36;google_apis_playstore;x86_64"
)

# Find sdkmanager
$sdkmanager = Get-ChildItem -Path $sdkRoot -Recurse -Filter 'sdkmanager.bat' -ErrorAction SilentlyContinue |
    Select-Object -First 1 -ExpandProperty FullName

if (-not $sdkmanager) {
    Write-Fail "sdkmanager not found in SDK at: $sdkRoot"
    exit 1
}

# sdkmanager needs Java. Check PATH first, then install via winget if missing.
$javaCmd = Get-Command java -ErrorAction SilentlyContinue
if ($javaCmd) {
    Write-Ok "Using JDK: $(Split-Path $javaCmd.Source)"
} else {
    Write-Warn "Java not found. Installing Microsoft OpenJDK 25 via winget..."
    $winget = Get-Command winget -ErrorAction SilentlyContinue
    if (-not $winget) {
        Write-Fail "winget is not available. Install Java 21+ manually from https://adoptium.net then re-run."
        exit 1
    }
    & winget install --id Microsoft.OpenJDK.25 --silent --accept-package-agreements --accept-source-agreements
    if ($LASTEXITCODE -ne 0) {
        Write-Fail "Failed to install Java via winget."
        exit 1
    }
    # Add the newly installed JDK to PATH for this session
    $jdkBin = Get-Item "$env:ProgramFiles\Microsoft\jdk-25*\bin" -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
    if ($jdkBin) {
        $env:PATH      = "$jdkBin;$env:PATH"
        $env:JAVA_HOME = Split-Path $jdkBin
    }
    Write-Ok "Java 25 installed."
}

# Accept all SDK licenses up-front
Write-Step "Accepting Android SDK licenses..."
("y`n" * 20) | & $sdkmanager --licenses 2>&1 | Out-Null
Write-Ok "Licenses accepted."

Write-Step "Checking required Android SDK packages..."
$installedOut = & $sdkmanager --list_installed 2>&1 | Out-String
if ($LASTEXITCODE -ne 0) {
    Write-Fail "sdkmanager failed. Output:`n$installedOut"
    exit 1
}

$licenseInput = "y`n" * 20
foreach ($pkg in $AndroidSdkPackages) {
    if ($installedOut -match [regex]::Escape($pkg)) {
        Write-Ok "Already installed: $pkg"
    } else {
        Write-Warn "Installing: $pkg (this may take a few minutes)..."
        $out = ($licenseInput | & $sdkmanager $pkg 2>&1)
        $rc  = $LASTEXITCODE
        $out | Where-Object { $_ -match 'Downloading|Installing|Unzipping|done' } |
            ForEach-Object { Write-Host "    $_" -ForegroundColor DarkGray }
        if ($rc -ne 0) {
            Write-Fail "sdkmanager failed to install '$pkg'.`n$($out | Out-String)"
            exit 1
        }
        Write-Ok "Installed: $pkg"
    }
}

# ── Done ──────────────────────────────────────────────────────────────────────

Write-Host ""
Write-Host "  All prerequisites satisfied. You're ready to build Khepri." -ForegroundColor Green
Write-Host ""
