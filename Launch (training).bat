@echo off
setlocal

REM First-run setup downloads the large ML-Agents environment from the GitHub Release.
REM Later runs skip the download once python.exe is installed in StreamingAssets.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Aerial Plane Attack Cool XXX3_Data\StreamingAssets\setup-training-env.ps1"
if errorlevel 1 (
    echo.
    echo Training environment setup failed. See the error above.
    pause
    exit /b 1
)

start "" /D "%~dp0" "%~dp0Aerial Plane Attack Cool XXX3.exe" --mlagents-port 5004
