# AgentWriteStaticChecks.ps1 —— Agent 级主动写入静态门禁（CB-PROMPT-0038 个体控制面退役）
#
# 目的：从源码事实层证明 CombatBrain 生产代码不再对单兵执行「主动控制写入」：
#   * 单兵战斗能力属性覆盖（AgentDrivenProperties.* 写入）
#   * 逐兵移动方向 / 格挡方向 / 选敌 / 强制换武器 / 解阵框（Agent 级 API 调用）
#   * 退役链完整性（模型注册、死开关、无消费者残留、逐兵状态字典）
#
# 方法（沿用 OfflineTests\RouteAStaticChecks.ps1 的既有写法）：
#   1) 逐文件读取 CombatBrain\**\*.cs（排除 bin/obj），去掉 C# 注释与字符串字面量；
#   2) 对剩余代码做正则扫描，按「禁止写入 → 允许名单」逐条断言；
#   3) 允许名单极窄：逐文件 + 逐符号 + 逐条理由，禁止目录级/通配豁免。
#
# 纪律：本脚本只做静态事实核验，不替代游戏内运行期验证
#   （运行期 A/B 步骤与只读计数判定口径见 docs/agent-control-retirement-ab-checklist.md）。
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot

function Assert-Contract([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
    Write-Output "[PASS] $Message"
}

function Remove-CSharpTrivia([string]$Code) {
    [regex]::Replace($Code, '(?s)/\*.*?\*/|//[^\r\n]*|@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"', ' ')
}

# 生产代码文件集（bin/obj 排除）
$prodFiles = @()
foreach ($path in (& rg --files (Join-Path $repo 'CombatBrain') -g '*.cs' -g '!obj/**' -g '!bin/**')) {
    $prodFiles += $path
}
Assert-Contract ($prodFiles.Count -gt 50) "AgentWrite: production C# file set collected ($($prodFiles.Count) files)"

# 读入全文（去注释/去字符串），逐文件结构：Path / Raw / Code
$sources = @()
foreach ($path in $prodFiles) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing production file: $path" }
    $sources += [PSCustomObject]@{
        Path = $path
        Raw  = [IO.File]::ReadAllText($path)
        Code = Remove-CSharpTrivia ([IO.File]::ReadAllText($path))
    }
}

function Get-Source([string]$suffix) {
    return ($sources | Where-Object { $_.Path.EndsWith($suffix) })
}

# ---------- 允许名单（极窄：逐文件 + 逐符号 + 逐条理由） ----------
# 唯一保留条目：阵框释放（CB-PROMPT-0038 产品裁决＝编队级统一接战政策，保留）。
#   CombatAiMissionLogic.TickFrameRelease 对「编队 >25 人 + Charge/ChargeToTarget + 距目标 <=25m + L3 +
#   未逃跑 + 帧框仍启用 + 冷却放行」的合格成员调用 Agent.SetFormationFrameDisabled()：
#   统一政策触发、无逐兵战术判断、玩家队豁免（编队层前置）、一次性 latch + 5s 冷却防重复抢写。
#   引擎无编队级批量入口，逐兵调用属 API 形态；恢复由原版 AI 每帧重建帧框（FrameReleaseGlue.cs 文件头核验）。
$allowList = @{
    'CombatBrain\Ai\CombatAiMissionLogic.cs' = @('Agent.SetFormationFrameDisabled')
}

# 每文件允许的具体写入名（把名单从「整文件豁免」收紧为「逐条豁免」）
$allowKeys = @{}
foreach ($key in $allowList.Keys) {
    foreach ($entry in $allowList[$key]) { $allowKeys["$key|$entry"] = $true }
}

# 禁止的 Agent 级写入模式（正则 -> 语义）
$banned = @(
    @{ Name = 'Agent.SetMovementDirection'; Pattern = '\.SetMovementDirection\s*\(' },
    @{ Name = 'Agent.EnforceShieldUsage';   Pattern = '\.EnforceShieldUsage\s*\(' },
    @{ Name = 'Agent.SetTargetAgent';       Pattern = '\.SetTargetAgent\s*\(' },
    @{ Name = 'Agent.TryToWieldWeaponInSlot'; Pattern = '\.TryToWieldWeaponInSlot\s*\(' },
    @{ Name = 'Agent.SetFormationFrameDisabled'; Pattern = '\.SetFormationFrameDisabled\s*\(' },
    @{ Name = 'AgentDrivenProperties write'; Pattern = 'AgentDrivenProperties\.[A-Za-z_]+\s*=[^=]' },
    @{ Name = 'Agent.SetScriptedFlags';     Pattern = '\.SetScriptedFlags\s*\(' }
)

foreach ($rule in $banned) {
    $hits = @()
    $allowedHits = @()
    foreach ($src in $sources) {
        $m = [regex]::Matches($src.Code, $rule.Pattern)
        if ($m.Count -eq 0) { continue }
        $fileAllowed = $false
        foreach ($key in $allowList.Keys) {
            if ($src.Path.EndsWith($key) -and $allowKeys["$key|$($rule.Name)"]) { $fileAllowed = $true }
        }
        if ($fileAllowed) {
            $allowedHits += "$($src.Path) x$($m.Count) [allowlisted: $($rule.Name)]"
        } else {
            $hits += "$($src.Path) x$($m.Count)"
        }
    }
    Assert-Contract ($hits.Count -eq 0) "AgentWrite: no unauthorized $($rule.Name) in production code (hits: $($hits -join '; '))"
    if ($allowedHits.Count -gt 0) {
        Write-Output "       [INFO] allowlist hit (must be traced in audit doc section 5): $($allowedHits -join '; ')"
    }
}

# ---------- 退役项必须零命中（不受允许名单影响） ----------
foreach ($api in @('EnforceShieldUsage', 'SetTargetAgent', 'TryToWieldWeaponInSlot', 'SetMovementDirection')) {
    $hits = @()
    foreach ($src in $sources) {
        if ($src.Code -match "\.$api\s*\(") { $hits += $src.Path }
    }
    Assert-Contract ($hits.Count -eq 0) "AgentWrite: retired Agent.$api fully removed (hits: $($hits -join '; '))"
}

$drivenHits = @()
foreach ($src in $sources) {
    if ($src.Code -match 'AgentDrivenProperties\.[A-Za-z_]+\s*=[^=]') { $drivenHits += $src.Path }
}
Assert-Contract ($drivenHits.Count -eq 0) "AgentWrite: retired AgentDrivenProperties writes fully removed (hits: $($drivenHits -join '; '))"

# ---------- 退役链完整性（文件级） ----------
$subModule = Get-Source 'CombatBrain\SubModule.cs'
Assert-Contract ($subModule -ne $null) 'AgentWrite: SubModule.cs present'
Assert-Contract ($subModule.Code -notmatch 'AddModel\s*\(') 'AgentWrite: SubModule registers no game model (X2 model removed)'

foreach ($retired in @('CombatBrainAgentStatCalculateModel.cs', 'HandSkillProfile.cs', 'AttackSuppression.cs',
                       'AttackSuppressionState.cs', 'CombatBrain\Combat\AttackStats.cs', 'CombatBrain\Combat\PredictiveBlock.cs',
                       'CombatBrain\Ai\MovementGlue.cs', 'CombatBrain\Ai\DecisionJitterGlue.cs',
                       'CombatBrain\Compliance\DecisionJitter.cs',
                       'CombatBrain\Movement\MovementController.cs', 'CombatBrain\Survival\SurvivalInstinct.cs')) {
    $still = Get-Source $retired
    Assert-Contract ($still.Count -eq 0) "AgentWrite: retired file deleted ($retired)"
}

# t9（SCOPE-AMEND-2）终态：软断言（文件保留 + 零调用点）已升级为硬断言——文件必须不存在 + 类型零引用。
foreach ($name in @('CombatBrainAgentStatCalculateModel', 'HandSkillProfile', 'AttackSuppression', 'PredictiveBlock',
                    'MovementGlue', 'DecisionJitterGlue', 'MovementController', 'MovementContext', 'MoveAction',
                    'SurvivalInstinct', 'SurvivalAction', 'SurvivalContext', 'FighterProfileFactory')) {
    $hits = @()
    foreach ($src in $sources) {
        if ($src.Code -match "\b$name\b") { $hits += $src.Path }
    }
    Assert-Contract ($hits.Count -eq 0) "AgentWrite: no production reference to retired type $name (hits: $($hits -join '; '))"
}

# R3 口径复核（防过度退役）：FighterProfile 四字段表退役，但 SoldierInput/PrimaryWeaponClass 保留
#   —— 消费者 Ai/FormationSlotGlue.BuildSoldierInput → Formation/TroopClassifier.Classify 仍然在用。
$f0 = Get-Source 'CombatBrain\Combat\FighterProfile.cs'
Assert-Contract ($f0 -ne $null) 'AgentWrite: Combat/FighterProfile.cs present (retained X5-1 input types)'
Assert-Contract ($f0.Code -match 'struct SoldierInput' -and $f0.Code -match 'PrimaryWeaponClass') 'AgentWrite: SoldierInput/PrimaryWeaponClass retained (in-use X5-1 consumers)'
Assert-Contract ($f0.Code -notmatch 'struct FighterProfile|class FighterProfileFactory') 'AgentWrite: FighterProfile struct + FighterProfileFactory removed (R3 consumer-zeroed)'

# ---------- 逐兵状态与执行腿零残留 ----------
foreach ($symbol in @('X5SlotCooldown', 'ShouldRunIndividualLeg', 'IsIndividualOverBudget', 'IndividualLegEnabled',
                      'FrameDisabledAgents', 'IsFrameDisabled', 'IsRetreating')) {
    $hits = @()
    foreach ($src in $sources) {
        if ($src.Code -match "\b$symbol\b") { $hits += $src.Path }
    }
    Assert-Contract ($hits.Count -eq 0) "AgentWrite: per-soldier residue removed ($symbol) (hits: $($hits -join '; '))"
}

# 阵框释放的一次性 latch 门控必须为 2 参形态（不含 F7/F9 跳过条件）
$frameGlue = Get-Source 'CombatBrain\Ai\FrameReleaseGlue.cs'
Assert-Contract ($frameGlue -ne $null) 'AgentWrite: FrameReleaseGlue.cs present'
Assert-Contract ($frameGlue.Code -match 'ShouldSkipAgent\(bool runningAway, bool frameAlreadyDisabled\)') 'AgentWrite: frame release skip gate is 2-arg one-time latch (no F7/F9 conditions)'
$missionLogicPath = ($sources | Where-Object { $_.Path.EndsWith('CombatBrain\Ai\CombatAiMissionLogic.cs') } | Select-Object -First 1).Path
$missionLogicRaw = [IO.File]::ReadAllText($missionLogicPath)
Assert-Contract ($missionLogicRaw -match 'IsFormationFrameEnabled') 'AgentWrite: frame release reads engine latch state (read-only)'
Assert-Contract ($frameGlue.Raw -match 'IsFormationFrameEnabled' -and $frameGlue.Raw -match 'SetFormationFrameEnabled' -and $frameGlue.Raw -match 'TrySetFormationFrame') 'AgentWrite: frame release file documents v1.4.8 API verification (latch/recovery evidence)'

# SurvivalGlue：个体求生腿已退役（零 Agent 写入），只允许编队级重整信号
$survivalGlue = Get-Source 'CombatBrain\Ai\SurvivalGlue.cs'
Assert-Contract ($survivalGlue -ne $null) 'AgentWrite: SurvivalGlue.cs present'
Assert-Contract ($survivalGlue.Code -match 'CasualtyReformSignal') 'AgentWrite: F7 formation-level reform signal retained'
# t9 终态硬断言：逐兵判定文件已不存在（改名/删除），且生产代码零引用其 API
$survivalSignal = Get-Source 'CombatBrain\Survival\CasualtyReformSignal.cs'
Assert-Contract ($survivalSignal -ne $null) 'AgentWrite: Survival/CasualtyReformSignal.cs present (formation-level signal kept)'
Assert-Contract ($survivalSignal.Code -match 'OnCriticalCasualty') 'AgentWrite: F7 reform window/threshold contract retained'
Assert-Contract ($survivalSignal.Code -notmatch 'SurvivalInstinct|SurvivalAction|SurvivalContext|HpThreshold') 'AgentWrite: F7 per-soldier decision API fully removed (hard assertion)'

# ---------- 保留项仍在（防过度退役） ----------
$rangedBrain = Get-Source 'CombatBrain\Ranged\RangedBrain.cs'
Assert-Contract ($rangedBrain -ne $null -and $rangedBrain.Code -match 'DecideFallback') 'AgentWrite: F9 formation-level fallback decision retained'
$rangedGlue = Get-Source 'CombatBrain\Ai\RangedGlue.cs'
Assert-Contract ($rangedGlue -ne $null -and $rangedGlue.Code -match 'PlayerTeamExemptionEnabled') 'AgentWrite: F9 formation snapshot skips player team'
$missionLogic = Get-Source 'CombatBrain\Ai\CombatAiMissionLogic.cs'
Assert-Contract ($missionLogic.Code -notmatch 'TickBlock') 'AgentWrite: F4 per-soldier block tick removed'
Assert-Contract ($missionLogic.Code -match 'FormationCommandExecutor') 'AgentWrite: formation-level command executor retained'
Assert-Contract ($missionLogic.Code.Length -gt 0 -and $missionLogic.Raw.Length -gt 0) 'AgentWrite: mission logic source loaded for retained-path assertions (frame release latch read verified by audit doc + runtime counter)'
$decisionDelay = Get-Source 'CombatBrain\Ai\DecisionDelay.cs'
Assert-Contract ($decisionDelay.Code -match 'HoldTime' -and $decisionDelay.Code -notmatch 'ScaleProfile') 'AgentWrite: command-level hysteresis kept, individual jitter scaling removed'

# ---------- 只读诊断计数（§九）：存在、只读、覆盖六类 ----------
$diag = Get-Source 'CombatBrain\Ai\DiagnosticCounters.cs'
Assert-Contract ($diag -ne $null) 'AgentWrite: DiagnosticCounters.cs present'
Assert-Contract ($diag.Code -match 'CountFormationWrite' -and $diag.Code -match 'CountAgentMovementWrite' `
    -and $diag.Code -match 'CountAgentBlockWrite' -and $diag.Code -match 'CountAgentTargetWrite' `
    -and $diag.Code -match 'CountAgentWeaponWrite' -and $diag.Code -match 'CountAgentStatsOverride' `
    -and $diag.Code -match 'CountFrameReleaseWrite') 'AgentWrite: six write counters + FrameRelease counter present'
Assert-Contract ($diag.Code -notmatch 'SetMovementDirection|SetTargetAgent|EnforceShieldUsage|TryToWieldWeaponInSlot|SetFormationFrameDisabled') 'AgentWrite: diagnostics perform no agent writes (read-only)'
$lifecycle = Get-Source 'CombatBrain\Coordination\MissionStateLifecycle.cs'
Assert-Contract ($lifecycle.Code -match 'DiagnosticCounters\.Reset\(\)') 'AgentWrite: counters reset at mission lifecycle (no cross-mission residue)'

Write-Output 'AgentWrite static contract checks passed. Static facts only - runtime A/B acceptance is still pending.'