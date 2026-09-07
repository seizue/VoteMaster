#Requires -Version 7.0
<#
.SYNOPSIS
    VoteMaster Launcher - Powered by PwshSpectreConsole (Spectre.Console)
.DESCRIPTION
    A polished terminal UI launcher for VoteMaster using Spectre.Console widgets.
    Requires PowerShell 7+ and auto-installs PwshSpectreConsole on first run.
#>

# ─── Bootstrap: Install PwshSpectreConsole if missing ──────────────────────
if (-not (Get-Module -ListAvailable -Name PwshSpectreConsole)) {
    Write-Host "  Installing PwshSpectreConsole..." -ForegroundColor Cyan
    Install-Module -Name PwshSpectreConsole -Scope CurrentUser -Force -AllowClobber
}
Import-Module PwshSpectreConsole -ErrorAction Stop

# ─── Bootstrap: Register Desktop shortcut on first run ─────────────────────
$ShortcutPath = Join-Path ([Environment]::GetFolderPath('Desktop')) 'VoteMaster Launcher.lnk'
if (-not (Test-Path $ShortcutPath)) {
    try {
        $pwshExe = Get-Command pwsh -ErrorAction SilentlyContinue
        $wsh = New-Object -ComObject WScript.Shell
        $lnk = $wsh.CreateShortcut($ShortcutPath)
        $lnk.TargetPath = if ($pwshExe) { $pwshExe.Source } else { 'pwsh.exe' }
        $lnk.Arguments = "-ExecutionPolicy Bypass -NoProfile -File `"$($MyInvocation.MyCommand.Path)`""
        $lnk.WorkingDirectory = $PSScriptRoot
        $lnk.Description = 'VoteMaster Launcher — Start, stop and manage VoteMaster'
        $lnk.Hotkey = 'CTRL+ALT+V'
        $lnk.WindowStyle = 1
        $lnk.Save()
        Write-Host "  [Shortcut created on Desktop — Hotkey: Ctrl+Alt+V]" -ForegroundColor DarkCyan
        Start-Sleep 1
    }
    catch {
        # Non-fatal: shortcut creation is a convenience, not a requirement
        Write-Host "  [Note: Could not create Desktop shortcut: $($_.Exception.Message)]" -ForegroundColor DarkYellow
    }
}

# ─── Configuration ─────────────────────────────────────────────────────────
# $PSScriptRoot is the "VoteMaster Launcher" folder; project is one level up
$ProjectPath = Join-Path $PSScriptRoot '..\VoteMaster\VoteMaster.csproj'
$WebPort = 5000
$LogFile = Join-Path $env:TEMP 'VoteMaster.log'

# ─── Process helpers ───────────────────────────────────────────────────────
function Get-AppProcess {
    # 1. Try finding process listening on the web port first (most accurate)
    try {
        $conn = Get-NetTCPConnection -LocalPort $WebPort -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($conn -and $conn.OwningProcess) {
            $p = Get-CimInstance Win32_Process -Filter "ProcessId=$($conn.OwningProcess)" -ErrorAction SilentlyContinue
            if ($p) { return $p }
        }
    }
    catch { }

    # 2. Check for VoteMaster.exe or dotnet process running VoteMaster
    $proc = Get-CimInstance Win32_Process | Where-Object {
        ($_.Name -eq 'VoteMaster.exe') -or
        ($_.Name -eq 'dotnet.exe' -and ($_.CommandLine -like '*VoteMaster*' -or $_.CommandLine -like '*run*'))
    } | Select-Object -First 1

    return $proc
}

function Test-PortOpen([string]$ip = '127.0.0.1', [int]$port = 5000) {
    try {
        $tcp = [System.Net.Sockets.TcpClient]::new()
        $async = $tcp.BeginConnect($ip, $port, $null, $null)
        $wait = $async.AsyncWaitHandle.WaitOne(800)
        if ($wait -and $tcp.Connected) {
            $tcp.EndConnect($async)
            $tcp.Close()
            return $true
        }
        $tcp.Close()
        return $false
    }
    catch {
        return $false
    }
}

function Test-AppRunning {
    if (Test-PortOpen '127.0.0.1' $WebPort) { return $true }
    if ($null -ne (Get-AppProcess)) { return $true }
    return $false
}

function Get-UptimeString {
    $proc = Get-AppProcess
    if ($null -eq $proc -or $null -eq $proc.CreationDate) { return $null }
    $up = (Get-Date) - $proc.CreationDate
    return '{0:D2}h {1:D2}m {2:D2}s' -f [int]$up.TotalHours, $up.Minutes, $up.Seconds
}

# ─── Console Encoding (Anthony Simmon: UTF-8 for full emoji & symbol support) ─
[Console]::InputEncoding = [System.Text.Encoding]::UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

# ─── Header banner ─────────────────────────────────────────────────────────
function Show-Header {
    Clear-Host
    Write-Host ""
    "  [bold teal]VoteMaster[/] [white]CLI[/] [grey]| Secure Voting Platform[/]" | Write-SpectreHost
    Write-Host ""
}

# ─── Status panel ──────────────────────────────────────────────────────────
function Show-StatusPanel {
    if (Test-AppRunning) {
        $proc = Get-AppProcess
        $pidStr = if ($proc) { "$($proc.ProcessId)" } else { "Active" }
        $uptime = Get-UptimeString
        $uptimeStr = if ($uptime) { $uptime } else { "Just now" }
        "  [bold teal]|[/] [bold green]Running[/]" | Write-SpectreHost
        "    [grey]• PID     :[/] [teal]$pidStr[/]" | Write-SpectreHost
        "    [grey]• Uptime  :[/] [teal]$uptimeStr[/]" | Write-SpectreHost
        "    [grey]• URL     :[/] [bold underline teal]http://localhost:$WebPort[/]" | Write-SpectreHost
    }
    else {
        "  [bold teal]|[/] [bold white]Stopped[/]" | Write-SpectreHost
        "    [grey]• Status  :[/] [grey]Application is currently idle[/]" | Write-SpectreHost
        "    [grey]• Target  :[/] [teal]http://localhost:$WebPort[/]" | Write-SpectreHost
    }
    Write-Host ""
}

# ─── Start app ─────────────────────────────────────────────────────────────
function Start-App {
    if (Test-AppRunning) {
        "  [bold teal]|[/] [yellow]VoteMaster is already running.[/]" | Write-SpectreHost
        Start-Sleep 1; return
    }
    if (-not (Test-Path $ProjectPath)) {
        "  [bold teal]|[/] [red]Project not found:[/] [grey]$ProjectPath[/]" | Write-SpectreHost
        "  [grey]   Ensure the 'VoteMaster Launcher' folder sits next to the VoteMaster project folder.[/]" | Write-SpectreHost
        Start-Sleep 3; return
    }

    $localProjectPath = $ProjectPath
    $localLogFile = $LogFile
    $localWebPort = $WebPort

    Clear-Host
    Show-Header
    "  [bold teal]|[/] [bold yellow]Starting[/]" | Write-SpectreHost
    "    [grey]• Status  :[/] [grey]Launching VoteMaster service in background...[/]" | Write-SpectreHost
    "    [grey]• Target  :[/] [teal]http://localhost:$localWebPort[/]" | Write-SpectreHost
    Write-Host ""

    Invoke-SpectreCommandWithStatus -Spinner "Dots2" -Title "[teal]Building & starting VoteMaster (waiting for port $localWebPort)...[/]" -Color "teal" -ScriptBlock {
        $errLog = [IO.Path]::ChangeExtension($localLogFile, '.err.log')
        Start-Process -FilePath 'dotnet' `
            -ArgumentList "run --project `"$localProjectPath`" --no-launch-profile" `
            -WorkingDirectory (Split-Path $localProjectPath) `
            -WindowStyle Hidden `
            -RedirectStandardOutput $localLogFile `
            -RedirectStandardError $errLog

        $attempts = 0; $started = $false
        while ($attempts -lt 45) {
            Start-Sleep -Milliseconds 1000
            if (Test-PortOpen '127.0.0.1' $localWebPort) {
                $started = $true; break
            }
            $attempts++
        }
        if (-not $started) {
            throw "Application did not respond after 45 seconds. Check log: $localLogFile"
        }
    }

    Clear-Host
    Show-Header
    Show-StatusPanel

    if (Test-AppRunning) {
        "  [bold teal]|[/] [bold green]VoteMaster started successfully.[/]" | Write-SpectreHost
        "    [grey]• URL     :[/] [bold underline teal]http://localhost:$WebPort[/]" | Write-SpectreHost
    }
    else {
        "  [bold teal]|[/] [yellow]Could not confirm startup. Check the log window.[/]" | Write-SpectreHost
    }
    Write-Host ""
    Read-Host "  Press Enter to continue"
}

# ─── Stop app ──────────────────────────────────────────────────────────────
function Stop-App {
    $proc = Get-AppProcess
    if ($null -eq $proc -and -not (Test-AppRunning)) {
        "  [bold teal]|[/] [yellow]VoteMaster is not running.[/]" | Write-SpectreHost
        Start-Sleep 1; return
    }

    Clear-Host
    Show-Header
    "  [bold teal]|[/] [bold red]Stopping[/]" | Write-SpectreHost
    "    [grey]• Status  :[/] [grey]Terminating VoteMaster processes...[/]" | Write-SpectreHost
    Write-Host ""

    $targetPid = if ($proc) { $proc.ProcessId } else { $null }
    $localPort = $WebPort

    Invoke-SpectreCommandWithStatus -Spinner "Dots2" -Title "[red]Stopping VoteMaster...[/]" -Color "red" -ScriptBlock {
        if ($targetPid) {
            Stop-Process -Id $targetPid -Force -ErrorAction SilentlyContinue
        }
        try {
            $conn = Get-NetTCPConnection -LocalPort $localPort -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($conn -and $conn.OwningProcess) {
                Stop-Process -Id $conn.OwningProcess -Force -ErrorAction SilentlyContinue
            }
        }
        catch { }

        Get-Process -Name 'VoteMaster' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
        Start-Sleep 1
    }

    Clear-Host
    Show-Header
    Show-StatusPanel
    "  [bold teal]|[/] [bold white]Application stopped.[/]" | Write-SpectreHost
    Start-Sleep 1
}

# ─── IP viewer ─────────────────────────────────────────────────────────────
function Show-IpInfo {
    Clear-Host
    Show-Header
    "  [bold teal]|[/] [bold white]Available Network Interfaces[/]" | Write-SpectreHost
    Write-Host ""

    $adapters = Get-NetIPAddress -AddressFamily IPv4 |
    Where-Object { $_.InterfaceAlias -notmatch 'Loopback' -and $_.IPAddress -ne '127.0.0.1' } |
    Sort-Object InterfaceAlias

    if (-not $adapters) {
        "  [yellow]No non-loopback interfaces found.[/]" | Write-SpectreHost
        Write-Host ""
        Read-Host "  Press Enter to continue"
        return
    }

    # Format choices with clean column padding so arrows and URLs align
    $choices = @()
    $choices += ("•  {0,-14} ->  {1}" -f "localhost", "http://localhost:$WebPort")
    foreach ($a in $adapters) {
        $choices += ("•  {0,-14} ->  {1}" -f $a.InterfaceAlias, "http://$($a.IPAddress):$WebPort")
    }
    $choices += "•  Back to menu"

    $sel = Read-SpectreSelection `
        -Choices $choices `
        -Title "  [bold teal]|[/] [white]Select an interface to copy its URL to clipboard[/]" `
        -Color "teal"

    if ($sel -notmatch 'Back to menu') {
        # Extract URL from "InterfaceName  ->  http://..."
        if ($sel -match '->\s+(http://\S+)') {
            $url = $matches[1]
            Set-Clipboard -Value $url
            Write-Host ""
            "  [bold teal]|[/] [grey]Copied to clipboard:[/] [white]$url[/]" | Write-SpectreHost
            Start-Sleep 1
        }
    }
    Write-Host ""
}

# ─── Log viewer ────────────────────────────────────────────────────────────
function Show-Log {
    if (-not (Test-Path $LogFile)) {
        "  [bold teal]|[/] [yellow]No log file found. Start the application first.[/]" | Write-SpectreHost
        Start-Sleep 2; return
    }
    Clear-Host
    Show-Header
    "  [bold teal]|[/] [bold white]Application Log[/] [grey](last 30 lines)[/]" | Write-SpectreHost
    Write-Host ""

    Get-Content $LogFile -Tail 30 | ForEach-Object {
        $line = "    $_"
        if ($_ -match 'error|fail|exception') {
            Write-Host $line -ForegroundColor Red
        }
        elseif ($_ -match 'warn') {
            Write-Host $line -ForegroundColor Yellow
        }
        elseif ($_ -match 'info|listen|start') {
            Write-Host $line -ForegroundColor Cyan
        }
        else {
            Write-Host $line -ForegroundColor DarkGray
        }
    }

    Write-Host ""
    Read-Host "  Press Enter to continue"
}

# ─── Main menu loop ────────────────────────────────────────────────────────
while ($true) {
    Show-Header
    Show-StatusPanel

    $running = Test-AppRunning

    $choices = if ($running) {
        @(" Stop Application", " Open Web Portal", " View Network Interfaces", " View Application Log", " Quit")
    }
    else {
        @(" Start Application", " Open Web Portal", " View Network Interfaces", " View Application Log", " Quit")
    }

    $action = Read-SpectreSelection `
        -Choices $choices `
        -Title "  [bold teal]|[/] [white]What [teal]action[/] would you like to perform[/]" `
        -Color "teal" `
        -PageSize 10

    "  [bold teal]|[/] [grey]Selected:[/] [teal]$action[/]" | Write-SpectreHost
    Start-Sleep -Milliseconds 250

    switch -Wildcard ($action) {
        "*Start Application*" { Start-App }
        "*Stop Application*" { Stop-App }
        "*Open Web Portal*" {
            Start-Process "http://localhost:$WebPort"
            "  [teal]Opened http://localhost:$WebPort[/]" | Write-SpectreHost
            Start-Sleep 1
        }
        "*View Network Interfaces*" { Show-IpInfo }
        "*Application Log*" { Show-Log }
        "*Quit*" {
            Write-Host ""
            "  [dim]Goodbye.[/]" | Write-SpectreHost
            Write-Host ""
            Start-Sleep -Milliseconds 250
            [System.Environment]::Exit(0)
        }
    }
}
