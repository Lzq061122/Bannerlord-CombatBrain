$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$runDir = Join-Path $PSScriptRoot ('bin\verification\' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $runDir -Force | Out-Null
$reportPath = Join-Path $runDir 'result.json'
$report = [ordered]@{
    Status = 'RUNNING'
    GitHead = $null
    GitStatus = @()
    SourceSHA256 = $null
    ProductionDllSHA256 = $null
    OfflineExeSHA256 = $null
    GameDllSHA256 = $null
    Runtime = 'NOT_RUN'
    Stages = @()
}
function Get-SourceHash {
    $files = @(& rg --files CombatBrain OfflineTests -g '*.cs' -g '*.csproj' -g '*.ps1' -g '*.xml' -g '!**/obj/**' -g '!**/bin/**')
    if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate verification inputs' }
    $entries = foreach ($file in ($files | Sort-Object)) {
        $file + ':' + (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash
    }
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $digest = $sha.ComputeHash([Text.Encoding]::UTF8.GetBytes(($entries -join "`n")))
        return [BitConverter]::ToString($digest).Replace('-', '')
    }
    finally { $sha.Dispose() }
}
function Invoke-Stage([string]$Name, [string]$Command, [string[]]$Arguments) {
    $log = Join-Path $runDir ($Name + '.log')
    $stage = [ordered]@{ Name = $Name; Status = 'RUNNING'; ExitCode = $null; Pass = 0; Fail = 0; Log = $log }
    $report.Stages += $stage
    Write-Output ("== {0} ==" -f $Name)
    $previousPreference = $ErrorActionPreference
    try {
        # Native stderr is evidence; let the process finish so its exit code is recorded.
        $ErrorActionPreference = 'Continue'
        & $Command @Arguments 2>&1 | ForEach-Object { $_.ToString() } | Tee-Object -FilePath $log -ErrorAction Stop
        $stage.ExitCode = $LASTEXITCODE
    }
    catch {
        $stage.Status = 'FAIL'
        $stage.ExitCode = 1
        throw
    }
    finally { $ErrorActionPreference = $previousPreference }
    $lines = @(Get-Content -LiteralPath $log)
    $stage.Pass = @($lines | Where-Object { $_ -match '^\s*\[PASS\]' }).Count
    $stage.Fail = @($lines | Where-Object { $_ -match '^\s*\[FAIL\]' }).Count
    $emptyChecks = $Name -in @('Regression', 'StaticContracts') -and $stage.Pass -eq 0
    if ($stage.ExitCode -ne 0 -or $stage.Fail -gt 0 -or $emptyChecks) {
        $stage.Status = 'FAIL'
        throw "$Name failed; see $log"
    }
    $stage.Status = 'PASS'
}
Push-Location $repo
try {
    foreach ($command in @('dotnet', 'ilspycmd', 'rg', 'git')) { Get-Command $command -ErrorAction Stop | Out-Null }
    $report.GitHead = (& git rev-parse HEAD) -join ''
    if ($LASTEXITCODE -ne 0) { throw 'Cannot identify checkout' }
    $report.GitStatus = @(& git status --porcelain)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot read checkout status' }
    $report.SourceSHA256 = Get-SourceHash
    # Rebuild both projects: restoring an older source timestamp must not reuse a stale executable.
    Invoke-Stage 'ProductionBuild' 'dotnet' @('build', 'CombatBrain\CombatBrain.csproj', '-c', 'Release', '--nologo', '-t:Rebuild', '-warnaserror')
    $modDll = Join-Path $repo 'CombatBrain\bin\Win64_Shipping_Client\CombatBrain.dll'
    $report.ProductionDllSHA256 = (Get-FileHash -LiteralPath $modDll -Algorithm SHA256).Hash
    Invoke-Stage 'OfflineBuild' 'dotnet' @('build', 'OfflineTests\OfflineTests.csproj', '-c', 'Debug', '--nologo', '-t:Rebuild', '-warnaserror')
    $report.OfflineExeSHA256 = (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'bin\Debug\net472\OfflineTests.exe') -Algorithm SHA256).Hash
    Invoke-Stage 'Regression' 'dotnet' @('run', '--project', 'OfflineTests\OfflineTests.csproj', '-c', 'Debug', '--no-build')
    Invoke-Stage 'StaticContracts' 'powershell.exe' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $PSScriptRoot 'RouteAStaticChecks.ps1'))
    $gameHashLine = Get-Content -LiteralPath (Join-Path $runDir 'StaticContracts.log') | Where-Object { $_ -match '^Installed game DLL SHA256: ' }
    $report.GameDllSHA256 = ($gameHashLine -replace '^Installed game DLL SHA256: ', '').Trim()
    if ($report.SourceSHA256 -ne (Get-SourceHash)) { throw 'Verification inputs changed during this run; rerun the suite' }
    if ($report.ProductionDllSHA256 -ne (Get-FileHash -LiteralPath $modDll -Algorithm SHA256).Hash) { throw 'Production DLL changed during verification' }
    $report.Status = 'PASS'
}
catch {
    $report.Status = 'FAIL'
    $report.Error = $_.Exception.Message
}
finally {
    $report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $reportPath -Encoding UTF8
    Write-Output ('Verification report: ' + $reportPath)
    Pop-Location
}
if ($report.Status -ne 'PASS') {
    Write-Output ('OFFLINE SUITE FAIL: ' + $report.Error)
    exit 1
}
Write-Output 'OFFLINE SUITE PASS (regression and static contracts; runtime NOT RUN)'
exit 0
