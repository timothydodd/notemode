#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Smoke-tests the direct-download installer on a clean Windows machine (CI runs it on windows-latest).

.DESCRIPTION
  Silent install -> files, Start menu shortcut, uninstall entry and Explorer context menu ->
  upgrade over itself -> uninstall leaves no files, shortcut, context menu or uninstall entry behind,
  and removes file associations the app made for the current user -> a silent install without the
  context menu task leaves it out.

  Changes the machine it runs on; meant for throwaway CI runners and test VMs.

.EXAMPLE
  .\packaging\Test-Installer.ps1 -Installer artifacts\NoteMode-Setup-0.3.0.exe
#>
param(
    [Parameter(Mandatory)][string]$Installer,
    [string]$LogDir = (Join-Path ([System.IO.Path]::GetTempPath()) 'notemode-installer-logs')
)

$ErrorActionPreference = 'Stop'
$appDir = Join-Path $env:ProgramFiles 'NoteMode'
$exe = Join-Path $appDir 'NoteMode.exe'
$uninstallKey = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{D56F8563-E0D0-44E4-AC8E-0D7CE1726DEE}_is1'
$fileMenuKey = 'Registry::HKEY_LOCAL_MACHINE\SOFTWARE\Classes\*\shell\EditWithNoteMode'
$folderMenuKey = 'Registry::HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\EditWithNoteMode'
$shortcut = Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs\NoteMode\NoteMode.lnk'
New-Item $LogDir -ItemType Directory -Force | Out-Null
$failures = [System.Collections.Generic.List[string]]::new()

function Check([bool]$condition, [string]$what) {
    if ($condition) { Write-Host "  ok   $what" }
    else { Write-Host "  FAIL $what" -ForegroundColor Red; $failures.Add($what) }
}

function Invoke-Setup([string]$exePath, [string[]]$extra = @()) {
    $log = Join-Path $LogDir ("{0}-{1}.log" -f [IO.Path]::GetFileNameWithoutExtension($exePath), [DateTime]::Now.Ticks)
    $arguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/LOG=`"$log`"") + $extra
    $p = Start-Process -FilePath $exePath -ArgumentList $arguments -Wait -PassThru
    return $p.ExitCode
}

function Invoke-Uninstall {
    $uninstaller = Join-Path $appDir 'unins000.exe'
    if (-not (Test-Path $uninstaller)) { throw "No uninstaller at $uninstaller" }
    Start-Process -FilePath $uninstaller -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART' -Wait | Out-Null
    # The uninstaller hands over to a copy of itself in %TEMP%; wait for the files to go.
    for ($i = 0; $i -lt 120 -and (Test-Path $uninstaller); $i++) { Start-Sleep -Milliseconds 500 }
}

function Test-Installed([string]$label, [bool]$contextMenu = $true) {
    Check (Test-Path $exe) "$label - NoteMode.exe installed"
    Check (Test-Path (Join-Path $appDir 'Assets\FileIcons\text.ico')) "$label - file type icons installed"
    Check (Test-Path $shortcut) "$label - Start menu shortcut"
    Check (Test-Path $uninstallKey) "$label - uninstall entry"
    if ($contextMenu) {
        $command = (Get-ItemProperty -LiteralPath "$fileMenuKey\command").'(default)'
        Check ($command -eq "`"$exe`" `"%1`"") "$label - 'Edit with NoteMode' runs the installed exe ($command)"
        Check (Test-Path -LiteralPath $folderMenuKey) "$label - 'Open NoteMode here' on folder backgrounds"
    }
    else {
        Check (-not (Test-Path -LiteralPath $fileMenuKey)) "$label - no context menu without the task"
    }
}

Write-Host "Install $Installer"
Check ((Invoke-Setup $Installer) -eq 0) 'install exits 0'
Test-Installed 'fresh install'

# Stand-in for an association made from Settings > File associations, which the uninstaller removes.
New-Item 'HKCU:\Software\Classes\.notemodetest' -Force | Set-ItemProperty -Name '(default)' -Value 'NoteMode.Text'
New-Item 'HKCU:\Software\Classes\NoteMode.Text\shell\open\command' -Force | Set-ItemProperty -Name '(default)' -Value "`"$exe`" `"%1`""

Write-Host 'Upgrade over itself'
Check ((Invoke-Setup $Installer) -eq 0) 'reinstall exits 0'
Test-Installed 'after upgrade'

Write-Host 'Uninstall'
Invoke-Uninstall
Check (-not (Test-Path $exe)) 'app files removed'
Check (-not (Test-Path $shortcut)) 'Start menu shortcut removed'
Check (-not (Test-Path $uninstallKey)) 'uninstall entry removed'
Check (-not (Test-Path -LiteralPath $fileMenuKey)) "'Edit with NoteMode' removed"
Check (-not (Test-Path -LiteralPath $folderMenuKey)) "'Open NoteMode here' removed"
Check (-not (Test-Path 'HKCU:\Software\Classes\.notemodetest')) 'per-user extension association removed'
Check (-not (Test-Path 'HKCU:\Software\Classes\NoteMode.Text')) 'per-user NoteMode ProgId removed'

Write-Host 'Install without the context menu task'
Check ((Invoke-Setup $Installer @('/TASKS=""')) -eq 0) 'install without tasks exits 0'
Test-Installed 'no tasks' -contextMenu $false
Invoke-Uninstall
Check (-not (Test-Path $exe)) 'removed again'

if ($failures.Count -gt 0) {
    Write-Host "`n$($failures.Count) check(s) failed. Setup logs: $LogDir" -ForegroundColor Red
    exit 1
}
Write-Host "`nAll installer checks passed."
exit 0
