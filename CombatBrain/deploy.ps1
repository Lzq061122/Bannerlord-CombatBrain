param(
    [string]$GameDir = "E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord"
)

$ErrorActionPreference = "Stop"
$src = Split-Path -Parent $MyInvocation.MyCommand.Path
$dll = Join-Path $src "bin\Win64_Shipping_Client\CombatBrain.dll"
$xml = Join-Path $src "SubModule.xml"

$modRoot = Join-Path $GameDir "Modules\CombatBrain"
$modBin  = Join-Path $modRoot "bin\Win64_Shipping_Client"

New-Item -ItemType Directory -Force -Path $modBin | Out-Null
Copy-Item $xml -Destination (Join-Path $modRoot "SubModule.xml") -Force
Copy-Item $dll -Destination (Join-Path $modBin "CombatBrain.dll") -Force

# P1-2: sync ModuleData config (opponent_priors.json etc.) into the deployed module.
# Without it, OpponentModel.LoadPriors always falls back to BuildDefault()
# (it reads Modules\CombatBrain\ModuleData\, see OpponentModelGlue.LoadPriors).
# Idempotent: Force-create dir, Copy-Item -Force overwrite; safe to re-run.
# NOTE: keep this script pure ASCII -- PowerShell 5.1 reads BOM-less files as
# ANSI/GBK and mojibake in comments can even swallow newlines and break parsing.
$srcData = Join-Path $src "ModuleData"
if (Test-Path $srcData) {
    $modData = Join-Path $modRoot "ModuleData"
    New-Item -ItemType Directory -Force -Path $modData | Out-Null
    Get-ChildItem $srcData -File | ForEach-Object {
        Copy-Item $_.FullName -Destination (Join-Path $modData $_.Name) -Force
    }
    Write-Host "ModuleData synced -> $modData"
}
Write-Host "Deploy done -> $modRoot"
