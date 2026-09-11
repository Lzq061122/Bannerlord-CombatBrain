# CombatBrain 个体控制面完整审计表（Agent Control Surface Audit）

> status: draft-for-implementation（t1 只读审计产物；§2.1/§2.2/§2.3 已于 t2 回填实施结论）
> authority: DESCRIPTIVE（源码事实审计） + 待裁决结论清单（OPERATIONAL 建议）
>
> 依据任务卡：CB-PROMPT-0038 `CombatBrain-个体控制面退役与产品边界收口-正式派单-2026-09-11.md`
> （series_id=agent-control-retirement；本文件覆盖其 §四 审计范围、§十.1 审计表、§十.9 保留清单）。
> 本文件是**审计事实**，不是规范。任何与 `docs/knowledge/CONSTITUTION.md` 的差异属 implementation/spec mismatch，
> 需按 INDEX §4 Waiver / 架构级 review 处理，不得由本文件宣称规范已改变。

## 0a. 实施进度回填（t2；commit 见 worktree 分支）

| 审计条目 | t1 结论 | t2 实施状态 | 证据 |
|---|---|---|---|
| A1-A8（X2 手活属性模型 + AttackSuppression） | 删除 | **已实施** | `SubModule.cs` 删除 `OnGameStart`/`AddModel`；`CombatBrainAgentStatCalculateModel.cs`/`HandSkillProfile.cs`/`AttackSuppression.cs`/`AttackSuppressionState.cs` 已删除；`CombatBrainSettings.HandSkillStrength` 已删除；`MissionStateLifecycle.ClearStatic` 去掉 attackSuppression 参数与调用；`OfflineTests.csproj` 去掉 link |
| B1-B3（F4 逐兵格挡 + 攻击方向统计） | 删除 | **已实施** | `CombatAiMissionLogic.TickBlock` 整方法删除（含 OnMissionTick 的 `block` 段调用点）、`_blockTimer`/`_jitterBlock`/`_lastBlockJitter`/`_attackStats`/`GetAttackStats`/`MapAttackDir`/`ToUsageDefend` 删除；`OnAgentHit` 只保留 base 调用（不再发布 AttackObserved）；`Combat/AttackStats.cs`、`Combat/PredictiveBlock.cs` 已删除 |
| C1-C4（F9 逐兵选敌/换武器/后撤 + 逐兵态） | 删除 | **已实施** | `RangedGlue.cs` 重写为编队级只读快照（0.3s、玩家队豁免、零 Agent 写入）；`_lastJitterTime`/`_fallingBack`/`IsFallingBack`/`RemoveAgent`/目标数组删除；`RangedBrain` 的 `SelectTarget`/`Score`/`RangedTarget`/`ArmorClass`/`AmmoKind` 删除 |
| C5（F9 编队级远程判断） | 保留（上收） | **已实施** | `RangedBrain.DecideFallback`（是否继续射击/转换任务/整体后撤）保留 + `RangedGlue` 编队级弹药快照与低频日志；离线断言加固（阈值边界 0.15/0.16） |
| D1（X4 阵框释放） | 待裁决 | **未动（超出 t2 inScope）** | `CombatAiMissionLogic.cs` 的 `SetFormationFrameDisabled` 调用保留；门禁以「逐符号允许名单」登记，待 Integration Owner 按 §2.4.1 裁决 |
| E/F/G/H/I（X5/F6/F12/F7 逐兵写入与个体态） | 删除/上收 | **未动（属 t3）** | 门禁以「t3 范围外」允许名单登记三处 `SetMovementDirection`（MovementGlue/SurvivalGlue/FormationSlotGlue），t3 完成后必须移除条目并使门禁全绿 |
| 静态门禁 | 任务卡要求新增 | **已实施** | `OfflineTests/AgentWriteStaticChecks.ps1`（99 个生产文件扫描；允许名单逐文件逐符号 + 逐条理由；t2 退役 API 零容忍断言） |

## 0b. 实施进度回填（t3）与断言映射

### 0b.1 t3 实施状态

| 审计条目 | t1 结论 | t3 实施状态 | 证据 |
|---|---|---|---|
| E1/E2/E5（X5 逐兵补位执行腿 + 冷却 + L3 腿开关） | 删除执行腿 | **已实施** | `Ai/FormationSlotGlue.cs`：删除 `RunIndividualLeg`（唯一 `agent.SetMovementDirection`）、`X5SlotCooldown`、`_slotCooldown`、`IndividualLegEnabled`、`ByIndex` 个体腿查表；`Formation/X5ExecutionPolicy.cs`：删除 `ShouldRunIndividualLeg`/`IsIndividualOverBudget`/`X5SlotCooldown`，仅保留 `ShouldRefreshSlots` |
| E3/E4（编队构成/职责/布局/纪律/观察性 Slot 数据） | 保留 | **已实施** | `FormationSlotGlue.Tick` 保留归类→分流→`SlotLayout.Generate`→`TerrainWarp.Warp`→`FormationDisciplineController.Evaluate`→`SlotAssigner.Assign` 编队级快照（零 Agent 写入） |
| E 连带（`FrameDisabledAgents` 跳过集） | 消费者随执行腿消失 | **已实施删除** | `Formation/SlotAssigner.cs`：删除 `SlotAssignInput.FrameDisabledAgents` 与 `IsFrameDisabled`；解阵框改为一次性 latch（见 D1） |
| F1-F4（F6 逐兵走位 + 逐 Agent 态） | 删除 | **已实施** | 删除 `Ai/MovementGlue.cs`（唯一 `SetMovementDirection`）、`_movement` 字段/`TickMovement`/`movement` bench 段、`_lastJitterTime`/`_lastRetreatTime`/`_phase`/直方图 |
| G1-G5（F12 个体抖动） | 删除个体语义 | **已实施** | 删除 `Ai/DecisionJitterGlue.cs`、`Compliance/DecisionJitter.cs`、`DecisionDelay.ScaleProfile`；`_jitterBlock`/`_lastBlockJitter` 已于 t2 删除；`DecisionDelay.HoldTime`/`SwitchMargin`（指挥层意图防抖）保留 |
| H1-H3（F7 单兵求生腿 + 个体态） | 删除 | **已实施（定义与调用侧均已删除）** | `Ai/SurvivalGlue.cs` 重写为**编队级事件累计**（无 `Agent` 写入）：只读公开血量 → `CasualtyReformSignal`（5s 窗/阈值 8）→`IntentType.Reform`；删除 `ApplyRetreat`/`IsRetreating`/`_retreating`/`_lastJitterTime`/敌情与友军扫描。逐兵判定定义（`SurvivalAction`/`SurvivalContext`/`SurvivalInstinct.Decide`/`HpThreshold`）已随 SCOPE-AMEND-2（t9）删除，文件重命名为 `Survival/CasualtyReformSignal.cs`（见 §0b.4-B） |
| H4（F7 编队级重整信号） | 保留 | **已实施** | `CasualtyReformSignal` 迁至 `Ai/CasualtyReformSignal.cs`（命名空间保持 `CombatBrain.Survival`），消费者为 `SurvivalGlue`；统计口径＝事件累计（非逐拍逐兵比例） |
| I2/I3/I4/I10（逐 Agent 状态字典） | 删除 | **已实施** | `SurvivalGlue` 仅剩 `_wasCritical`（濒死边沿簿记，随 `Remove`/Mission 收尾清理）；F6/F12/X5 全部逐 Agent 字典删除 |
| D1/K1（X4 阵框释放） | 待裁决 | **保留（产品裁决：编队级统一接战政策）** | 见 §2.4.1 与 §0b.3：一次性 latch（`!agent.IsFormationFrameEnabled`）+ 5s 冷却 + 玩家队编队层前置豁免 + 删除 F7/F9 跳过条件 |
| §六 玩家队豁免 | 全控制面 | **已实施** | 逐兵 6 条路径随退役消失（未加任何 `IsPlayerTeam` 单点补丁）；MCM 文案改为「玩家队豁免（全控制面）」+ 明确 HintText |
| §九 只读诊断计数 | 新增 | **已实施** | `Ai/DiagnosticCounters.cs`（六类写入 + FrameRelease 单列）+ `FormationCommandExecutor.ApplySetOrder` 计数① + 5s 汇总日志 + `MissionStateLifecycle` 复位 |

### 0b.2 断言变更映射（t2 基线 1210 → t3 后 1162，−48；逐条）

| # | 被删断言（名称/位置） | 条数 | 对应退役模块 | 归属 |
|---|---|---|---|---|
| 1 | `IdealGap(1,1)=1.3`；`far target -> Advance`（+2 伴随）；`too close -> StepBack`（+1）；`in gap + flank aware -> StrafeLeft`；`in gap + unaware -> Hold`（+2）；`surrounded -> RetreatToAllies`（+1） | 12 | `Movement/MovementController.cs`（`Decide`/`IdealGap`/`MoveAction`）+ `Ai/MovementGlue.cs` | t3 |
| 2 | `low hp + focused + exposed -> RetreatToAllies`；`low hp + safe behind -> Hold`；`healthy -> Fight` | 3 | `Survival/SurvivalInstinct.cs`（`SurvivalInstinct.Decide`） | t3 |
| 3 | `x56 L3 normal/L2/exempted/frame-disabled/budget over -> …` | 5 | `Formation/X5ExecutionPolicy.ShouldRunIndividualLeg` | t3 |
| 4 | `x56 avgAi 4.9/5.1/5.0ms -> …` | 3 | `Formation/X5ExecutionPolicy.IsIndividualOverBudget`（个体腿 Bench 回退闸） | t3 |
| 5 | `x56 cooldown initial/window/boundary/after Remove/per-agent` | 5 | `Formation/X5ExecutionPolicy.X5SlotCooldown` | t3 |
| 6 | `x54 frame-disabled agent skipped`；`x54 frame-disabled agent2 not assigned` | 2 | `Formation/SlotAssigner.FrameDisabledAgents`/`IsFrameDisabled` | t3 |
| 7 | F12 DecisionJitter 块（`perfect/worst/lo-hi/good-bad/clamped/never/always/same seed` 全部子断言） | 15 | `Compliance/DecisionJitter.cs` + `Ai/DecisionJitterGlue.cs` | t3 |
| 8 | `decisiondelay strength 0/1/0.5 -> jitter …` | 3 | `Ai/DecisionDelay.ScaleProfile` | t3 |

**合计 12+3+5+3+5+2+15+3 = 48**，与 1210→1162 的差额**逐条对齐**，无与退役无关的断言静默消失。
替换/保留（非删除）说明：`ShouldSkipAgent` 断言由 3 参改 2 参（同 4 条改为 latch 语义，条数不变）；
`ShouldRefreshSlots` 3 条保留（仅改注释「交原版」）；`x56 stopped -> skip refresh` 措辞更新；`CasualtyReformSignal` 5 条保留。
**t9 追加（SCOPE-AMEND-2）**：`TestFighterProfile` 的 `FighterProfileFactory.Classify`（12 条）+ `.Create` 单调性（3 条）= **15 条**整块删除（`FighterProfile` 四字段表与工厂退役）→ 1162 → **1147**；两条差额合计 −63，逐条可追溯（详见 §0b.4-B 补记）。

### 0b.3 阵框释放（K1/D1）产品裁决与 v1.4.8 API 核验

**产品裁决（2026-09-11，最高 operational 权威）**：**保留**，定性为「**编队级统一接战政策**」；引擎无编队级批量入口，逐兵调用 `SetFormationFrameDisabled()` 属 API 形态，不构成逐兵决策；禁止附带逐兵目标/移动/人格决策；刷新频率与恢复语义必须实证；运行期振荡风险记运行期验证债。

**API 核验证据**（反编译 `TaleWorlds.MountAndBlade.dll` → 类型 `TaleWorlds.MountAndBlade.Agent`，本机 `bin\Win64_Shipping_Client`；行号＝ilspycmd 输出行）：

| API | 签名 | 语义 | CB 侧用法 |
|---|---|---|---|
| `Agent.IsFormationFrameEnabled` | `public bool IsFormationFrameEnabled { get; private set; }`（:1444） | 只读帧框状态 | **一次性 latch**：`!agent.IsFormationFrameEnabled` 才写（`CombatAiMissionLogic.cs:2088`） |
| `Agent.SetFormationFrameDisabled()` | `public void SetFormationFrameDisabled()`（:2305） | 禁用帧框（内部 `if (IsFormationFrameEnabled)` 才翻 false） | 唯一写入点（每 agent 至多一次 + 5s 冷却） |
| `Agent.SetFormationFrameEnabled(...)` | `public void SetFormationFrameEnabled(WorldPosition position, Vec2 direction, Vec2 positionVelocity, float formationDirectionEnforcingFactor)`（:2315） | 重新建立帧框 | **CB 不调用**（恢复交原版） |
| `Agent.TrySetFormationFrame(...)` | `public bool TrySetFormationFrame(in WorldPosition formationPosition, in Vec2 formationDirection)`（:3891） | position 有效→Enable，无效→Disable | CB 不调用（原版路径） |
| `Agent.GetBaseFormationFrame(...)` | `public bool GetBaseFormationFrame(out WorldPosition formationPosition, out Vec2 formationDirection)`（:2051） | 取编队基准帧 | CB 不调用（原版路径） |

**谁在什么时机重新建立阵框（恢复边界证据）**：
`Agent.ForceUpdateCachedAndFormationValues`（:3707）→ AI 士兵分支 `ParallelUpdateCachedAndFormationValuesForAIAgent`（:3737）→
`HumanAIComponent.ParallelUpdateFormationMovement()`（:3743）；非 AI 士兵走 `GetBaseFormationFrame` + `TrySetFormationFrame`（:3725-3726）。
即 **阵框由原版 AI/引擎每帧按编队槽位重建**，CB 的一次性禁用会被原版行为自然覆盖（不会永久 skip），CB 无需 Reset 调用；
`Agent.Tick`（:4745-4760）在位置变化时经 `TeamAI.OnFormationFrameChanged(isFrameEnabled:…)` 上报，满足「有激活/恢复/生命周期边界」。
另：`SlotAssigner` 的逐兵「已解阵跳过集」已删除，编队级绑定表不再消费逐兵解阵状态 → 无永久 skip 路径。

**4 项硬条件对照**：① 编队级触发（Charge/ChargeToTarget + ≤25m + >25 人）成立；② 5s/agent 冷却 + latch（禁止每拍/每 0.5s 重复写）成立；
③ 玩家队零写入（编队层 `FrameReleaseRules.ShouldExempt` 前置，`CombatAiMissionLogic.cs:2151` 同口径）成立；
④ ⑥「动作振荡」离线不可证 → **运行期验证债**，由 §九 A/B + 只读计数（FrameRelease 单列）闭环；若观测到重复抢写/振荡 → 回报 Captain 再议，不静默退役或放宽。

### 0b.4 范围差异与已知未退役残留（SCOPE-AMEND；如实登记）

**A. 已声明的连带路径归属**（t2 提交 6a33df4/7fc07bb 中的变更，不在 t2 最初 inScope）：
`CombatBrain/Ranged/`、`CombatBrain/Combat/` 由 **t7**（SCOPE-AMEND-1）承接；
`CombatBrain/CombatBrainSettings.cs` 由 **t8**（SCOPE-AMEND-2）承接；
4 个 `CombatBrain/Ai/` 注释/观测文件（DecisionObservatory/DecisionJitterGlue/FrameReleaseGlue/MovementGlue）归 **t3**；
ADR-0005 正式化归 **t5**。此为 inScope 契约过窄，非隐蔽越界。

**B. 已知未退役残留 → 已清零（SCOPE-AMEND-2 / t9 attempt 2 回填）**：

| 残留（t3 时） | 终态（t9 后） | 证据 |
|---|---|---|
| `CombatBrain/Movement/MovementController.cs` | **已整文件删除**（F6 逐兵决策内核 `MoveAction`/`MovementContext`/`Decide`/`IdealGap`；消费者 `Ai/MovementGlue.cs` 已随 t3 删除） | 提交 90d2fbf（`-114`）；`rg` 生产代码零引用；`AgentWriteStaticChecks.ps1` 断言「无生产引用 MovementController / 退役文件已删除」PASS |
| `CombatBrain/Survival/SurvivalInstinct.cs` | **逐兵判定已删除、文件已重命名**：`SurvivalAction`/`SurvivalContext`/`SurvivalInstinct.Decide`/`HpThreshold` 全部退役；编队级 `CasualtyReformSignal` 保留在同目录 `CasualtyReformSignal.cs`（消费者 `Ai/SurvivalGlue.cs`），未复活任何逐兵状态 | 提交 90d2fbf（`-59`/`+37`，rename）；`rg` 生产代码零引用 `SurvivalInstinct/SurvivalAction/SurvivalContext`（仅注释提及历史） |
| `CombatBrain/Combat/FighterProfile.cs` | **素质表与工厂已删除**：`FighterProfile` 四字段（`PredictConf`/`FeintRate`/`FlankAware`/`Shakiness`）与 `FighterProfileFactory`（`Create`/`Classify`）退役；**保留** `SoldierInput`/`PrimaryWeaponClass`（仍在用的编队级链路：`Ai/FormationSlotGlue.BuildSoldierInput` → `Formation/TroopClassifier.Classify`） | 提交 90d2fbf（`-66`）；`rg '\bFighterProfile\b'` 生产代码零命中；门禁断言「退役文件已删除 / 无生产引用 FighterProfileFactory」PASS |

**残留清零后的整仓检索**（生产代码 `CombatBrain/**/*.cs`，排除 bin/obj）：`MovementController`、`MovementContext`、`MoveAction`、`SurvivalInstinct`、`SurvivalAction`、`SurvivalContext`、`FighterProfileFactory` 均**零命中**（仅退役说明注释中保留历史名，不含可调用 API）；离线侧同步由提交 b0b1628 完成（`OfflineTests.csproj` link 重定向、`Program.cs` 移除已退役断言块）。

**断言口径补记（t9 后）**：离线 PASS 数 1162 → **1147（−15）**，全部来自 `TestFighterProfile` 的 15 条 `FighterProfileFactory.Classify/Create` 断言整块删除（消费者归零即删，见 §0b.1 I12 / §6 M10）；t3 的 −48 映射（§0b.2）与 t9 的 −15 合计 −63，均逐条可追溯，无与退役无关的断言消失。


**C. 玩家队豁免现状（§六口径）**：开启时 **7 项全控制面零写入** —— 编队命令/编队行为权重（唯一执行器 + 各 Glue 前置豁免）、士兵移动方向、格挡/换防/选敌/换武器、AI 士兵战斗能力属性、阵框释放（编队层 `ShouldExempt` 前置）。
逐兵 6 条路径（原 MovementGlue:223 / SurvivalGlue:197 / RangedGlue:187/197/232 / CombatAiMissionLogic:1605）**随退役消失，未加任何 `IsPlayerTeam` 单点补丁**（符合派单 §二）。豁免关闭时：编队/军队级接管恢复，但已退役的逐兵系统**不复活**（源码中写入点已不存在，且有门禁 ②–⑥ 零容忍断言）。




---

## 0. 审计基线与 STALE_STATE 显式记录

```yaml
audit_worktree: E:\Agent\cb-agent-retirement       # 独立 worktree（本任务第一步建立）
audit_branch: feature/agent-control-retirement
audit_commit: f6634f7                              # worktree 建立时的 HEAD（从 main 分出）
main_checkout: E:\Agent\6a9268112aeaf49072bef052  # 主检出，HEAD 同为 f6634f7，工作区有未提交 docs 文件
current_state_last_verified_commit: a879688237900af4f9706b4a0ca894229ca649c7
```

**STALE_STATE（必须显式记录）**：`docs/knowledge/CURRENT_STATE.md` 的
`last_verified_commit = a879688`，而当前 HEAD = `f6634f7`（`a879688` 之后又前进了 4 个提交：
`a219c5b` / `9a2afe4` / …）。按 CURRENT_STATE §Staleness Notes，**不得把 f6634f7 当作已验证状态**：
本审计的结论只对「f6634f7 的源码事实」负责；`docs/knowledge/CURRENT_STATE.md` 未刷新前，
运行期/部署口径的现状仍以 verifier 的最新验证为准。本审计**不修改** CURRENT_STATE（其维护 owner 为 Captain / Integration Owner）。

加载纪律（本轮实际读取的知识源）：`E:\MBmod\CombatAI\AGENTS.md` → repo `AGENTS.md` → `docs/knowledge/INDEX.md`
→ `CURRENT_STATE.md` → `CONSTITUTION.md` → Route R5 口径（目标子系统源码 + 测试链 `测试指南.md`）；
未读 legacy CONTEXT / 历史 handoff / research archive / `_kb`。

---

## 1. 审计范围与方法

按派单 §四 逐项静态审计所有运行期 **Agent 级写入**入口。方法：

1. 对 `CombatBrain/**/*.cs`（排除 `bin/ obj/`）做去注释/去字符串后的正则扫描，
   候选 API 集：`SetMovementDirection` / `EnforceShieldUsage` / `SetTargetAgent` / `TryToWieldWeaponInSlot` /
   `SetFormationFrameDisabled` / `SetScriptedFlags` / `AgentDrivenProperties.*` / `SetControlledByAI` /
   `AddAiBehavior` / `SetBehaviorWeight` / 其余 `agent.*` 赋值与 `Agent.Set*` 形态；
2. 对每个命中逐个读源码与其调用链（谁在什么节拍调用、LOD 闸门、玩家队判定、状态字典），
   剔除纯注释/文档命中；
3. 结论必须附 `文件:行` 证据；不得凭文件名或命名空间判断。

扫描结果摘要（f6634f7）：

| 类别 | 命中 |
|---|---|
| Agent 主动写入实调用 | 7 处（4 个调用点文件 + 2 处属性赋值 + 逐兵状态字典若干） |
| `SetScriptedFlags` | **0 处实调用**（仅 `MovementGlue.cs:30` 注释提及「属 F6 之后增强项，不在此样板内」） |
| `Agent.SetControlledByAI` | **0 处**（该 API 属 Formation 级；`SetControlledByAI` 仅出现在 `FormationControlLeaseGlue.cs`，且默认关） |
| `Formation.SetOrder` 三域 | 4 处（`FormationCommandExecutor.cs:370/374/377` 3 处 + `RouteAOrderAdapter.cs:35` 1 处）——**编队级**，不在本次个体退役范围 |

---

## 2. 审计表（每行 = 一个 Agent 级写入点）

列含义：**节拍** = 实际调用频率；**对象** = 作用对象集合；**玩家队** = 是否区分玩家队；
**来源** = 决策来源是单兵还是编队；**并行** = 是否与原版 HumanAI 并行控制；**结论** = 删除 / 上收 / 保留 / 仅诊断。

### 2.1 X2 手活属性模型（单兵战斗能力数值）

> **t2 实施结论（CB-PROMPT-0038 范围 A）：A1–A8 全部已实施删除**，见 §0a。
> 验证证据：`OfflineTests/AgentWriteStaticChecks.ps1`（模型注册链零命中 + `AgentDrivenProperties` 写入零命中 + 退役类型零引用）。

| # | 文件:方法:行 | 节拍 | 对象 | 玩家队 | 来源 | 并行 | 结论 |
|---|---|---|---|---|---|---|---|
| A1 | `CombatBrain/SubModule.cs` : `OnGameStart` : **31** | 每次 Game 启动 1 次（AddModel） | 全局 GameModel 链 | 否（注册本身不分队） | 编队无关（模型层） | 是（覆盖 `AgentStatCalculateModel` 链，BaseModel 转发） | **删除**（从注册链移除） |
| A2 | `CombatBrain/Ai/CombatBrainAgentStatCalculateModel.cs` : `UpdateAgentStats` : **68** | 引擎每 agent 属性刷新（≈每拍/每若干拍，无 CB 节流） | `IsHuman && IsAIControlled` 全部 agent（含玩家队 AI 士兵） | **否** | 单兵（per-agent 技能/难度） | **是**（写引擎属性，原版 HumanAI 立即消费） | **删除** |
| A3 | 同上 : **69** | 同上 | 同上 | 否 | 单兵 | 是 | **删除** |
| A4 | 同上 : **75** | 同上 | 同上（有弓技能者） | 否 | 单兵 | 是 | **删除** |
| A5 | 同上 : **79-80** | 同上 | 同上 | 否 | 单兵 | 是 | **删除** |
| A6 | 同上 : **85-86** | 同上 | 同上（非抑制索引） | 否 | 单兵 | 是 | **删除** |
| A7 | `CombatBrain/Ai/CombatBrainAgentStatCalculateModel.cs` : `MarkAttackSuppressed` / `UnmarkAttackSuppressed` / `ClearAllSuppressed` : **22 / 27 / 34**（静态抑制集 `_suppressedAttack` : 20） | 由 `AttackSuppression` 调用 | agent.Index 集合 | 否 | 单兵 | 否（仅耦合标记） | **删除**（解除与 AttackSuppression 的双向耦合） |
| A8 | `CombatBrain/Ai/AttackSuppression.cs` : `Suppress` : **33**（写 `AIAttackOnDecideChance = 0f`）、`Restore` : **47** | **运行期零调用**（见 §2.1.1） | 被抑制 agent | 否 | 单兵 | 是（会覆盖引擎攻击决定概率） | **删除**（连同 `AttackSuppressionState.cs`） |

**2.1.1 关键事实：`AttackSuppression` 运行期是死状态容器。**
`CombatAiMissionLogic.cs:220` 实例化 `_attackSuppression`，但全仓检索不到任何 `Suppress(` / `Restore(` 调用点；
唯一外部使用是 `CombatAiMissionLogic.cs:1702 → Coordination/MissionStateLifecycle.cs:18` 的 `ClearAll()`（跨 Mission 清理）。
即：**运行期对单兵攻击概率的置零/恢复路径当前未接线**，但它仍与 X2 模型通过静态 `_suppressedAttack` 双向耦合
（`AttackSuppression.cs:36/48/62/70`）。派单 §五.1「解除与 AttackSuppression 的耦合」在本审计下的准确含义是：
删除 X2 模型后，`AttackSuppression` 与其状态表**没有任何消费者**，应一并退役（连同 `OfflineTests/Program.cs:908` 的
`TestAttackSuppression` 与 `:81` 调用、`MissionStateLifecycle.cs:12/18` 的入参与清理、`CombatAiMissionLogic.cs:220/1666/1702` 的字段与调用）。

**2.1.2 死开关事实（派单 §五.1「不得留下看似可用的死开关」）**：
`CombatBrainSettings.HandSkillStrength`（`CombatBrainSettings.cs:25`）默认 `1f`，注释写「MCM 装载后可在通用组热切」，
但 `CombatBrainMcmSettings.cs`（全文 163 行）**没有任何对应属性**——该「设置项」实际无法从 MCM 调整。
同类检查：`DecisionDelayStrength`（`CombatBrainSettings.cs:19`）**有** MCM 属性（`DecisiveMode`，`CombatBrainMcmSettings.cs:31-37`），
是活开关。

### 2.2 F4 预测式格挡（逐兵格挡方向写入）

> **t2 实施结论（CB-PROMPT-0038 范围 B）：B1–B3 全部已实施删除**，见 §0a。
> 验证证据：`OfflineTests/AgentWriteStaticChecks.ps1`（`Agent.EnforceShieldUsage` 零命中 + `TickBlock`/`_attackStats` 零残留）。

| # | 文件:方法:行 | 节拍 | 对象 | 玩家队 | 来源 | 并行 | 结论 |
|---|---|---|---|---|---|---|---|
| B1 | `CombatBrain/Ai/CombatAiMissionLogic.cs` : `TickBlock` : **1605**（`defender.EnforceShieldUsage(ToUsageDefend(predicted))`） | 0.1s 节拍（`_blockTimer = 0.1f` : 1538） | 全部 **L3** agent（`_agentLod == LodLevel.L3` : 1546），**不区分队**；再经 F12 抖动闸 | **否**（无任何 `IsPlayerTeam` / 豁免判定） | 单兵（per-defender 预测 + per-agent 攻击方向统计） | **是**（写引擎盾牌用法，原版盾牌/格挡逻辑同帧仍运行） | **删除**（含 F12 闸门调用点） |
| B2 | `CombatBrain/Ai/CombatAiMissionLogic.cs` : `OnAgentHit` : **1634**（`GetAttackStats(affectorAgent.Index).Record(dir)`）+ `GetAttackStats` : **2538-2546**（`_attackStats` 字典 : 112） | 每次命中回调（事件驱动） | 全部 agent（含玩家队 AI 士兵） | 否 | 单兵（攻击方向历史） | 否（只记录） | **删除**（消费者仅 B1；§五.3「无其他编队级消费者则一并退役」成立） |
| B3 | `CombatBrain/Combat/AttackStats.cs`（整类，56 行）、`CombatBrain/Combat/PredictiveBlock.cs`（整类，36 行） | — | — | — | — | 否 | **删除**（随 B1/B2 无消费者） |

### 2.3 F9 远程（逐兵选敌 / 强制换武器 / 后撤）

> **t2 实施结论（CB-PROMPT-0038 范围 C）：C1–C4 已实施删除；C5 已实施上收为编队级只读判断**，见 §0a。
> 验证证据：`OfflineTests/AgentWriteStaticChecks.ps1`（`SetTargetAgent`/`TryToWieldWeaponInSlot` 零命中、`IsFallingBack` 零残留、`DecideFallback` 保留）
> + 离线断言 `TestRanged`（编队级取舍阈值边界）。

| # | 文件:方法:行 | 节拍 | 对象 | 玩家队 | 来源 | 并行 | 结论 |
|---|---|---|---|---|---|---|---|
| C1 | `CombatBrain/Ai/RangedGlue.cs` : `ApplyRanged` : **187**（`agent.SetTargetAgent(target)`） | 0.3s 节拍 × `StepFor(L2/L3)` 跳帧，`KeepShooting` 分支 | `IsHuman && IsAIControlled` 且有远程槽的 L2+ agent，**不区分队** | **否** | 单兵（per-agent 扫描 45m + `RangedBrain.SelectTarget` 打分） | **是**（覆盖原版射手选敌） | **删除**（保留编队级远程判断） |
| C2 | 同上 : **197**（`TryToWieldWeaponInSlot(melee, WithAnimation, false)`） | 同上，`SwitchToMelee` 分支（弹药 ≤15%） | 同上 | 否 | 单兵 | 是（强制换武器打断原版动作） | **删除** |
| C3 | 同上 : `Retreat` : **232**（`SetMovementDirection(dir)`） | 同上，`Retreat` 分支 / 无近战武器回退 | 同上 | 否 | 单兵（远离最近威胁 / 编队中心） | 是 | **删除** |
| C4 | `CombatBrain/Ai/RangedGlue.cs` : `IsFallingBack` : **56-60**（`_fallingBack` 逐 agent 态）+ `_lastJitterTime` : 50 | 读写于 C1/C3 与 `MovementGlue` 跳过闸 | 同上 | 否 | 单兵 | 否（跨胶水耦合） | **删除**（随 C1-C3 无依据） |
| C5 | `CombatBrain/Ai/RangedGlue.cs` : 编队级判断部分（`RangedBrain.DecideFallback` : 162，是否继续射击/整体后撤/换任务） | 0.3s | 编队可消费的聚合判断 | 否 | 编队（弹药余量 + 近战距离） | 否 | **保留（上收）**：以编队级信号表达，不再驱动单兵 API；`CombatBrain/Ranged/RangedBrain.cs` 纯逻辑保留 |

### 2.4 X4 阵框释放（逐兵 `SetFormationFrameDisabled`）

| # | 文件:方法:行 | 节拍 | 对象 | 玩家队 | 来源 | 并行 | 结论 |
|---|---|---|---|---|---|---|---|
| D1 | `CombatBrain/Ai/CombatAiMissionLogic.cs` : `TickFrameRelease` : **2188**（`agent.SetFormationFrameDisabled()`） | 0.5s 编队级节拍（: 2146）+ 逐 agent 5s 冷却（`FrameReleaseCooldown.CooldownSeconds` = 5f，`FrameReleaseGlue.cs:68`） | 编队 >25 人（`FrameReleaseRules.MinReleaseCount` : 28）、L3、非生存/后撤/逃跑的 agent | **是**（: 2151 `FrameReleaseRules.ShouldExempt`） | **编队级触发 + 同政策**（见 §2.4.1 六项核验） | 是（解除阵框后交原版 AI 自由接敌——设计意图即如此） | **保留或退役待裁决**：六项条件中 5 项成立、1 项（「不造成周期性重复抢写或动作振荡」）**需运行期证据**；本审计给建议 = 保留（附运行期验证债） |

**2.4.1 派单 §五.8 六项条件逐条核验（证据）**

| 条件 | 判定 | 证据 |
|---|---|---|
| ① 由统一编队接战状态触发 | **成立** | : 2158-2159 `GetReadonlyMovementOrderReference().OrderEnum` ∈ {Charge=2, ChargeToTarget=3}（`FrameReleaseGlue.cs:29-30`）+ : 2168 `ShouldReleaseFormation(true, dist, count)` |
| ② 对符合条件成员执行相同政策 | **成立（但非全编队齐一）** | 同一编队的合格成员执行同一动作；但 L3 闸门（: 2177）、F7/F9/逃跑跳过（: 2180-2183）、5s 冷却（: 2186）使**成员间存在差异化的「未写入者」**——差异来自节流而非逐兵战术判断 |
| ③ 不为每名士兵运行独立战术判断 | **成立** | 判定全部来自编队级量（MovementOrder、编队中心、`TryGetFormationTarget`）；逐兵分支只是过滤（LOD/状态/冷却），不产生 per-agent 战术决策 |
| ④ 有明确激活/恢复/生命周期边界 | **激活成立、恢复不明确** | 激活：Charge 命令 + 距离 + 规模三闸；生命期：`_frameReleaseCooldown` 随 `OnAgentRemoved` 清理（: 1667）。**但「何时恢复阵框」全仓无 `SetFormationFrameEnabled` 调用**——恢复依赖引擎在编队离开 Charge/重整队列时自行处理，CB 侧无显式恢复边界（对 `SlotAssigner.cs:98/116/147` 的 `FrameDisabledAgents` 跳过集有持续影响） |
| ⑤ 不作用于已豁免的玩家队 | **成立** | : 2151 |
| ⑥ 不造成周期性重复抢写/动作振荡 | **未证明（运行期债）** | 冷却窗 5s 限频可静态说明「不重复抢写」，但「动作振荡」是运行期观感/帧级现象，离线无法证；需 §九 A/B 采样配合只读计数 |

> §五.8 规定「若无法证明这些条件，则一并退役」。**本审计不代替产品裁决**：把 ⑥ 的未证明项与 ④ 的恢复边界一并
> 列入 §6 待裁决清单，建议 t3 在实现前先由 Captain 裁决「保留（附运行期债）或退役」。

### 2.5 X5 逐兵补位腿（Slot 目标驱动）

| # | 文件:方法:行 | 节拍 | 对象 | 玩家队 | 来源 | 并行 | 结论 |
|---|---|---|---|---|---|---|---|
| E1 | `CombatBrain/Ai/FormationSlotGlue.cs` : `RunIndividualLeg` : **183**（`agent.SetMovementDirection(dir)` 朝 slot） | 0.5s 编队级节拍 + `X5SlotCooldown` 2s 冷却（`X5ExecutionPolicy.cs:48`） | 仅 L3、未解阵、未超预算、有指派 slot 的 AI 人类 agent | **是**（`FormationSlotGlue.cs:81` 玩家队整队 skip；`X5ExecutionPolicy.ShouldRunIndividualLeg` : 27-34 亦留 exempted 形参） | 编队级布局产物（SlotLayout/TerrainWarp/Discipline）+ 逐兵就近匹配 | 是 | **删除执行腿**（保留布局/分类/纪律等编队级产物） |
| E2 | `FormationSlotGlue.cs` : `_slotCooldown` : 41 / `X5SlotCooldown`（`X5ExecutionPolicy.cs:46-69`） | 2s/agent | 同上 | 是（同 E1 路径） | 逐 agent 冷却 | 否 | **删除**（随 E1 无消费者） |
| E3 | `FormationSlotGlue.cs` : 编队级链 `BuildAgentSnap` : 193 / `SlotLayout.Generate` : 108 / `TerrainWarp.Warp` : 109 / `FormationDisciplineController.Evaluate` : 113 / `SlotAssigner.Assign` : 131 | 0.5s | 编队 | 是（: 81） | **编队级** | 否 | **保留**（编队构成分类/职责/Arrangement/纪律/观察性 Slot 数据） |
| E4 | `FormationSlotGlue.cs` : `_agentSnap` : 49（`FormationAgents` : 53-59，含 `FrameDisabled`/`Entries`/`ByIndex`） | 0.5s 复用缓冲 | 编队 | 是 | 编队级快照 | 否 | **保留（观察性）**：`FrameDisabled` 同时被 `SlotAssigner` 消费（见 D1 ④） |
| E5 | `CombatBrain/Formation/X5ExecutionPolicy.cs` : `ShouldRunIndividualLeg` : 27 / `IsIndividualOverBudget` : 18 / `ShouldRefreshSlots` : 37 | 0.5s | 门控 | 是 | 编队级门控 | 否 | `ShouldRunIndividualLeg`=**删除**（随 E1）；`IsIndividualOverBudget`/`ShouldRefreshSlots`=**保留**或**上收**（纪律门控仍被编队级链消费） |

### 2.6 F6 逐兵走位

| # | 文件:方法:行 | 节拍 | 对象 | 玩家队 | 来源 | 并行 | 结论 |
|---|---|---|---|---|---|---|---|
| F1 | `CombatBrain/Ai/MovementGlue.cs` : `ApplyDecision` : **223**（`agent.SetMovementDirection(dir)`） | 0.2s 节拍（`TickInterval` : 34）× `StepFor(L2/L3)` 跳帧；仅 L2+（: 101）、人类非骑乘（: 95） | 全部 L2+ AI 人类步兵 agent，**不区分队** | **否** | 单兵（`MovementController.Decide` per-agent + 25m 敌情扫描 + 绕侧意识） | **是**（注释 `MovementGlue.cs:28-30` 自认「原版 HumanAIComponent 每帧自行驱动移动，本层按 0.2s 叠加决策输入」） | **删除** |
| F2 | `MovementGlue.cs` : 逐兵决策族 Advance/StepBack/Strafe/RetreatToAllies/Hold —— `MovementController.Decide` : 174 + 后处理 : 187-204（Retreat 2s 冷却 `_lastRetreatTime` : 62、Strafe 1/4 占空比 `StrafePeriod` : 43） | 0.2s | 同上 | 否 | 单兵 | 是 | **删除**（编队去哪改由编队级 MovementOrder / Formation Mission / Route A 表达） |
| F3 | `MovementGlue.cs` : `FindRetreatPoint` : 289-325（逐兵找最近友军/编队中心/退离敌人） | 被围分支 | 同上 | 否 | 单兵 | 否（仅产出目标点） | **删除**（随 F1） |
| F4 | `MovementGlue.cs` : `_lastJitterTime` : 59、`_lastRetreatTime` : 62、`_phase` : 48、直方图计数 : 65-71 | 0.2s | 同上 | 否 | 单兵 | 否 | **删除**（随 F1；直方图日志 `Report()` : 363 一并退役，见 §十.9 保留清单外的观测项） |

### 2.7 F12 逐兵抖动 / 犹豫 / 延迟 / 走形

| # | 文件:方法:行 | 节拍 | 对象 | 玩家队 | 来源 | 并行 | 结论 |
|---|---|---|---|---|---|---|---|
| G1 | `CombatBrain/Ai/DecisionJitterGlue.cs` : `ShouldAct` : 58-86（Hesitate 跳过本轮、Delay 维持期、Wobble 幅度） + `ApplyWobble` : 89 | 挂在 4 个个体决策点：走位 0.2s（`MovementGlue.cs:216`）、求生 0.3s（`SurvivalGlue.cs:192`）、远程 0.3s（`RangedGlue.cs:173`）、格挡 0.1s（`CombatAiMissionLogic.cs:1600`） | 上述各路径的 agent（含玩家队） | **否** | 单兵（per-agent `Random` + 纪律/疲劳/士气） | 是（跳过写入 = 让原版占主导；走形 = 扰动写入方向） | **删除**（个体闸门与走形） |
| G2 | `DecisionJitterGlue.cs` : `_states` : 42（`AgentState` : 30-40：`Rng` : 32 种子 = `agentIndex * 7919 + 12345` : 107、`DelayRemaining` : 33、`ActiveTime` : 34，疲劳上限 180s : 27） | 随 G1 调用 | 同上 | 否 | 单兵 | 否 | **删除**（每 Agent 状态字典；`Remove` : 97 生命周期钩子一并退役） |
| G3 | `CombatBrain/Compliance/DecisionJitter.cs` : `ComputeProfile` : 25（HesitateProb/DelayBase/Wobble 公式） + `Apply` : 39（**每次调用抽一次随机**） | 随 G1 | 同上 | 否 | 单兵 | 否 | **删除个体语义**；若上层迟滞需要保留概率模型，按派单 §五.4 上收至指挥层 |
| G4 | `CombatBrain/Ai/DecisionDelay.cs` : `ScaleProfile` : 33（缩放 HesitateProb/DelayBase/Wobble） | MCM 热切 | 同上 | 否 | 单兵 | 否 | **删除个体缩放**；`HoldTime` : 21 / `SwitchMargin` : 27（意图层 hold/余量）**保留**（属指挥层迟滞） |
| G5 | `CombatBrain/Ai/CombatAiMissionLogic.cs` : `_jitterBlock` : 201 + `_lastBlockJitter` : 204 | 0.1s | L3 defender | 否 | 单兵 | 否（仅闸门） | **删除**（随 B1） |

**结论口径**：派单 §五.4 要求「保留的迟滞必须明确上收至指挥层意图切换 / 编队任务切换 / 编队命令防抖」。
本审计的落点是：`DecisionDelay.HoldTime/SwitchMargin`（指挥层意图防抖）+ `IntentEngine` 的 hold 机制 +
`FormationCommandArbiter`/`Executor` 的提案-裁决（编队命令防抖）；个体三件套（Hesitate/Delay/Wobble）全部退役。

### 2.8 F7 求生（逐兵撤退腿 + 编队级重整信号）

| # | 文件:方法:行 | 节拍 | 对象 | 玩家队 | 来源 | 并行 | 结论 |
|---|---|---|---|---|---|---|---|
| H1 | `CombatBrain/Ai/SurvivalGlue.cs` : `ApplyRetreat` : **197**（`SetMovementDirection` 退向最近友军/编队中心） | 0.3s 节拍（`TickInterval` : 30）× 跳帧；仅 L2+、濒死（HP<30% : 32）、被集火、背后无安全区 | 全部 L2+ AI 人类步兵 agent，**不区分队** | **否** | **单兵**（per-agent HP + 敌情扫描 + F12 闸门） | **是** | **删除** |
| H2 | `SurvivalGlue.cs` : `_retreating` : 53 / `IsRetreating` : 60-64（供 `MovementGlue.cs:103-107` 与 `RangedGlue.cs:96` 跳过） | 0.3s | 同上 | 否 | 单兵 | 否（跨胶水耦合） | **删除**（随 H1；调用点 `CombatAiMissionLogic.cs:1447/1461`、`FrameReleaseGlue.cs:51` 入参需同步清理） |
| H3 | `SurvivalGlue.cs` : `_wasCritical` : 52 / `_lastJitterTime` : 51 / `_jitter` : 48 | 0.3s | 同上 | 否 | 单兵 | 否 | **删除**（随 H1/G1） |
| H4 | `SurvivalGlue.cs` : `TrackCritical` : 267-308 + `_reformByTeam` : 56 / `_reformArmed` : 57 → `publish(IntentSwitched, IntentType.Reform)` : 292-299 | 0.3s，**入队为 O(1)**，`CasualtyReformSignal.OnCriticalCasualty(now)` 5s 窗阈值 8 | 队级 | 否（走队级信号，不改玩家队行为；玩家队豁免由 `TickIntent` : 678 处理） | **编队/队级**（伤亡事件累计 + 窗口） | 否 | **保留（编队级信号）** |
| H5 | `CombatBrain/Survival/SurvivalInstinct.cs` : `Decide` : 139（个体判定） | 0.3s | 单兵 | 否 | 单兵 | 否 | **删除个体判定**；`CasualtyReformSignal` 部分（H4 依赖）**保留** |

**逐兵统计口径核验**：H4 的队级重整信号当前**已经是事件累计式**（`_wasCritical` 记录「新跌破濒死线」跃迁，
再入队 5s 窗口计数），不是「每拍遍历全队算比例」。派单 §五.5「若现有重伤比例必须逐兵统计，应改为事件累计…」
在当前代码下**已满足**；`_wasCritical` 本身是每 Agent 状态（H3 范畴），退役 H1 后其唯一消费者即 H4，
需在 t3 中把 H4 改为不依赖逐兵 `_wasCritical` 的等价口径（例如按 `OnAgentHit`/血量事件或低频编队快照），
否则会出现「保留队级信号但保留逐兵状态」的残留。

### 2.9 每 Agent 状态字典 / 冷却 / 随机数 / 人格 / 纪律 / 疲劳 总表（派单 §四 第 8 项）

| # | 状态对象 | 文件:行 | 键 | 节拍/用途 | 玩家队 | 结论 |
|---|---|---|---|---|---|---|
| I1 | `DecisionJitterGlue._states`（Rng/DelayRemaining/ActiveTime） | `DecisionJitterGlue.cs:42`（30-40/107） | agent.Index | 个体决策闸门 | 否 | 删除 |
| I2 | `MovementGlue._lastJitterTime` / `_lastRetreatTime` | `MovementGlue.cs:59 / 62` | agent.Index | F6 走位节流/后撤冷却 | 否 | 删除 |
| I3 | `SurvivalGlue._lastJitterTime / _wasCritical / _retreating` | `SurvivalGlue.cs:51 / 52 / 53` | agent.Index | F7 求生 | 否 | `_lastJitterTime`/`_retreating` 删除；`_wasCritical` 需在保留 H4 时重定义（见 §2.8） |
| I4 | `RangedGlue._lastJitterTime / _fallingBack` | `RangedGlue.cs:50 / 51` | agent.Index | F9 远程 | 否 | 删除 |
| I5 | `CombatAiMissionLogic._attackStats`（`AttackStats` 滑窗 8 次方向） | `CombatAiMissionLogic.cs:112`（/ 2538；清除 1665） | agent.Index | F4 预测格挡输入 | 否 | 删除 |
| I6 | `CombatAiMissionLogic._lastBlockJitter` | `CombatAiMissionLogic.cs:204`（清除 1664） | agent.Index | F4+F12 闸门 | 否 | 删除 |
| I7 | `CombatBrainAgentStatCalculateModel._suppressedAttack`（**static**） | `CombatBrainAgentStatCalculateModel.cs:20` | agent.Index | X2×AttackSuppression 耦合 | 否 | 删除（跨 Mission 静态残留风险点，见 §2.1.1） |
| I8 | `AttackSuppressionState._saved` | `AttackSuppressionState.cs:10` | agent.Index | 攻击概率存原值 | 否 | 删除（无消费者） |
| I9 | `FrameReleaseCooldown._lastRelease` | `FrameReleaseGlue.cs:70`（清除 `CombatAiMissionLogic.cs:1667`） | agent.Index | X4 冷却 | 是（豁免在编队层） | 随 D1 裁决 |
| I10 | `X5SlotCooldown._last` | `X5ExecutionPolicy.cs:50`（清除 `FormationSlotGlue.cs:143`） | agent.Index | X5 补位冷却 | 是 | 删除（随 E1/E2） |
| I11 | `CombatAiMissionLogic._agentLod` | `CombatAiMissionLogic.cs:110` | agent.Index | **LOD 闸门（不是控制写入）** | 否 | **保留**（派单 §八.10 要求「Mission 结束后无旧的每 Agent 状态字典」——需核对该字典在 `OnAgentRemoved`（: 1658）与 Mission 结束（`ClearMissionStaticState` : 1700-1703）后被清空） |
| I12 | `CombatAiMissionLogic._profiles`（`FighterProfile`：含 `FeintRate`/`PredictConf`/`FlankAware` 等兵种素质） | `CombatAiMissionLogic.cs:111`（/ 2476；清除 1659） | agent.Index | F12 纪律输入 + F4 预测输入 | 否 | 退役 A1-G5 后 `FeintRate`/`PredictConf` 消费者变为 0；`FlankAware` 仍在 `MovementGlue`（F1）——随 F1 退役后**全部消费者归零**，需裁决「整表退役」或「保留为观察性 profile」。**本审计建议：随 F12/F4/F6 退役一并删除或降级为只读观测，避免保留无消费者状态**（派单 §五.3 同口径） |
| I13 | `FormationSlotGlue._agentSnap.ByIndex` | `FormationSlotGlue.cs:58` | agent.Index | X5 快照（个体腿查表） | 是 | `ByIndex` 随 E1 删除；其余字段（Comp/FrameDisabled/Entries）保留 |

### 2.10 其他「可能改变单兵动作/动画/攻击/防御/移动」的入口（穷尽性核对）

| # | 入口 | 实调用 | 结论 |
|---|---|---|---|
| J1 | `Agent.SetScriptedFlags` | **0 处**（仅 `MovementGlue.cs:30` 注释） | 仅诊断记录：**无待退役实体** |
| J2 | `Agent.SetFormationFrameEnabled`（恢复阵框） | **0 处** | 与 D1 ④ 的恢复边界缺口相关（见 §2.4.1） |
| J3 | `Agent.SetControlledByAI` | **0 处**（属 Formation 级，见 J5） | 无 |
| J4 | `AgentDrivenProperties` 其他字段写入 | 仅 `AttackSuppression.cs:33/47`（AIAttackOnDecideChance）与 X2 模型 68/69/75/79/85 | 见 §2.1 |
| J5 | `Formation.SetControlledByAI(false/true)` | `FormationControlLeaseGlue.cs:375 / 426 / 455` | **编队级**；默认关（`CombatBrainSettings.FormationControlLeasePocEnabled = false` : 46）；不在个体退役范围，但**会间接冻结整编队 AI**，属 §十.9 保留清单的透明说明项 |
| J6 | `Formation.SetOrder` 三域 | `FormationCommandExecutor.cs:370/374/377`（唯一执行器）+ `RouteAOrderAdapter.cs:35`（Route A 单编队） | **编队级**，保留（CPS 唯一出口语义不变） |
| J7 | `FormationAI.AddAiBehavior` / `SetBehaviorWeight` / `GetAiWeight` | `RouteAOrderAdapter.cs:175`（装载 4 个 Behavior）、`FormationCommandPolicy.cs`（策略/权重模型） | **编队级行为权重**，保留；注意这是「编队去哪里」的合法表达通道（派单 §五.2/§五.6 明确指向它） |
| J8 | `Agent.TryToWieldWeaponInSlot` 之外的换武器/装备写入 | 全仓仅 `RangedGlue.cs:197` | 无其他 |
| J9 | 人格/纪律/疲劳状态 | `DecisionJitterGlue`（疲劳 `ActiveTime`）、`FighterProfile`（纪律/素质，只读派生）、`CombatBrain/Compliance/*` | 个体部分（G1-G5）删除；`FighterProfile` 派生链见 I12 |
| J10 | 动画/动作直接写入（`SetAction`/`SetAnimation`/`SetLastAttack*` 等） | **0 处** | 无 |
| J11 | `Agent.EnforceShieldUsage` 的「盾向」等价入口 | 全仓仅 `CombatAiMissionLogic.cs:1605` | 见 B1 |

---

## 3. 根因结论：为什么玩家队 AI 士兵仍会抽搐

1. **豁免面只覆盖编队级**：`PlayerTeamExemptionEnabled` 的 13 个判定点（见 §4）全部位于**编队/队级**路径；
2. **个体三个胶水完全没有玩家队判定**：`MovementGlue`（F1）、`SurvivalGlue`（H1）、`RangedGlue`（C1-C3）逐兵遍历时
   **只按 `IsHuman/IsAIControlled/LOD/跳帧` 过滤**（`MovementGlue.cs:94-110`、`SurvivalGlue.cs:91-101`、`RangedGlue.cs:87-99`），
   而 `TickLod`（`CombatAiMissionLogic.cs:607-619`）对 `mission.Agents` **全量**评估 LOD，玩家队 AI 士兵同样拿到 L2/L3；
3. **四个个体写入点对玩家队生效**：F1 走位方向、H1 求生撤退方向、C1 选敌、C2 换武器、C3 后撤方向、
   B1 格挡方向（`TickBlock` 的 `l3` 列表同样无队过滤）——合计 **6 个写入路径把玩家队 AI 士兵当普通 AI 士兵写**；
4. **与原版 HumanAI 并行**：每个写入点的注释/语义均承认原版同帧仍在驱动（`MovementGlue.cs:28`、
   `RangedGlue.cs:24-25`、`CombatBrainAgentStatCalculateModel.cs:9-10`），构成双重控制；
5. **CPS 观测不到**：`CommandProvenance` 只观测 Formation 三域 setter（`CommandProvenance.cs:45-51`），
   Agent 级写入**无归因、无仲裁**。

结论：这是**产品边界/架构问题**，不是「玩家队豁免漏判一处」的 bug；单点补 `IsPlayerTeam` 会掩盖问题（派单 §二 明令禁止）。

---

## 4. 玩家队豁免现状清单（13 个判定点，均为编队/队级）

`CombatAiMissionLogic.cs` : 678（意图决策）、1735（阵型战术）、1821/1848（远程位）、1914（兵力保全）、
1961/1972（僵局）、2018/2030（目标选择）、2091（朝向修正）、2151（阵框释放）、2234（异常检测）；
`BraceGlue.cs` : 70/191、`CavalryGlue.cs` : 92、`FormationCommandExecutor.cs` : 230、
`FormationSlotGlue.cs` : 81、`FormationControlLeaseGlue.cs` : 199/247/311/469、`MoraleGlue.cs` : 92、
`ProtectGlue.cs` : 60/216、`SkirmishGlue.cs` : 67。

**缺失判定的个体路径（须在 t3 补齐 / 或随退役消失）**：
`TickBlock`（B1，须补或删除）、`TickMovement`（F1）、`TickSurvival`（H1）、`TickRanged`（C1-C3），
以及 `CombatBrainAgentStatCalculateModel`（A2-A6，引擎级，补判定的正解是**整链退役**）。

退役后的目标语义（派单 §六）：豁免开启时 **Formation 命令 / 编队行为权重 / 移动方向 / 格挡换防选敌换武器 /
AI 士兵战斗能力属性 / 阵框释放** 对玩家队一律**零写入**，仅允许只读观测且观测不得改变行为；
豁免**关闭**时也不得复活已退役的逐兵系统（只允许军队级/编队级接管）。

MCM 文案现状：`CombatBrainMcmSettings.cs:23` `[SettingPropertyBool("玩家队豁免", ...)]` **无 HintText**，
无法表达「全控制面豁免」语义（派单 §六「MCM 说明文字改得足够明确」的落点）。

---

## 5. §十.9 仍将保留的 Agent / 编队 API 写入清单（含保留理由）

**保留的 Agent 级主动写入：1 项（K1，已裁决保留）**；终态下 CombatBrain 对 Agent 的主动写入仅此单点（门禁 68 条含零容忍硬断言）。

| # | API / 写入 | 位置 | 保留理由（若裁决保留） | 恢复原版语义的方法 | 状态 |
|---|---|---|---|---|---|
| K1 | `Agent.SetFormationFrameDisabled()` | **终态行号 `CombatBrain/Ai/CombatAiMissionLogic.cs:2097`**（判定源 `CombatBrain/Ai/FrameReleaseGlue.cs`；本行旧值 `:2188` 与 §2.4 内 `:2151/:2188` 为 **f6634f7 审计时点坐标**，终态因退役位移） | 编队级**统一接战政策**：编队级触发 + 同政策 + 无逐兵战术判断 + 一次性 latch（只读 `IsFormationFrameEnabled`）+ 5s 冷却 + 玩家队零写入 | 移除该调用即可：引擎在 `HumanAIComponent.ParallelUpdateFormationMovement` 每帧重建帧框、离开 Charge 后自行恢复；同时清 `SlotAssigner.FrameDisabledAgents` 消费 | **resolved（产品裁决：保留；见 §6 M7 与 §0b.3）** |

**保留的编队级写入（不属 Agent 级，列出以便边界透明）**：

| # | API | 位置 | 理由 | 恢复原版语义的方法 |
|---|---|---|---|---|
| L1 | `Formation.SetMovementOrder` / `SetFacingOrder` / `SetArrangementOrder` | `FormationCommandExecutor.cs:370/374/377`（唯一执行器）；`RouteAOrderAdapter.cs:35`（Route A 单编队） | CPS 唯一出口：所有战术模块只提交提案，由 Arbiter 裁决后单点落地；这是「编队去哪里」的合法表达通道 | 关闭 CPS 开关（`Cps3RuntimeSettings`/`Cps2PoCSettings`）即零写入；或整体停用 `FormationCommandExecutor` |
| L2 | `Formation.SetControlledByAI(bool)` | `FormationControlLeaseGlue.cs:375/426/455` | ATC-5R 租约 PoC 的单域排他控制；默认关 | `FormationControlLeasePocEnabled=false`（默认）；`ReleaseAll` 幂等恢复 |
| L3 | `FormationAI.AddAiBehavior` / 行为权重 | `RouteAOrderAdapter.cs:175`；权重策略 `FormationCommandPolicy.cs` | Route A 以引擎公开行为权重表达 Movement 域意图（含冲突降权） | 释放 Route A（`CbRouteAPoC.ResetAll()`，Mission 收尾 :1677/1688 已接） |

**只读诊断（不改变行为，保留）**：`DecisionObservatory`（分支计数）、`InGameFeedback`（聚合播报）、
`BattleBenchmark`（分段耗时）、`BattleTelemetry`、`CommandProvenance`/`SetOrderObserver`（Formation 三域只读 Prefix）、
`AnomalyDetector`、`CombatLog`、`MovementGlue.Report()` 之外的日志类输出。
注：`MovementGlue` 的直方图（: 65-71/335-380）随 F1 退役；若需保留「个体写入计数」的观测，
按派单 §九 改为**临时只读诊断计数**（Formation 级 / Agent 移动 / 格挡 / 目标 / 换武器 / 属性覆盖六类）。

---

## 6. 待 Integration Owner / Captain 裁决清单（规范与编号）

> 本节是**裁决台账**：原为「待裁决清单」，2026-09-11 由 Captain / Integration Owner 角色位逐条裁决并落地；
> 每条标 `resolved（已落地）` 并给出落点。规范文本的实际改动见 `docs/knowledge/CONSTITUTION.md`（§3.1/§3.2/P2 + 新增 HC-2）、
> `docs/knowledge/NOMENCLATURE.md`（§2 F/X 行 RETIRED）、`docs/adr/ADR-0005`（accepted，已登记 README §5）。

| # | 事项 | 现状 / 落点（证据） | 规范文本 |
|---|---|---|---|
| M1 | 「个体层」重新定义为「原版 AI 执行层/适配边界」 | **resolved（已落地）**：`CONSTITUTION.md` §3.1 三层定义已改写；§4 P2 行同步改写 | `CONSTITUTION#architecture-principles` §3.1、`#design-principles` P2 |
| M2 | 明确禁止周期性逐兵抢写移动/格挡/目标 | **resolved（已落地，位置=§5）**：新增 `CONSTITUTION#hard-invariants` **HC-2 个体控制面禁令**（禁周期性逐兵抢写 + 禁改战斗能力数值 + 玩家队全控制面豁免 + 例外须按 ADR-0005 登记） | `CONSTITUTION` §5（HC-2） |
| M3 | 明确 CombatBrain 产品核心 = 军队级 + 编队级决策 | **resolved（已落地）**：§3.2 新增「产品核心 = 军队级 + 编队级决策；不替每个士兵设计独立人格/格斗能力/微观移动路线」 | `CONSTITUTION#architecture-principles` §3.2 |
| M4 | 明确玩法能力数值不属于 CombatBrain 职责 | **resolved（已落地）**：§3.2 明确「格挡/招架能力、攻击决定概率、射击误差、随机防守方向概率等不得由 CombatBrain 覆盖（含 `AgentStatCalculateModel` 链替换）」 | `CONSTITUTION` §3.2 |
| M5 | 本次方向变化登记为正式 ADR | **resolved（已落地）**：`docs/adr/ADR-0005-individual-control-plane-retirement.md` → **status=accepted**，并在 `docs/adr/README.md` §5 Register 登记 | `docs/adr/README.md` §5 |
| M6 | F4/F6/F7/F9/F12/X2/X5 退役部分标 `RETIRED` | **resolved（已落地）**：`NOMENCLATURE.md` §2 `F<n>`/`X<n>` 行已标 RETIRED 并写明 RETIRED 语义（编号沿用但不代表现行功能） | `docs/knowledge/NOMENCLATURE.md` §2 |
| M7 | 阵框释放（K1）保留或退役的**产品裁决** | **resolved（产品裁决：保留，编队级统一接战政策）** —— 附 v1.4.8 API 核验（§0b.3：类型 `TaleWorlds.MountAndBlade.Agent`，`SetFormationFrameDisabled`/`SetFormationFrameEnabled`/`IsFormationFrameEnabled`/`TrySetFormationFrame` 的签名与调用方）＋ 4 项硬条件（编队级触发 / 一次性 latch+5s 冷却 / 玩家队零写入 / 运行期计数实证）＋ 运行期验证债（动作振荡与恢复时机由 §九 A/B + FrameRelease 单列计数闭环，观测异常则回报 Captain 再议） | 执行落点：`docs/agent-control-retirement-ab-checklist.md` §3/§4；规范层影响见 M2 |
| M8 | CPS 功能冻结令冲突处理与合并时点 | **待 Integration Owner 裁决（唯一未闭环规范项）**：本任务为「退役/减写者」，与冻结令目标同向，不构成冻结令例外；分支 `feature/agent-control-retirement` 未合入 main、未部署，合并时点与 waiver/architecture decision 记录需 Integration Owner 确认（见 ADR-0005「实施与合并」、最终报告 §5） | CONSTITUTION §7.2 + INDEX §4 Waiver |
| M9 | MCM 文案与死开关清理 | `HandSkillStrength` 死开关已随 t2 删除（t8 承接声明）；MCM「玩家队豁免」文案已改为「玩家队豁免（全控制面）」+ 全控制面 HintText（`CombatBrainMcmSettings.cs:23-30`，t3） | 文字/设置层已落地；规范层无剩余待裁决项 |
| M10 | `_profiles`/`FighterProfile` 是否整表退役（I12） | **resolved（按 R3「消费者归零即删」执行完毕）** —— t3 已删除 `_profiles` 字典与 `GetProfile`（`CombatAiMissionLogic.cs`）；四个字段的生产读取点（`DecisionJitterGlue.cs:47` 读 `PredictConf`、`MovementGlue.cs:160` 读 `FlankAware`）随模块退役归零，`FeintRate`/`Shakiness` 在 t2 后即无读取者；**t9（SCOPE-AMEND-2）已删除 `FighterProfile` 四字段表与 `FighterProfileFactory`**（提交 90d2fbf，`-66`），并核实 `rg '\bFighterProfile\b'` 生产代码零命中；**保留** `SoldierInput`/`PrimaryWeaponClass`（在用消费者：`Ai/FormationSlotGlue.BuildSoldierInput` → `Formation/TroopClassifier.Classify`） | 已闭环；离线侧同步由 b0b1628 完成（`TestFighterProfile` 15 条断言随表退役删除，1162→1147） |
| M11 | 契约文本错误更正（t9 卡「`SoldierInput` 随链退役」） | **resolved（口径更正）**：以代码实情为准——`SoldierInput`/`PrimaryWeaponClass` **保留**（活消费者 `Ai/FormationSlotGlue.cs:198` → `Formation/TroopClassifier.cs:27`）；开发12 的 finding `T9-SCOPE-2` 据此关闭 | 本审计表 + 最终报告 §2 |
| M12 | `CombatBrain/Enums.cs:36` `SoldierTier` 无消费者死类型（F-7） | **待执行（承接任务，不在 t5 文档范围）**：厂长裁定按 R3 退役（不批 waiver）——删枚举 + 更正 `Enums.cs:44` 滞留注释（仍指向已删 `FighterProfile.Classify`）+ 复查 `OfflineTests` link 与编译链 + 四命令复跑 | `docs/qc-review-agent-control-retirement.md` §9.1（F-7 = low） |

**QC findings 消化登记（t4 报告 §9/§9.1）**：F-1（§5 K1 状态/行号漂移）→ 本表 §5 K1 行已改 `resolved` 并标注 f6634f7 时点坐标；
F-2（ab-checklist 门禁 55→68）→ 已同步；F-3（既有 `BracePlan.survivalRetreating` 死参数注释）→ 登记为**既有残留**（非本分支引入，
产品代码不在本轮退役范围，后续卫生提交处理）；F-4（`DiagnosticCounters` 死字段/哨兵语义）→ 登记为**后续卫生提交**，
当前静态门禁为零写入主证据；F-5（−31 逐条名单）→ 见 QC 报告 §4（本表 §0b.2 为模块级映射，点名来源已注明）；
F-6（门禁补 Agent 级 `SetControlledByAI`/`AddAiBehavior` 禁止模式）→ 下轮门禁加固；F-7 → 见 M12。

---

## 7. 与任务卡的覆盖对应

| 派单条目 | 本审计落点 |
|---|---|
| §四 审计范围（9 项） | §2.1–§2.10 逐项；J 组为穷尽性核对 |
| §四 输出列要求（8 列） | §2 各表列头（文件:方法:行 / 节拍 / 对象 / 玩家队 / 来源 / 并行 / 结论 / 证据与恢复方法） |
| §十.1 审计表 | 本文件 |
| §十.2 ADR 草案 | `docs/adr/ADR-0005-individual-control-plane-retirement.md` |
| §十.9 保留的 Agent API 写入清单 | §5（K/L/只读诊断三组） |
| §五.8 阵框裁决依据 | §2.4.1 六项逐条 |
| §六 玩家队豁免全控制面 | §3 根因 + §4 现状与缺失点 |

## 8. 本审计的自身边界（未做什么）

- 未运行游戏、未部署、未触碰 `%TEMP%\CombatBrain.log` 与既有 CPS 证据（只读审计，无运行期证据）；
- 未修改任何产品代码 / 测试 / 规范核心；产物仅本文件 + ADR 草案；
- §2.4.1 ⑥（动作振荡）与 §九 观感类结论**离线不可证**，一律记为**运行期验证债**，
  不得据此写成无条件 pass。
