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
    if ($start -lt 0) { throw "Missing method body $Signature" }
    $depth = 0
    for ($i = $start; $i -lt $Code.Length; $i++) {
        if ($Code[$i] -eq '{') { $depth++ }
        if ($Code[$i] -eq '}') { $depth--; if ($depth -eq 0) { return $Code.Substring($start, $i - $start + 1) } }
    }
    throw "Unclosed method $Signature"
}
function Test-CallOrder([string]$Code, [string]$First, [string]$Second) {
    $firstAt = $Code.IndexOf($First, [StringComparison]::Ordinal)
    $secondAt = $Code.IndexOf($Second, [StringComparison]::Ordinal)
    return $firstAt -ge 0 -and $secondAt -ge 0 -and $firstAt -lt $secondAt
}
function Read-CompiledType([string]$Dll, [string]$Type) {
    $code = (& ilspycmd --disable-updatecheck -r $GameBin -t $Type $Dll) -join "`n"
    if ($LASTEXITCODE -ne 0) { throw "Decompile failed: $Type" }
    return Remove-CSharpTrivia $code
}
Assert-Contract (Test-CallOrder 'First(); Second();' 'First()' 'Second()') 'Call-order checker accepts two present calls in order'
Assert-Contract (-not (Test-CallOrder 'Second();' 'First()' 'Second()')) 'Call-order checker rejects a missing first call'
Assert-Contract (-not (Test-CallOrder 'First();' 'First()' 'Second()')) 'Call-order checker rejects a missing second call'
Assert-Contract (-not (Test-CallOrder 'Second(); First();' 'First()' 'Second()')) 'Call-order checker rejects reversed calls'
$writes = @()
foreach ($path in (& rg --files (Join-Path $repo 'CombatBrain') -g '*.cs' -g '!**/obj/**' -g '!**/bin/**')) {
    $code = Remove-CSharpTrivia ([IO.File]::ReadAllText($path))
    foreach ($match in [regex]::Matches($code, '\.Set(?:Movement|Facing|Arrangement)Order\s*\(')) {
        $writes += [PSCustomObject]@{ Path = $path; Call = $match.Value }
    }
}
Assert-Contract ($writes.Count -eq 4) 'Production has exactly four direct formation setters'
Assert-Contract (@($writes | Where-Object {$_.Path.EndsWith('FormationCommandExecutor.cs')}).Count -eq 3) 'Executor owns three domain setters'
Assert-Contract (@($writes | Where-Object {$_.Path.EndsWith('RouteAOrderAdapter.cs')}).Count -eq 1) 'Approved RouteA adapter owns one Movement setter'
$adapterSource = Remove-CSharpTrivia ([IO.File]::ReadAllText((Join-Path $repo 'CombatBrain\Ai\RouteAOrderAdapter.cs')))
$adapter = Read-Method (Read-CompiledType $modDll 'CombatBrain.Ai.RouteAOrderAdapter') 'public static bool TryApply('
$selectionGuard = [regex]::Match($adapter, 'if\s*\(\s*!CbRouteAPoC\.Control\.TrySelect\([^;]*?\)\s*\)\s*\{\s*return false;\s*\}')
Assert-Contract ($selectionGuard.Success -and (Test-CallOrder $adapter $selectionGuard.Value '.SetMovementOrder(')) 'Compiled adapter rejects failed selection before its setter'
$routeA = Read-CompiledType $modDll 'CombatBrain.Ai.CbRouteAPoC'
$beginSession = Read-Method $routeA 'public static void BeginSession('
Assert-Contract ($beginSession -match 'Control\.ReleaseLiveText\s*=\s*\(?\s*(?:object\s+)?(\w+)\s*\)?\s*=>\s*LiveOwnershipText\(\1\s+as\s+Formation\)\s*;') 'Compiled BeginSession forwards the released identity to the live ownership probe'
Assert-Contract ($beginSession -match 'Control\.ReleaseGateText\s*=\s*\(\)\s*=>\s*GateText\(Enabled\)\s*;') 'Compiled BeginSession wires the release gate probe'

foreach ($name in @('CbHoldBehavior','CbAdvanceBehavior','CbRejoinBehavior','CbRetreatBehavior')) {
    $code = Read-CompiledType $modDll "CombatBrain.Ai.$name"
    $calculate = Read-Method $code 'CalculateCurrentOrder()'
    Assert-Contract ($calculate -notmatch 'SetMovementOrder|TryApply|RecordBehaviorSelected|\.Selected\(') "$name compiled candidate calculation has no write/selection side effects"
    Assert-Contract ($calculate -match 'IsCurrentOrderChanged = false') "$name cannot leak its prepared order into FormationAI fallback apply"
}
$ai = Read-CompiledType $gameDll 'TaleWorlds.MountAndBlade.FormationAI'
$find = Read-Method $ai 'private bool FindBestBehavior()'
Assert-Contract (Test-CallOrder $find 'PrecalculateMovementOrder()' 'ActiveBehavior =') 'Installed FormationAI precalculates candidates before final selection'
$tick = Read-Method $ai 'private void TickOccasionally(float dt)'
$activeTick = $tick.IndexOf('ActiveBehavior.TickOccasionally()')
$normalEnd = -1
if ($activeTick -ge 0) { $normalEnd = $tick.IndexOf('return;', $activeTick) }
$fallbackSetter = $tick.IndexOf('_formation.SetMovementOrder(')
Assert-Contract ($normalEnd -ge 0 -and $fallbackSetter -ge 0 -and $normalEnd -lt $fallbackSetter) 'Installed FormationAI returns after normal selected TickOccasionally; setter below is fallback only'
$formation = Read-CompiledType $gameDll 'TaleWorlds.MountAndBlade.Formation'
$restore = Read-Method $formation 'public void SetControlledByAI('
Assert-Contract ($restore -match 'ForceTickOccasionally = true' -and $restore -match 'AI.Tick\(\)' -and $restore -match 'SetMovementOrder\(AI.ActiveBehavior.CurrentOrder\)') 'Installed AI-control restoration has a forced tick followed by a separate engine setter'
$missionSource = Remove-CSharpTrivia ([IO.File]::ReadAllText((Join-Path $repo 'CombatBrain\Ai\CombatAiMissionLogic.cs')))
$missionTick = Read-Method $missionSource 'public override void OnMissionTick('
Assert-Contract (Test-CallOrder $missionTick 'ApplyRouteASingle(mission, CbRouteAPoC.Enabled)' '_commandExecutor.Tick(mission,') 'Production reserves RouteA Movement before ordinary executor writes'
$compiledTick = Read-Method (Read-CompiledType $modDll 'CombatBrain.Ai.CombatAiMissionLogic') 'public override void OnMissionTick('
$routeCall = [regex]::Match($compiledTick, 'ApplyRouteASingle\([^;]*CbRouteAPoC\.Enabled\)')
Assert-Contract ($routeCall.Success -and (Test-CallOrder $compiledTick $routeCall.Value '_commandExecutor.Tick(')) 'Compiled Mission tick reserves RouteA before executor writes'
foreach ($hook in @('public override void OnMissionStateFinalized()', 'public override void OnClearScene()')) {
    $method = Read-Method $missionSource $hook
    Assert-Contract (Test-CallOrder $method 'CbRouteAPoC.ResetAll()' 'base.') "$hook releases RouteA before native teardown"
}
Assert-Contract ((Read-Method $missionSource 'private void Init()') -match 'catch[\s\S]*CbRouteAPoC.ResetAll') 'Initialization exceptions release RouteA'
Assert-Contract ($adapterSource -match 'GetPathDistanceBetweenPositions' -and $adapterSource -match 'GetNavMesh\(' -and $adapterSource -match 'IsPositionInsideBoundaries') 'Rejoin production bridge uses public navmesh, boundary and path APIs'
$patches = Read-CompiledType $modDll 'CombatBrain.Coordination.SetOrderObserver+Patches'
foreach ($binding in @(
    @('SetMovementOrder', 'MovementPrefix', 'MovementOrder'),
    @('SetFacingOrder', 'FacingPrefix', 'FacingOrder'),
    @('SetArrangementOrder', 'ArrangementPrefix', 'ArrangementOrder')
)) {
    $target = [regex]::Match($formation, 'public void ' + $binding[0] + '\(' + $binding[2] + '\s+(?<name>\w+)\)')
    $prefix = [regex]::Match($patches, 'private static void ' + $binding[1] + '\(Formation\s+__instance,\s*' + $binding[2] + '\s+(?<name>\w+)(?:,|\))')
    Assert-Contract ($target.Success -and $prefix.Success -and $target.Groups['name'].Value -eq $prefix.Groups['name'].Value) ("Compiled {0} parameter matches installed {1}" -f $binding[1], $binding[0])
}
Write-Output ('Production DLL SHA256: ' + (Get-FileHash -LiteralPath $modDll -Algorithm SHA256).Hash)
Write-Output ('Installed game DLL SHA256: ' + (Get-FileHash -LiteralPath $gameDll -Algorithm SHA256).Hash)
Write-Output 'Static and installed-engine contract checks passed. This is not a live battle test.'
