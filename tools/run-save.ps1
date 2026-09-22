# Launches the game straight into a save, for checking the mod against a real colony without clicking
# through menus. The game reads -settlementName and -saveName itself (its AutoStarter); this script only
# passes them through. By default it goes through Steam (steam.exe -applaunch), because Timberborn.exe started
# on its own hands over to Steam and loses its arguments. With -Seconds it closes the game again after that long
# and prints the mod's log lines.
#
#   .\tools\run-save.ps1 -Settlement "My town" -Save "2026-09-21 13h12m, Day 17-3.autosave"
#   .\tools\run-save.ps1 -Settlement "My town" -Save "..." -Seconds 240
param(
    [Parameter(Mandatory = $true)][string]$Settlement,
    [Parameter(Mandatory = $true)][string]$Save,
    [int]$Seconds = 0,
    [switch]$Direct,
    [string]$GameDir = "C:\Program Files (x86)\Steam\steamapps\common\Timberborn",
    [string]$SteamExe = "C:\Program Files (x86)\Steam\steam.exe"
)
$ErrorActionPreference = "Stop"
$log = Join-Path $env:USERPROFILE "AppData\LocalLow\Mechanistry\Timberborn\Player.log"
$gameArguments = "-settlementName `"$Settlement`" -saveName `"$Save`""
if ($Direct -or -not (Test-Path $SteamExe)) {
    Start-Process -FilePath (Join-Path $GameDir "Timberborn.exe") -ArgumentList $gameArguments | Out-Null
    Write-Host "Started Timberborn.exe directly, loading '$Save' from '$Settlement'."
} else {
    Start-Process -FilePath $SteamExe -ArgumentList "-applaunch 1062090 $gameArguments" | Out-Null
    Write-Host "Asked Steam to start Timberborn loading '$Save' from '$Settlement'."
}
if ($Seconds -gt 0) {
    Start-Sleep -Seconds $Seconds
    Get-Process -Name "Timberborn" -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 2
    Get-Content $log | Select-String -Pattern "\[HungryPathing\]|Exception|Error" | ForEach-Object { $_.Line }
}
