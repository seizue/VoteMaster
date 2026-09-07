$desktop = [Environment]::GetFolderPath('Desktop')
$shortcutPath = Join-Path $desktop "VoteMaster Launcher.lnk"
$wsh = New-Object -ComObject WScript.Shell
$sc = $wsh.CreateShortcut($shortcutPath)
$pwshCmd = Get-Command pwsh -ErrorAction SilentlyContinue
$pwshPath = if ($pwshCmd) { $pwshCmd.Source } else { "pwsh.exe" }
$sc.TargetPath = $pwshPath
$sc.Arguments = "-ExecutionPolicy Bypass -NoProfile -File `"$PSScriptRoot\Start-VoteMaster.ps1`""
$sc.WorkingDirectory = $PSScriptRoot
$sc.IconLocation = "$PSScriptRoot\VoteMaster.ico,0"
$sc.Description = "VoteMaster Interactive CLI Launcher"
$sc.Save()
Write-Host "Created shortcut: $shortcutPath" -ForegroundColor Green
