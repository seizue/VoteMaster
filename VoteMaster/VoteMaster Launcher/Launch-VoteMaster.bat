@echo off
setlocal EnableDelayedExpansion
title VoteMaster Launcher

:: --- Check for PowerShell 7 (pwsh) -----------------------------------------
where pwsh >nul 2>&1
if %errorlevel% equ 0 goto :launch

echo.
echo  [VoteMaster Launcher]
echo  PowerShell 7 is required but was not found on this machine.
echo.
echo  Attempting to install PowerShell 7 automatically...
echo.

:: --- Try winget first -------------------------------------------------------
where winget >nul 2>&1
if %errorlevel% equ 0 (
    echo  [Method 1] Installing via winget...
    winget install --id Microsoft.PowerShell --source winget --accept-source-agreements --accept-package-agreements
    if !errorlevel! equ 0 (
        echo.
        echo  [OK] PowerShell 7 installed via winget.
        goto :refresh
    )
    echo  [!] winget install failed. Trying direct download...
)

:: --- Fallback: download MSI via PowerShell 5 --------------------------------
echo  [Method 2] Downloading PowerShell 7 MSI installer...
echo.
set "PS7_URL=https://github.com/PowerShell/PowerShell/releases/latest/download/PowerShell-7.6.5-win-x64.msi"
set "PS7_MSI=%TEMP%\PowerShell-7-win-x64.msi"

powershell -NoProfile -Command ^
    "Write-Host '  Downloading installer, please wait...' -ForegroundColor Cyan;" ^
    "[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12;" ^
    "Invoke-WebRequest -Uri '%PS7_URL%' -OutFile '%PS7_MSI%' -UseBasicParsing"

if not exist "%PS7_MSI%" (
    echo.
    echo  [ERROR] Download failed. Please install PowerShell 7 manually:
    echo          https://aka.ms/powershell
    echo.
    pause
    exit /b 1
)

echo.
echo  [OK] Download complete. Running installer (UAC prompt may appear)...
echo.
msiexec /i "%PS7_MSI%" /quiet /norestart ADD_EXPLORER_CONTEXT_MENU_OPENPOWERSHELL=1 ENABLE_PSREMOTING=0 REGISTER_MANIFEST=1
if %errorlevel% neq 0 (
    echo  [ERROR] Installation failed. Try running the MSI manually:
    echo          %PS7_MSI%
    echo.
    pause
    exit /b 1
)

del /f /q "%PS7_MSI%" >nul 2>&1

:: --- Refresh PATH so pwsh is visible in this session ------------------------
:refresh
echo.
echo  [OK] Refreshing environment PATH...
for /f "tokens=2*" %%A in ('reg query "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment" /v Path 2^>nul') do set "SYS_PATH=%%B"
for /f "tokens=2*" %%A in ('reg query "HKCU\Environment" /v Path 2^>nul') do set "USR_PATH=%%B"
set "PATH=%SYS_PATH%;%USR_PATH%"

where pwsh >nul 2>&1
if %errorlevel% neq 0 (
    echo.
    echo  [!] PowerShell 7 was installed but requires a terminal restart to use.
    echo      Please close this window and re-run Launch-VoteMaster.bat.
    echo.
    pause
    exit /b 0
)

:: --- Launch the PowerShell 7 launcher ---------------------------------------
:launch
echo.
echo  Launching VoteMaster...
echo.
pwsh -ExecutionPolicy Bypass -NoProfile -File "%~dp0Start-VoteMaster.ps1"
exit /b 0