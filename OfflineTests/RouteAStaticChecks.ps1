param(
    [string]$GameBin = 'E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client'
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$gameDll = Join-Path $GameBin 'TaleWorlds.MountAndBlade.dll'
$modDll = Join-Path $repo 'CombatBrain\bin\Win64_Shipping_Client\CombatBrain.dll'
function Assert-Contract([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
    Write-Output "[PASS] $Message"
}
function Remove-CSharpTrivia([string]$Code) {
    [regex]::Replace($Code, '(?s)/\*.*?\*/|//[^\r\n]*|@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"', ' ')
}
function Read-Method([string]$Code, [string]$Signature) {
    $at = $Code.IndexOf($Signature, [StringComparison]::Ordinal)
    if ($at -lt 0) { throw "Missing method $Signature" }
    $start = $Code.IndexOf('{', $at)
    $depth = 0
    for ($i = $start; $i -lt $Code.Length; $i++) {
        if ($Code[$i] -eq '{') { $depth++ }
        if ($Code[$i] -eq '}') { $depth--; if ($depth -eq 0) { return $Code.Substring($start, $i - $start + 1) } }
    }
    throw "Unclosed method $Signature"
}
$writes = @()
foreach ($path in (& rg --files (Join-Path $repo 'CombatBrain') -g '*.cs' -g '!obj/**' -g '!bin/**')) {
    $code = Remove-CSharpTrivia ([IO.File]::ReadAllText($path))
    foreach ($match in [regex]::Matches($code, '\.Set(?:Movement|Facing|Arrangement)Order\s*\(')) {
        $writes += [PSCustomObject]@{ Path = $path; Call = $match.Value }
    }
}
Assert-Contract ($writes.Count -eq 4) 'Production has exactly four direct formation setters'
Assert-Contract (@($writes | Where-Object {$_.Path.EndsWith('FormationCommandExecutor.cs')}).Count -eq 3) 'Executor owns three domain setters'
Assert-Contract (@($writes | Where-Object {$_.Path.EndsWith('RouteAOrderAdapter.cs')}).Count -eq 1) 'Approved RouteA adapter owns one Movement setter'
$adapterSource = Remove-CSharpTrivia ([IO.File]::ReadAllText((Join-Path $repo 'CombatBrain\Ai\RouteAOrderAdapter.cs')))
$adapter = Read-Method $adapterSource 'public static bool TryApply('
Assert-Contract ($adapter -match '\.SetMovementOrder\(' -and $adapter -match '\.TrySelect\(') 'Adapter gates its only setter through the production selection controller'

foreach ($name in @('CbHoldBehavior','CbAdvanceBehavior','CbRejoinBehavior','CbRetreatBehavior')) {
    $code = (& ilspycmd -r $GameBin -t "CombatBrain.Ai.$name" $modDll) -join "`n"
    if ($LASTEXITCODE -ne 0) { throw "Decompile failed: $name" }
    $calculate = Read-Method (Remove-CSharpTrivia $code) 'CalculateCurrentOrder()'
    Assert-Contract ($calculate -notmatch 'SetMovementOrder|TryApply|RecordBehaviorSelected|\.Selected\(') "$name compiled candidate calculation has no write/selection side effects"
    Assert-Contract ($calculate -match 'IsCurrentOrderChanged = false') "$name cannot leak its prepared order into FormationAI fallback apply"
}
$ai = (& ilspycmd -r $GameBin -t 'TaleWorlds.MountAndBlade.FormationAI' $gameDll) -join "`n"
$find = Read-Method (Remove-CSharpTrivia $ai) 'private bool FindBestBehavior()'
Assert-Contract ($find.IndexOf('PrecalculateMovementOrder()') -lt $find.IndexOf('ActiveBehavior = behaviorComponent')) 'Installed FormationAI precalculates candidates before final selection'
$tick = Read-Method (Remove-CSharpTrivia $ai) 'private void TickOccasionally(float dt)'
$normalEnd = $tick.IndexOf('return;', $tick.IndexOf('ActiveBehavior.TickOccasionally()'))
Assert-Contract ($normalEnd -gt 0 -and $normalEnd -lt $tick.IndexOf('_formation.SetMovementOrder(')) 'Installed FormationAI returns after normal selected TickOccasionally; setter below is fallback only'
$formation = (& ilspycmd -r $GameBin -t 'TaleWorlds.MountAndBlade.Formation' $gameDll) -join "`n"
$restore = Read-Method (Remove-CSharpTrivia $formation) 'public void SetControlledByAI('
Assert-Contract ($restore -match 'ForceTickOccasionally = true' -and $restore -match 'AI.Tick\(\)' -and $restore -match 'SetMovementOrder\(AI.ActiveBehavior.CurrentOrder\)') 'Installed AI-control restoration has a forced tick followed by a separate engine setter'
$missionSource = Remove-CSharpTrivia ([IO.File]::ReadAllText((Join-Path $repo 'CombatBrain\Ai\CombatAiMissionLogic.cs')))
Assert-Contract ($missionSource.IndexOf('ApplyRouteASingle(mission, CbRouteAPoC.Enabled)') -lt $missionSource.IndexOf('_commandExecutor.Tick(mission,')) 'Production reserves RouteA Movement before ordinary executor writes'
foreach ($hook in @('public override void OnMissionStateFinalized()', 'public override void OnClearScene()')) {
    $method = Read-Method $missionSource $hook
    Assert-Contract ($method.IndexOf('CbRouteAPoC.ResetAll()') -ge 0 -and $method.IndexOf('CbRouteAPoC.ResetAll()') -lt $method.IndexOf('base.')) "$hook releases RouteA before native teardown"
}
Assert-Contract ((Read-Method $missionSource 'private void Init()') -match 'catch[\s\S]*CbRouteAPoC.ResetAll') 'Initialization exceptions release RouteA'
Assert-Contract ($adapterSource -match 'GetPathDistanceBetweenPositions' -and $adapterSource -match 'GetNavMesh\(' -and $adapterSource -match 'IsPositionInsideBoundaries') 'Rejoin production bridge uses public navmesh, boundary and path APIs'
Write-Output ('Installed game DLL SHA256: ' + (Get-FileHash -LiteralPath $gameDll -Algorithm SHA256).Hash)
Write-Output 'Static and installed-engine contract checks passed. This is not a live battle test.'
