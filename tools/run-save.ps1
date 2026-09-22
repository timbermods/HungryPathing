# Launches the game straight into a save, for checking the mod against a real colony without clicking
# through menus. The game reads -settlementName and -saveName itself (its AutoStarter); this script only
# passes them through and, with -Seconds, closes the game again after that long and prints the mod's log lines.
#
#   .\tools\run-save.ps1 -Settlement "My town" -Save "2026-09-21 13h12m, Day 17-3.autosave"
#   .\tools\run-save.ps1 -Settlement "My town" -Save "..." -Seconds 240
param(
    [Parameter(Mandatory = $true)][string]$Settlement,
    [Parameter(Mandatory = $true)][string]$Save,
    [int]$Seconds = 0,
    [string]$GameDir = "C:\Program Files (x86)\Steam\steamapps\common\Timberborn"
)
$ErrorActionPreference = "Stop"
$exe = Join-Path $GameDir "Timberborn.exe"
$log = Join-Path $env:USERPROFILE "AppData\LocalLow\Mechanistry\Timberborn\Player.log"
$process = Start-Process -FilePath $exe -ArgumentList @("-settlementName", "`"$Settlement`"", "-saveName", "`"$Save`"") -PassThru
Write-Host "Started Timberborn (pid $($process.Id)) loading '$Save' from '$Settlement'."
if ($Seconds -gt 0) {
    Start-Sleep -Seconds $Seconds
    if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
    Start-Sleep -Seconds 2
    Get-Content $log | Select-String -Pattern "\[HungryPathing\]|Exception|Error" | ForEach-Object { $_.Line }
}
