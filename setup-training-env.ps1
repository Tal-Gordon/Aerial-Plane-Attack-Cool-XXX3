<#
.SYNOPSIS
    Installs the bundled ML-Agents environment into an already-built Windows player.

.DESCRIPTION
    Ship this script beside the game executable and Launch (training).bat. On first run it
    downloads the large environment from the pinned GitHub Release and extracts it into the
    player's StreamingAssets directory. Later runs skip setup when python.exe is present.
#>
[CmdletBinding()]
param(
    [string]$Repo = "Tal-Gordon/Aerial-Plane-Attack-Cool-XXX3",
    [string]$Tag = "env-v1",
    [string]$Asset = "mlagents-env.tar.zst",
    [string]$GameDataFolder = "Aerial Plane Attack Cool XXX3_Data"
)

$ErrorActionPreference = "Stop"

$streaming = Join-Path $PSScriptRoot "$GameDataFolder\StreamingAssets"
$destDir = Join-Path $streaming "mlagents-env"
$marker = Join-Path $destDir "python.exe"

if (Test-Path $marker) {
    Write-Host "ML-Agents environment is already installed." -ForegroundColor Green
    return
}

$url = "https://github.com/$Repo/releases/download/$Tag/$Asset"
$tmp = Join-Path $env:TEMP $Asset
$curl = Join-Path $env:SystemRoot "System32\curl.exe"
$tar = Join-Path $env:SystemRoot "System32\tar.exe"

if (-not (Test-Path $curl)) {
    throw "curl.exe is required but was not found. Download $url manually and save it as $tmp"
}
if (-not (Test-Path $tar)) {
    throw "tar.exe is required but was not found."
}

New-Item -ItemType Directory -Path $streaming -Force | Out-Null

Write-Host "Downloading the ML-Agents environment (~1.7 GB)..." -ForegroundColor Cyan
Write-Host "The download resumes automatically if this setup is interrupted."
& $curl -L --fail --retry 5 --retry-delay 2 -C - -o $tmp $url
if ($LASTEXITCODE -ne 0) {
    throw "Download failed (curl exit $LASTEXITCODE). Run the launcher again to resume."
}

Write-Host "Extracting the environment (~3.3 GB installed)..." -ForegroundColor Cyan
& $tar -xf $tmp -C $streaming
if ($LASTEXITCODE -ne 0) {
    throw "Extraction failed (tar exit $LASTEXITCODE). This Windows tar may lack Zstandard support."
}

if (-not (Test-Path $marker)) {
    throw "Extraction finished, but $marker is missing."
}

Remove-Item $tmp -Force
Write-Host "ML-Agents environment installed successfully." -ForegroundColor Green
