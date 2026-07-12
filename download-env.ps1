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

    Uses Windows' bundled curl.exe instead of Windows PowerShell's Invoke-WebRequest. The
    latter can be extremely slow for multi-gigabyte files because its legacy progress and
    response-stream handling add substantial overhead. curl also resumes a partial download.

    Private repo: an unauthenticated download 404s. Use gh release download instead, or pass
    suitable authentication to curl.
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
$curl = Join-Path $env:SystemRoot "System32\curl.exe"
if (-not (Test-Path $curl)) {
    throw "curl.exe was not found. Install curl, or download $url in a browser and save it as $tmp"
}

# -L follows GitHub's release-asset redirect. -C - resumes $tmp when a previous run was
# interrupted, while --retry handles transient network failures without restarting 1.7 GB.
& $curl -L --fail --retry 5 --retry-delay 2 -C - -o $tmp $url
if ($LASTEXITCODE -ne 0) {
    throw "Download failed (curl exit $LASTEXITCODE). Re-run the script to resume the partial file."
}

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
