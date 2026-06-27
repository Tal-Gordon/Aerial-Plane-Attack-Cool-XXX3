<#
.SYNOPSIS
    Downloads the bundled 'mlagents' Python env from a GitHub Release and extracts it into
    StreamingAssets, so a machine WITHOUT the env can run the RL modes (no Python install).

.DESCRIPTION
    The env is ~3.3 GB unpacked — too large to commit, so it ships as a single compressed
    Release asset (mlagents-env.tar.zst, ~1.7 GB). This script downloads it (skipping if the
    env is already present) and extracts it to Assets/StreamingAssets/mlagents-env/, which is
    exactly where TrainerProcessLauncher.FindCondaPython() looks for python.exe.

    Counterpart to package.ps1: package.ps1 PRODUCES the env on a dev machine; this script
    CONSUMES it on any machine that just needs to run. Re-run only when the env changes
    (publish a new Release and bump -Tag).

.NOTES
    Requires Windows 'tar' (System32) with zstd support — present on Windows 11 and recent
    Windows 10. If extraction fails complaining about the format, that tar lacks the zstd
    codec; install a zstd-capable tar, or re-publish the asset as .tar.xz.

    Private repo: Invoke-WebRequest can't auth, so the download 404s. Either make the repo
    public, or replace the download line with:  gh release download $Tag -R $Repo -p $Asset -D $env:TEMP
#>
[CmdletBinding()]
param(
    [string]$Repo  = "Tal-Gordon/Aerial-Plane-Attack-Cool-XXX3",
    [string]$Tag   = "env-v1",
    [string]$Asset = "mlagents-env.tar.zst"
)

$ErrorActionPreference = "Stop"

$root    = $PSScriptRoot
$destDir = Join-Path $root "Assets\StreamingAssets\mlagents-env"
$marker  = Join-Path $destDir "python.exe"

# Idempotent: if the interpreter is already there, there's nothing to do.
if (Test-Path $marker) {
    Write-Host "==> Env already present at $destDir - nothing to do." -ForegroundColor Green
    return
}

$streaming = Join-Path $root "Assets\StreamingAssets"
New-Item -ItemType Directory -Path $streaming -Force | Out-Null

$url = "https://github.com/$Repo/releases/download/$Tag/$Asset"
$tmp = Join-Path $env:TEMP $Asset

Write-Host "==> Downloading $url" -ForegroundColor Cyan
Write-Host "    (~1.7 GB - this takes a while)"
Invoke-WebRequest -Uri $url -OutFile $tmp

# The archive's top-level entry is 'mlagents-env/', so extracting into StreamingAssets
# yields StreamingAssets/mlagents-env/... with no double-nesting.
Write-Host "==> Extracting into $streaming" -ForegroundColor Cyan
& "$env:SystemRoot\System32\tar.exe" -xf $tmp -C $streaming
if ($LASTEXITCODE -ne 0) {
    throw "tar extraction failed (exit $LASTEXITCODE). This Windows 'tar' may lack zstd support."
}

if (-not (Test-Path $marker)) {
    throw "Extraction finished but $marker is missing - check the archive's internal layout."
}

Remove-Item $tmp -Force
Write-Host ""
Write-Host "==> Done. Env ready at $destDir" -ForegroundColor Green
Write-Host "    Open the project in Unity; RL modes will find this interpreter automatically." -ForegroundColor Green
