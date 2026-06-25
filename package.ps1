<#
.SYNOPSIS
    Bundles the 'mlagents' conda env into the Unity project's StreamingAssets so a
    standalone build is self-contained (no Python install required on the target).

.DESCRIPTION
    Run this ONCE before making a Windows build (and again only when the env changes).
    It conda-pack's the working 'mlagents' env (pinned in environment.yml) into a
    relocatable folder at Assets/StreamingAssets/mlagents-env/. TrainerProcessLauncher
    picks that interpreter up automatically via Application.streamingAssetsPath.

    The output is multi-GB (CUDA torch). It is .gitignore'd on purpose: regenerate it,
    do not commit it.

.NOTES
    Requires: conda on PATH, the 'mlagents' env already created (see environment.yml).
    Windows 10+ ships 'tar', which this script uses to extract.
#>
[CmdletBinding()]
param(
    [string]$EnvName = "mlagents",
    [string]$Tarball = "mlagents-env.tar.gz"
)

$ErrorActionPreference = "Stop"

# Resolve paths relative to this script so it works from any CWD.
$root        = $PSScriptRoot
$tarballPath = Join-Path $root $Tarball
$destDir     = Join-Path $root "Assets\StreamingAssets\mlagents-env"

Write-Host "==> Packaging env '$EnvName' into $destDir" -ForegroundColor Cyan

# 1. Make sure conda + conda-pack are available.
if (-not (Get-Command conda -ErrorAction SilentlyContinue)) {
    throw "conda not found on PATH. Open an Anaconda/Miniconda prompt, or add conda to PATH."
}
Write-Host "==> Ensuring conda-pack is installed (base env)..."
conda install -n base -y conda-pack | Out-Null

# 2. Resolve the env's on-disk path. conda-pack's by-name (-n) lookup is flaky on some
#    setups ("Failed to determine path to environment"), so we pack by explicit prefix
#    (-p), which is robust. Parse `conda env list` for the line ending in the env name and
#    pull the drive-rooted path off it (handles spaces in the path, e.g. "Yoav Cohen").
$prefix = (& conda env list) |
    Where-Object { $_ -match "[\\/]$EnvName\s*$" } |
    ForEach-Object { if ($_ -match '([A-Za-z]:\\.*?)\s*$') { $matches[1] } } |
    Select-Object -First 1

if (-not $prefix) {
    # Fall back to the conventional miniconda location.
    $prefix = Join-Path $env:USERPROFILE "miniconda3\envs\$EnvName"
}
if (-not (Test-Path (Join-Path $prefix "python.exe"))) {
    throw "Could not locate env '$EnvName' (looked at '$prefix'). Is it created? See environment.yml."
}
Write-Host "==> Found env at: $prefix"

# Pack the env into a relocatable tarball (overwrite any previous one).
if (Test-Path $tarballPath) { Remove-Item $tarballPath -Force }
Write-Host "==> Running 'conda pack' (this is slow + produces a multi-GB file)..."
conda pack -p $prefix -o $tarballPath
if (-not (Test-Path $tarballPath)) { throw "conda pack did not produce $tarballPath" }

# 3. Extract into StreamingAssets (clean any previous bundle first).
if (Test-Path $destDir) {
    Write-Host "==> Removing previous bundle at $destDir..."
    Remove-Item $destDir -Recurse -Force
}
New-Item -ItemType Directory -Path $destDir -Force | Out-Null
Write-Host "==> Extracting into $destDir..."
tar -xzf $tarballPath -C $destDir

# 4. Fix the absolute paths baked into the packed env. REQUIRED after relocation.
$condaUnpack = Join-Path $destDir "Scripts\conda-unpack.exe"
if (-not (Test-Path $condaUnpack)) {
    throw "conda-unpack.exe not found at $condaUnpack. The pack/extract step likely failed."
}
Write-Host "==> Running conda-unpack to repair env paths..."
& $condaUnpack

# 5. Smoke-test: the bundled python must be able to import the mlagents trainer.
$bundledPython = Join-Path $destDir "python.exe"
$smokeCode = "import mlagents.trainers; import torch; print('mlagents + torch import OK; CUDA available:', torch.cuda.is_available())"
Write-Host "==> Smoke-testing bundled interpreter..."
& $bundledPython -c $smokeCode
if ($LASTEXITCODE -ne 0) { throw "Bundled interpreter failed its smoke test." }

# 6. The tarball is no longer needed once extracted.
Remove-Item $tarballPath -Force

Write-Host ""
Write-Host "==> Done. Bundled env is at $destDir" -ForegroundColor Green
Write-Host "    Now make a Windows build in Unity; StreamingAssets will be copied into the build's _Data folder automatically." -ForegroundColor Green
