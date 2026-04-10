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
    -p:DebugType=portable `
    -p:DebugSymbols=true `
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

# ── 5. Locate R8 mapping file (deobfuscation) ────────────────────────────────

$mappingInOutput = Join-Path $OutputDir 'mapping.txt'
$mappingInBin    = Join-Path $PSScriptRoot '..\artifacts\bin\Khepri\release_net10.0-android\mapping.txt'
$mappingInBin    = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($mappingInBin)

if (Test-Path $mappingInOutput) {
    Write-Ok "R8 mapping: $mappingInOutput"
} elseif (Test-Path $mappingInBin) {
    Copy-Item $mappingInBin $mappingInOutput -Force
    Write-Ok "R8 mapping: $mappingInOutput"
} else {
    Write-Host "  [--] R8 mapping not found (R8 may not have run or path changed)" -ForegroundColor Yellow
}

# ── 6. Copy native debug symbols (.so files) ─────────────────────────────────
#
# Google Play rejects Mono/Xamarin runtime internals; exclude them.
# The zip must preserve ABI folder structure: arm64-v8a/libfoo.so, x86_64/libfoo.so

$symbolsSrc = Join-Path $PSScriptRoot '..\artifacts\obj\Khepri\release_net10.0-android\app_shared_libraries'
$symbolsSrc = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($symbolsSrc)

$excludedNames = @('libxamarin-app.so', 'libxamarin-app.dbg.so', 'assembly-store.so')
$soFiles = Get-ChildItem -Path $symbolsSrc -Filter '*.so' -Recurse -ErrorAction SilentlyContinue |
           Where-Object { $_.Name -notin $excludedNames }

if ($soFiles) {
    $symbolsZip = Join-Path $OutputDir 'native-symbols.zip'
    if (Test-Path $symbolsZip) { Remove-Item $symbolsZip -Force }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::Open($symbolsZip, 'Create')
    try {
        foreach ($file in $soFiles) {
            # Preserve relative path e.g. arm64-v8a/libfoo.so
            $entryName = $file.FullName.Substring($symbolsSrc.Length + 1).Replace('\', '/')
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $file.FullName, $entryName) | Out-Null
        }
    } finally {
        $zip.Dispose()
    }

    Write-Ok "Native symbols: $symbolsZip ($($soFiles.Count) files)"
} else {
    Write-Host "  [--] No .so files found for native symbols" -ForegroundColor Yellow
}

Write-Host ""
Write-Ok "AAB ready: $($aab.FullName)"
Write-Host ""
Write-Host "  Upload to Google Play Console:" -ForegroundColor DarkGray
Write-Host "    1. AAB:             $($aab.FullName)" -ForegroundColor DarkGray
Write-Host "    2. Mapping file:    $OutputDir\mapping.txt  (App Bundle explorer → Downloads → Mapping file)" -ForegroundColor DarkGray
Write-Host "    3. Native symbols:  $OutputDir\native-symbols.zip  (App Bundle explorer → Downloads → Native debug symbols)" -ForegroundColor DarkGray
Write-Host ""
