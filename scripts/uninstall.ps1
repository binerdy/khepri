#Requires -Version 5.1
<#
.SYNOPSIS
    Removes prerequisites installed by install.ps1 for the Khepri project.
.DESCRIPTION
    Removes the 'maui' .NET workload, the Component.Android.SDK.MAUI Visual Studio
    component, and the ANDROID_HOME user environment variable.
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

# ── Helpers ───────────────────────────────────────────────────────────────────

function Write-Step([string]$Message) { Write-Host "  $Message" -ForegroundColor Cyan }
function Write-Ok([string]$Message)   { Write-Host "  [OK] $Message" -ForegroundColor Green }
function Write-Warn([string]$Message) { Write-Host "  [..] $Message" -ForegroundColor Yellow }
function Write-Fail([string]$Message) { Write-Host "  [!!] $Message" -ForegroundColor Red }

# ── Banner ────────────────────────────────────────────────────────────────────

Write-Host ""
Write-Host "  Khepri — prerequisite uninstaller" -ForegroundColor Magenta
Write-Host "  ────────────────────────────────────" -ForegroundColor DarkGray
Write-Host ""

# ── 1. Remove 'maui' .NET workload ────────────────────────────────────────────

Write-Step "Checking for 'maui' workload..."
$workloads = dotnet workload list 2>&1 | Out-String
if ($workloads -match '\bmaui\b') {
    Write-Warn "Removing 'maui' workload..."
    dotnet workload uninstall maui 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Fail "Failed to uninstall 'maui' workload. Try manually: dotnet workload uninstall maui"
    } else {
        Write-Ok "'maui' workload removed."
    }
} else {
    Write-Ok "'maui' workload is not installed — nothing to do."
}

# ── 2. Remove Component.Android.SDK.MAUI via VS installer ─────────────────────

Write-Step "Checking for Component.Android.SDK.MAUI in Visual Studio..."

$vsInstaller = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vs_installer.exe"
$vswhere     = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"

if (-not (Test-Path $vsInstaller)) {
    Write-Warn "VS installer not found — skipping Android SDK component removal."
} else {
    $vsInstallPath = & $vswhere -latest -property installationPath 2>$null

    if (-not $vsInstallPath) {
        Write-Warn "No VS installation found via vswhere — skipping."
    } else {
        # Attempt removal — VS installer exits 0 if the component wasn't installed
        Write-Warn "Removing Component.Android.SDK.MAUI from Visual Studio at: $vsInstallPath"
        $argList = "modify --installPath `"$vsInstallPath`" --quiet --norestart --remove Component.Android.SDK.MAUI"
        $proc = Start-Process -FilePath $vsInstaller -ArgumentList $argList -Wait -PassThru
        if ($proc.ExitCode -notin @(0, 3010)) {
            Write-Fail "VS installer exited with code $($proc.ExitCode)."
        } else {
            Write-Ok "Component.Android.SDK.MAUI removed (or was not installed)."
            if ($proc.ExitCode -eq 3010) {
                Write-Warn "A restart is required to complete the removal."
            }
        }
    }
}

# ── 3. Remove ANDROID_HOME user environment variable ─────────────────────────

Write-Step "Checking ANDROID_HOME user environment variable..."
$current = [System.Environment]::GetEnvironmentVariable('ANDROID_HOME', 'User')
if ($current) {
    [System.Environment]::SetEnvironmentVariable('ANDROID_HOME', $null, 'User')
    Write-Ok "ANDROID_HOME removed (was: $current)"
} else {
    Write-Ok "ANDROID_HOME is not set — nothing to do."
}

# ── 4. Remove Microsoft OpenJDK 21 (if installed by install.ps1) ─────────────

Write-Step "Checking for Microsoft OpenJDK 25..."
$winget = Get-Command winget -ErrorAction SilentlyContinue
if ($winget) {
    $installed = & winget list --id Microsoft.OpenJDK.25 2>&1 | Out-String
    if ($installed -match 'Microsoft.OpenJDK.25') {
        Write-Warn "Removing Microsoft OpenJDK 25..."
        & winget uninstall --id Microsoft.OpenJDK.25 --silent 2>&1 | Out-Null
        if ($LASTEXITCODE -eq 0) {
            Write-Ok "Microsoft OpenJDK 25 removed."
        } else {
            Write-Fail "Failed to remove Microsoft OpenJDK 25. Try: winget uninstall --id Microsoft.OpenJDK.25"
        }
    } else {
        Write-Ok "Microsoft OpenJDK 25 is not installed — nothing to do."
    }
} else {
    Write-Warn "winget not available — skipping OpenJDK removal."
}

# ── Done ──────────────────────────────────────────────────────────────────────

Write-Host ""
Write-Host "  Uninstall complete." -ForegroundColor Green
Write-Host ""
