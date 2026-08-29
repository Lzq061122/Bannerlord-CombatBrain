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
Write-Host "部署完成 -> $modRoot"