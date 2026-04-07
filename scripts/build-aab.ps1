#Requires -Version 5.1
<#
.SYNOPSIS
    Builds a signed release AAB for manual upload to Google Play Console.
.DESCRIPTION
    Produces a signed .aab file that can be uploaded manually to Google Play Console
    to activate the app before the CI/CD API publishing pipeline is set up.

    The AAB is written to: artifacts/aab/

.PARAMETER KeystorePath
    Path to the .keystore file. Defaults to 'khepri.keystore' in the repo root.
.PARAMETER KeyAlias
    Signing key alias. Defaults to 'khepri'.
#>
param(
    [string]$KeystorePath = (Join-Path $PSScriptRoot '..\artifacts\khepri.keystore'),
    [string]$KeyAlias     = 'khepri'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ProjectPath = Join-Path $PSScriptRoot '..\src\Khepri\Khepri.csproj'
$OutputDir   = Join-Path $PSScriptRoot '..\artifacts\aab'

# ── Helpers ───────────────────────────────────────────────────────────────────

function Write-Step([string]$msg) { Write-Host "  $msg"       -ForegroundColor Cyan  }
function Write-Ok([string]$msg)   { Write-Host "  [OK] $msg"  -ForegroundColor Green }
function Write-Fail([string]$msg) { Write-Host "  [!!] $msg"  -ForegroundColor Red   }

# ── Banner ────────────────────────────────────────────────────────────────────

Write-Host ""
Write-Host "  Khepri — build release AAB" -ForegroundColor White
Write-Host "  ────────────────────────────" -ForegroundColor DarkGray
Write-Host ""

# ── 1. Resolve keystore ───────────────────────────────────────────────────────

$KeystorePath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($KeystorePath)

if (-not (Test-Path $KeystorePath)) {
    Write-Fail "Keystore not found at: $KeystorePath"
    Write-Host ""
    Write-Host "  Create one with:" -ForegroundColor DarkGray
    Write-Host "    keytool -genkeypair -v -keystore khepri.keystore -alias khepri \" -ForegroundColor DarkGray
    Write-Host "      -keyalg RSA -keysize 2048 -validity 10000 \" -ForegroundColor DarkGray
    Write-Host "      -dname `"CN=Alan Keller, O=Ion Core Studios, C=CH`"" -ForegroundColor DarkGray
    Write-Host ""
    exit 1
}

Write-Ok "Keystore: $KeystorePath"

# ── 2. Prompt for passwords ───────────────────────────────────────────────────

$StorePass = Read-Host -Prompt "  Keystore password" -AsSecureString
$KeyPass   = Read-Host -Prompt "  Key password      " -AsSecureString

$storePlain = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [Runtime.InteropServices.Marshal]::SecureStringToBSTR($StorePass))
$keyPlain   = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [Runtime.InteropServices.Marshal]::SecureStringToBSTR($KeyPass))

# ── 3. Build signed AAB ───────────────────────────────────────────────────────

Write-Step "Building signed AAB (Release, net10.0-android)..."
Write-Host ""

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

& dotnet publish $ProjectPath `
    -f net10.0-android `
    -c Release `
    -o $OutputDir `
    -p:AndroidPackageFormats=aab `
    -p:AndroidKeyStore=true `
    -p:AndroidSigningKeyStore=$KeystorePath `
    -p:AndroidSigningKeyAlias=$KeyAlias `
    -p:AndroidSigningKeyPass=$keyPlain `
    -p:AndroidSigningStorePass=$storePlain

# Clear passwords from memory
$storePlain = $keyPlain = $null

if ($LASTEXITCODE -ne 0) {
    Write-Fail "Build failed (exit code $LASTEXITCODE)."
    exit $LASTEXITCODE
}

# ── 4. Locate and report the AAB ─────────────────────────────────────────────

$aab = Get-ChildItem -Path $OutputDir -Filter '*.aab' -Recurse |
       Sort-Object LastWriteTime -Descending |
       Select-Object -First 1

Write-Host ""
Write-Ok "AAB ready: $($aab.FullName)"
Write-Host ""
Write-Host "  Next steps:" -ForegroundColor DarkGray
Write-Host "    1. Go to play.google.com/console → Khepri → Internal testing" -ForegroundColor DarkGray
Write-Host "    2. Create release → Upload the AAB above" -ForegroundColor DarkGray
Write-Host "    3. Save as draft — this activates the app for API access" -ForegroundColor DarkGray
Write-Host ""
