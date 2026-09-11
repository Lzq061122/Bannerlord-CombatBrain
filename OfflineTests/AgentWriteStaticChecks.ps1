# AgentWriteStaticChecks.ps1 —— Agent 级主动写入静态门禁（CB-PROMPT-0038 个体控制面退役）
#
# 目的：从源码事实层证明 CombatBrain 生产代码不再对单兵执行「主动控制写入」：
#   * 单兵战斗能力属性覆盖（AgentDrivenProperties.* 写入）
#   * 逐兵移动方向 / 格挡方向 / 选敌 / 强制换武器 / 解阵框（Agent 级 API 调用）
#   * 上述能力的模型注册链与无消费者残余（等价替代注册 / 死亡开关）
#
# 方法（参照 OfflineTests\RouteAStaticChecks.ps1 的既有写法）：
#   1) 逐文件读取 CombatBrain\**\*.cs（排除 bin/obj），去掉 C# 注释与字符串字面量；
#   2) 对剩余代码做正则扫描，按「禁止写入 → 允许名单」逐条断言；
#   3) 允许名单极窄：逐文件 + 逐符号 + 逐条理由，禁止目录级/通配豁免。
#
# 纪律：本脚本只做静态事实核验，不替代游戏内运行期验证
#   （运行期 A/B 步骤见 docs/agent-control-retirement-ab-checklist.md）。
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

# ---------- 允许名单（极窄：逐文件 + 逐符号 + 逐条理由） ----------
# 两类条目，均须在 docs/agent-control-surface-audit.md §5 与 Integration Owner 裁决清单留痕：
#  (1) 待裁决项：X4 阵框释放（CB-PROMPT-0038 §五.8 六条件：5 项成立、第⑥项需运行期证据）。
#  (2) t3 范围外条目：F6/F7/X5 的逐兵移动写入属 t3（§五.2/§五.5/§五.7）实施范围，
#      本任务（t2）不得越界删除；t3 完成后这些条目必须移除、门禁必须全绿。
$allowList = @{
    'CombatBrain\Ai\CombatAiMissionLogic.cs' = @('Agent.SetFormationFrameDisabled')
    'CombatBrain\Ai\MovementGlue.cs'         = @('Agent.SetMovementDirection')
    'CombatBrain\Ai\SurvivalGlue.cs'         = @('Agent.SetMovementDirection')
    'CombatBrain\Ai\FormationSlotGlue.cs'    = @('Agent.SetMovementDirection')
}

# 允许名单理由（逐条，供 reviewer 核对；改名单必须同步改审计表 §5）：
#   CombatAiMissionLogic.cs / Agent.SetFormationFrameDisabled
#     -- TickFrameRelease：编队 >25 人 + Charge/ChargeToTarget + 距目标 <=25m + L3 的合格成员统一解阵框，
#        同政策、无逐兵战术判断、玩家队豁免。保留或退役由 Integration Owner 按 §五.8 裁决。
#   MovementGlue.cs / SurvivalGlue.cs / FormationSlotGlue.cs / Agent.SetMovementDirection
#     -- F6 逐兵走位 / F7 单兵求生腿 / X5 逐兵补位腿，属 t3 退役范围（CB-PROMPT-0038 §五.2/§五.5/§五.7）；
#        t2 的 inScope 不含这三个文件的退役删除，故在此显式登记为「已知未退役」，不冒充已合规。

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

# ---------- t2 退役项必须零命中（不受允许名单影响） ----------
foreach ($api in @('EnforceShieldUsage', 'SetTargetAgent', 'TryToWieldWeaponInSlot')) {
    $hits = @()
    foreach ($src in $sources) {
        if ($src.Code -match "\.$api\s*\(") { $hits += $src.Path }
    }
    Assert-Contract ($hits.Count -eq 0) "AgentWrite: t2-retired Agent.$api fully removed (hits: $($hits -join '; '))"
}

$drivenHits = @()
foreach ($src in $sources) {
    if ($src.Code -match 'AgentDrivenProperties\.[A-Za-z_]+\s*=[^=]') { $drivenHits += $src.Path }
}
Assert-Contract ($drivenHits.Count -eq 0) "AgentWrite: t2-retired AgentDrivenProperties writes fully removed (hits: $($drivenHits -join '; '))"

# ---------- 退役链完整性（模型注册 / 死开关 / 无消费者残留） ----------
$subModule = $sources | Where-Object { $_.Path.EndsWith('CombatBrain\SubModule.cs') }
Assert-Contract ($subModule -ne $null) 'AgentWrite: SubModule.cs present'
Assert-Contract ($subModule.Code -notmatch 'AddModel\s*\(') 'AgentWrite: SubModule registers no game model (X2 model removed)'
Assert-Contract ($subModule.Raw -match 'CB-PROMPT-0038') 'AgentWrite: SubModule documents the X2 retirement (CB-PROMPT-0038)'

$settings = $sources | Where-Object { $_.Path.EndsWith('CombatBrain\CombatBrainSettings.cs') }
Assert-Contract ($settings -ne $null) 'AgentWrite: CombatBrainSettings.cs present'
Assert-Contract ($settings.Code -notmatch 'HandSkillStrength') 'AgentWrite: HandSkillStrength dead switch removed'

foreach ($retired in @('CombatBrainAgentStatCalculateModel.cs', 'HandSkillProfile.cs', 'AttackSuppression.cs',
                       'AttackSuppressionState.cs', 'CombatBrain\Combat\AttackStats.cs', 'CombatBrain\Combat\PredictiveBlock.cs')) {
    $still = $sources | Where-Object { $_.Path.EndsWith($retired) }
    Assert-Contract ($still.Count -eq 0) "AgentWrite: retired file deleted ($retired)"
}

foreach ($name in @('CombatBrainAgentStatCalculateModel', 'HandSkillProfile', 'AttackSuppression', 'PredictiveBlock')) {
    $hits = @()
    foreach ($src in $sources) {
        if ($src.Code -match "\b$name\b") { $hits += $src.Path }
    }
    Assert-Contract ($hits.Count -eq 0) "AgentWrite: no production reference to retired type $name (hits: $($hits -join '; '))"
}

# ---------- 保留项仍在（防过度退役导致功能回归） ----------
$rangedBrain = $sources | Where-Object { $_.Path.EndsWith('CombatBrain\Ranged\RangedBrain.cs') }
Assert-Contract ($rangedBrain -ne $null) 'AgentWrite: RangedBrain.cs present'
Assert-Contract ($rangedBrain.Code -match 'DecideFallback') 'AgentWrite: F9 formation-level fallback decision retained'
Assert-Contract ($rangedBrain.Code -notmatch 'SelectTarget') 'AgentWrite: F9 per-soldier target scoring removed'

$rangedGlue = $sources | Where-Object { $_.Path.EndsWith('CombatBrain\Ai\RangedGlue.cs') }
Assert-Contract ($rangedGlue -ne $null) 'AgentWrite: RangedGlue.cs present (formation-level snapshot)'
Assert-Contract ($rangedGlue.Code -match 'PlayerTeamExemptionEnabled') 'AgentWrite: F9 formation snapshot keeps player-team exemption'
Assert-Contract ($rangedGlue.Code -notmatch 'IsFallingBack') 'AgentWrite: F9 per-agent falling-back state removed'

$missionLogic = $sources | Where-Object { $_.Path.EndsWith('CombatBrain\Ai\CombatAiMissionLogic.cs') }
Assert-Contract ($missionLogic -ne $null) 'AgentWrite: CombatAiMissionLogic.cs present'
Assert-Contract ($missionLogic.Code -notmatch 'TickBlock') 'AgentWrite: F4 per-soldier block tick removed'
Assert-Contract ($missionLogic.Code -notmatch '_attackStats') 'AgentWrite: F4 per-agent attack statistics dictionary removed'
Assert-Contract ($missionLogic.Code -notmatch '_jitterBlock') 'AgentWrite: F4/F12 per-agent jitter glue removed from mission logic'
Assert-Contract ($missionLogic.Code -match 'FormationCommandExecutor') 'AgentWrite: formation-level command executor retained'

# ---------- 玩家队豁免：编队级写入点不得绕过豁免（t3 收口前的现状核对） ----------
Assert-Contract ($rangedGlue.Code -match 'team\.IsPlayerTeam') 'AgentWrite: F9 formation snapshot skips player team'

Write-Output 'AgentWrite static contract checks passed. Static facts only - runtime A/B acceptance is still pending.'
