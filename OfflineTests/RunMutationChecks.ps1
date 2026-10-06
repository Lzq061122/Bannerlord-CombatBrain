$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$work = Join-Path ([IO.Path]::GetTempPath()) ('CombatBrain-mutations-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
$reportPath = Join-Path $work 'detection-results.json'
$results = @()
$sourceHead = $null
$sourceStatus = @()

function Replace-Once([string]$Root, [string]$Path, [string]$Before, [string]$After) {
    $file = Join-Path $Root $Path
    $text = [IO.File]::ReadAllText($file)
    $at = $text.IndexOf($Before, [StringComparison]::Ordinal)
    if ($at -lt 0 -or $text.IndexOf($Before, $at + $Before.Length, [StringComparison]::Ordinal) -ge 0) {
        throw "Mutation anchor is missing or ambiguous: $Path / $Before"
    }
    [IO.File]::WriteAllText($file, $text.Substring(0, $at) + $After + $text.Substring($at + $Before.Length))
}

function New-Snapshot([string]$Name, [string[]]$Files) {
    $root = Join-Path $work $Name
    New-Item -ItemType Directory -Path $root | Out-Null
    foreach ($file in $Files) {
        $destination = Join-Path $root $file
        New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $repo $file) -Destination $destination
    }
    & git -C $root init --quiet
    if ($LASTEXITCODE -ne 0) { throw 'Cannot initialize mutation snapshot' }
    & git -C $root add --all
    if ($LASTEXITCODE -ne 0) { throw 'Cannot stage mutation snapshot' }
    & git -C $root -c user.name=OfflineVerification -c user.email=offline@local commit --quiet -m 'Verification snapshot'
    if ($LASTEXITCODE -ne 0) { throw 'Cannot commit mutation snapshot' }
    return $root
}

function Run-Snapshot([string]$Name, [string]$Root, [string[]]$ExpectedStages) {
    $log = Join-Path $work ($Name + '.log')
    $previousPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $Root 'OfflineTests\RunOfflineTests.ps1') 2>&1 |
            ForEach-Object { $_.ToString() } | Tee-Object -FilePath $log -ErrorAction Stop
        $exitCode = $LASTEXITCODE
    }
    finally { $ErrorActionPreference = $previousPreference }
    $reports = @(Get-ChildItem -LiteralPath (Join-Path $Root 'OfflineTests\bin\verification') -Recurse -Filter result.json)
    if ($reports.Count -ne 1) { throw "Expected one complete suite report: $Name" }
    $suite = Get-Content -LiteralPath $reports[0].FullName -Raw | ConvertFrom-Json
    $failed = @($suite.Stages | Where-Object { $_.Status -eq 'FAIL' })
    $builds = @($suite.Stages | Where-Object { $_.Name -in @('ProductionBuild', 'OfflineBuild') -and $_.Status -eq 'PASS' })
    if ($ExpectedStages.Count -eq 0) {
        $detected = $exitCode -eq 0 -and $suite.Status -eq 'PASS' -and $builds.Count -eq 2
    }
    else {
        $detected = $exitCode -eq 1 -and $suite.Status -eq 'FAIL' -and $builds.Count -eq 2 -and $failed.Count -eq 1 -and $failed[0].Name -in $ExpectedStages
    }
    $script:results += [ordered]@{ Name = $Name; Passed = $detected; ExitCode = $exitCode; FailedStages = @($failed | ForEach-Object { $_.Name }); Report = $reports[0].FullName; Log = $log }
    if (-not $detected) { throw "Unexpected baseline or detection result: $Name; see $log" }
}

$cases = @(
    @{ Name = 'MissingLiveWiring'; Stages = @('Regression', 'StaticContracts'); Apply = {
        param($root)
        Replace-Once $root 'CombatBrain\Ai\RouteAOrderAdapter.cs' 'Control.ReleaseLiveText = () => LiveOwnershipText();' '// Removed live wiring'
    } },
    @{ Name = 'WrongPrefixParameter'; Stages = @('StaticContracts'); Apply = {
        param($root)
        # Preserve body references so this mutation still compiles.
        Replace-Once $root 'CombatBrain\Coordination\CommandProvenance.cs' 'MovementPrefix(Formation __instance, MovementOrder input,' 'MovementPrefix(Formation __instance, MovementOrder order,'
        $file = Join-Path $root 'CombatBrain\Coordination\CommandProvenance.cs'
        $text = [IO.File]::ReadAllText($file)
        $prefixPattern = [regex]::new('(MovementPrefix\(Formation __instance, MovementOrder order,[\s\S]*?)(?=private static|\z)')
        $text = $prefixPattern.Replace($text, {
            param($match)
            [regex]::Replace($match.Value, '\binput\b', 'order')
        }, 1)
        [IO.File]::WriteAllText($file, $text)
    } },
    @{ Name = 'MissingReservation'; Stages = @('StaticContracts'); Apply = {
        param($root)
        Replace-Once $root 'CombatBrain\Ai\CombatAiMissionLogic.cs' 'ApplyRouteASingle(mission, CbRouteAPoC.Enabled);' '// Removed reservation'
    } },
    @{ Name = 'UnisolatedReleaseDiagnostics'; Stages = @('Regression'); Apply = {
        param($root)
        $file = Join-Path $root 'CombatBrain\Coordination\RouteAControl.cs'
        $text = [IO.File]::ReadAllText($file)
        $pattern = '(if \(Evidence\.Capture && Release(?:Gate|Live)Text != null\)\s*\{)\s*(?://[^\r\n]*\s*)?try\s*\{([\s\S]*?)\}\s*catch \(Exception\) \{ \}'
        if ([regex]::Matches($text, $pattern).Count -ne 2) { throw 'Expected both release diagnostic exception boundaries' }
        [IO.File]::WriteAllText($file, [regex]::Replace($text, $pattern, '$1$2'))
    } },
    @{ Name = 'MissingSelectionReturn'; Stages = @('StaticContracts'); Apply = {
        param($root)
        Replace-Once $root 'CombatBrain\Ai\RouteAOrderAdapter.cs' 'eligibility, valid, now)) return false;' 'eligibility, valid, now)) { }'
    } },
    @{ Name = 'WrongReservationBranch'; Stages = @('StaticContracts'); Apply = {
        param($root)
        Replace-Once $root 'CombatBrain\Ai\CombatAiMissionLogic.cs' 'ApplyRouteASingle(mission, CbRouteAPoC.Enabled);' 'if (!CbRouteAPoC.Enabled) ApplyRouteASingle(mission, CbRouteAPoC.Enabled);'
    } },
    @{ Name = 'CandidateFlagOverwritten'; Stages = @('StaticContracts'); Apply = {
        param($root)
        Replace-Once $root 'CombatBrain\Ai\RouteAOrderAdapter.cs' '{ CurrentOrder = MovementOrder.MovementOrderStop; IsCurrentOrderChanged = false; }' '{ CurrentOrder = MovementOrder.MovementOrderStop; IsCurrentOrderChanged = false; IsCurrentOrderChanged = true; }'
    } },
    @{ Name = 'BrokenMcmObserverSetter'; Stages = @('StaticContracts'); Apply = {
        param($root)
        Replace-Once $root 'CombatBrain\Mcm\CombatBrainMcmSettings.cs' 'set => CombatBrain.Settings.Cps3RuntimeSettings.ObserverEnabled = value;' 'set => CombatBrain.Settings.Cps3RuntimeSettings.ObserverEnabled = false;'
    } },
    @{ Name = 'MissingMcmEOption'; Stages = @('StaticContracts'); Apply = {
        param($root)
        Replace-Once $root 'CombatBrain\Mcm\CombatBrainMcmSettings.cs' ', "E: 单编队 Route A"' ''
    } },
    @{ Name = 'OmittedReleaseTestGroup'; Stages = @('RegressionCompleteness'); Apply = {
        param($root)
        Replace-Once $root 'OfflineTests\RouteATests.cs' 'RunTest(TestReleaseDiagnosticBoundary);' '// Removed required test group'
    } }
)

Push-Location $repo
try {
    $sourceHead = (& git rev-parse HEAD) -join ''
    if ($LASTEXITCODE -ne 0) { throw 'Cannot identify source checkout' }
    $sourceStatus = @(& git status --porcelain)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot read source checkout status' }
    $previousConsoleEncoding = [Console]::OutputEncoding
    try {
        [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
        $files = @(& git -c core.quotepath=false ls-files --cached --others --exclude-standard)
        if ($LASTEXITCODE -ne 0 -or $files.Count -eq 0) { throw 'Cannot enumerate the current workspace snapshot' }
    }
    finally { [Console]::OutputEncoding = $previousConsoleEncoding }
    # Never seed a mutation with an old executable or a previous verification report.
    $files = @($files | Where-Object { $_ -notmatch '(^|/)(bin|obj)/' } | Sort-Object -Unique)
    Run-Snapshot 'Baseline' (New-Snapshot 'Baseline' $files) @()
    foreach ($case in $cases) {
        $root = New-Snapshot $case.Name $files
        & ($case.Apply) $root
        Run-Snapshot $case.Name $root $case.Stages
    }
}
catch {
    $script:failure = $_.Exception.Message
}
finally {
    [ordered]@{ Status = $(if ($script:failure) { 'FAIL' } else { 'PASS' }); SourceGitHead = $sourceHead; SourceGitStatus = $sourceStatus; Runtime = 'NOT_RUN'; Error = $script:failure; Results = $results } |
        ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $reportPath -Encoding UTF8
    Write-Output ('Mutation verification report: ' + $reportPath)
    Pop-Location
}
if ($script:failure) { Write-Output ('MUTATION CHECKS FAIL: ' + $script:failure); exit 1 }
Write-Output 'MUTATION CHECKS PASS (baseline and ten buildable negative samples; runtime NOT RUN)'
exit 0
