# ADR-0005: 个体控制面退役与产品边界收口（Individual Control Plane Retirement）

- **Status**: proposed（草案；正式化 = Captain / Integration Owner 权限，见 `docs/adr/README.md` §5）
- **Date**: 2026-09-11
- **Deciders**: 产品方向由用户裁决（CB-PROMPT-0038 派单「最新产品决定」）；架构级采纳待 Captain / Integration Owner review
- **Related task**: CB-PROMPT-0038 `CombatBrain-个体控制面退役与产品边界收口-正式派单-2026-09-11.md`（series_id=agent-control-retirement）
- **Scope note**：本 ADR 只覆盖**产品边界与个体控制面退役**这一条决策。
  不含 CPS Route A vs Route B、租约设计、CPS GO 判定等进行中决策（那些等各自闭环后另行成档）。

## Context

**触发事实（运行期观测）**：最新 CPS 第 1 场游戏内测试发现，即使开启了「玩家队豁免」，
我方（玩家队）AI 士兵仍出现快速切换动作、小碎步、反复改变方向、频繁换防等近似「抽搐」的表现。

**静态审计结论（本任务 §四 审计表，`docs/agent-control-surface-audit.md`）**：

1. 玩家队豁免的 13 个判定点**全部位于编队/队级路径**（意图决策、阵型战术、远程位、兵力保全、僵局、目标选择、
   朝向修正、阵框释放、异常检测、各编队 Glue）——豁免面只覆盖「编队级意图与命令」。
2. `TickLod`（`CombatBrain/Ai/CombatAiMissionLogic.cs:607-619`）对 `mission.Agents` **全量**评估 LOD，
   玩家队 AI 士兵同样获得 L2/L3 剧场等级。
3. 因此以下 **Agent 级模块对玩家队 AI 士兵同样生效**，且**完全没有玩家队判定**：
   - F6 走位：`MovementGlue.cs:223` `agent.SetMovementDirection`（0.2s 节拍 × 跳帧，L2+）；
   - F7 求生：`SurvivalGlue.cs:197` `SetMovementDirection`（0.3s，濒死 + 被集火）；
   - F9 远程：`RangedGlue.cs:187` `SetTargetAgent`、`:197` `TryToWieldWeaponInSlot`、`:232` `SetMovementDirection`（0.3s）；
   - F4 格挡：`CombatAiMissionLogic.cs:1605` `defender.EnforceShieldUsage(...)`（0.1s，全部 L3）；
   - F12 抖动：`DecisionJitterGlue.ShouldAct` 横切挂在上述 4 个决策点（犹豫/延迟/走形）；
   - X2 手活属性：`CombatBrainAgentStatCalculateModel.UpdateAgentStats`（`SubModule.cs:31` 注册）改
     格挡/招架/攻击决定概率/射击误差/随机防守方向概率，作用于 `IsHuman && IsAIControlled`（含玩家队 AI 士兵）；
   - X5 逐兵补位：`FormationSlotGlue.cs:183` `SetMovementDirection`（0.5s + 2s 冷却，L3；**此路径已有玩家队豁免**）。
4. 这些写入与**原版 `HumanAIComponent` 同帧并行**（代码注释自认：`MovementGlue.cs:28`「原版每帧自行驱动移动，
   本层按 0.2s 叠加决策输入」；`RangedGlue.cs:24-25`；`CombatBrainAgentStatCalculateModel.cs:9-10` 覆盖模型链）——
   构成单兵双重控制与高频状态翻转。
5. CPS 当前只收口 Formation 的 Movement/Facing/Arrangement 三域 setter（`CommandProvenance.cs:45-51`），
   **Agent 级写入无归因、无仲裁、不可观测**。

**根因定性**：这不是「玩家队豁免漏判一处」的 bug，而是早期产品方向（让 CombatBrain 也负责单兵微操作）
遗留下来的架构问题。用补 `IsPlayerTeam` 单点掩盖会保留架构病症。

## Decision

**产品边界收口**：

> CombatBrain 为军队和编队提供战术大脑，不为每一个士兵设计独立人格、格斗能力和微观移动路线。

职责重新划分：

1. **CombatBrain 负责**：观察战场、判断战局阶段、形成战术意图、选择目标、分配编队任务、协调攻防、
   下达**编队级**命令（经 CPS 唯一执行器 / Route A / 编队行为权重表达）。
2. **原版 AI 负责**：单个士兵如何迈步、挥刀、格挡、换防、选择具体攻击动作与近战微操作。
3. **禁止**：
   - 持续向单兵输入移动方向或格挡方向（周期性逐兵抢写）；
   - 修改单兵格挡、招架、攻击概率、射击误差、随机防守方向等**玩法能力数值**；
   - 周期性逐兵选敌 / 强制换武器 / 逐兵后撤移动。
4. **允许**：因游戏 API 限制而遍历士兵执行一个**统一的编队状态**（同一政策、无逐兵独立决策器），
   决策必须来自编队级状态。
5. **个体层重新定义**：`CONSTITUTION#architecture-principles` §3.1 的「个体层」不再是 CombatBrain
   自有的逐兵决策层，而是「**原版 AI 执行层 / 适配边界**」（CombatBrain 只做只读观测与编队级适配）。
   → 该规范文本改动**待 Integration Owner 裁决**（本 ADR 不自行宣称规范已改变；见 Related Canonical Rules）。
6. **玩家队豁免语义统一**：开启时 CombatBrain 对玩家队**不做任何主动战斗控制写入**
   （Formation 命令 / 编队行为权重 / 士兵移动方向 / 格挡换防选敌换武器 / AI 士兵战斗能力属性 / 阵框释放），
   允许只读观测但观测不得改变行为；玩家队里的 AI 士兵同样在豁免范围内。
   豁免**关闭**时也不得复活已退役的逐兵系统（只允许军队级/编队级接管）。
7. **编号语义**：F4/F6/F7/F9/F12/X2/X5 中被退役的部分标 `RETIRED` 或重新定义，避免编号继续暗示旧功能有效
   （落点 `docs/knowledge/NOMENCLATURE.md:22`；属规范层，待裁决）。

**具体退役映射（本决策的实施口径，详见审计表 §2）**：

| 编号 | 退役内容 | 保留/上收内容 |
|---|---|---|
| X2 | `CombatBrainAgentStatCalculateModel` 注册与全部单兵能力覆盖；`HandSkillProfile`；与 `AttackSuppression` 的耦合 | 无（玩法能力数值不属产品职责） |
| F4 | `EnforceShieldUsage` 调用点；攻击方向统计 `AttackStats`/`PredictiveBlock`（无消费者） | 无 |
| F9 | 逐兵 `SetTargetAgent` / `TryToWieldWeaponInSlot` / 后撤 `SetMovementDirection` | 编队级远程任务判断（是否继续射击/整体后撤/转换任务/目标优先级） |
| F6 | 逐兵 `SetMovementDirection` 与 Advance/StepBack/Strafe/RetreatToAllies/Hold/走形扰动 | 编队级 MovementOrder / Formation Mission / Route A |
| F12 | 逐 Agent 犹豫 / 延迟 / 疲劳 / 移动方向扰动（每 Agent 状态字典 + 随机 + 疲劳累计） | 指挥层意图切换迟滞、编队任务切换、编队命令防抖（`DecisionDelay.HoldTime/SwitchMargin`、Arbiter/Executor） |
| F7 | 单兵低血量自行寻友军并 `SetMovementDirection` 后撤 | 编队级重整/撤退信号（伤亡/完整度/重伤比例 → `IntentType.Reform`） |
| X5 | 逐兵向 Slot 目标 `SetMovementDirection` 执行腿 + 逐 agent 冷却字典 | 编队构成分类、编队职责、阵宽/阵深/Arrangement、编队纪律、纯观察性 Slot 数据 |

**阵框释放（X4 / `Agent.SetFormationFrameDisabled`）**：不按「用了 Agent API 就一定删除」处理，按 §五.8 语义裁决：
必须同时满足「统一编队接战状态触发 / 同政策 / 无逐兵独立战术判断 / 有激活-恢复-生命周期边界 / 不作用于玩家队 /
不造成周期性重复抢写或动作振荡」。审计结果：5 项成立，**第 ⑥ 项（动作振荡）离线不可证**，
第 ④ 项「恢复边界」缺失（全仓无 `SetFormationFrameEnabled`）→ **裁决权交 Integration Owner**，
默认建议 = 保留并挂运行期验证债；若 §九 A/B 观测证伪则一并退役。

## Alternatives Considered

1. **只给个体路径补 `IsPlayerTeam` 判断（最小改动）** —— 拒绝（派单 §二 明令）：
   只掩盖玩家队观感，保留「CombatBrain 与原版 AI 双重控制普通 AI 士兵」的架构病症；
   也保留 Agent 级写入的不可观测性（CPS 仍无法归因）。
2. **把 Agent 级写入纳入 CPS 归因/仲裁（扩大 CPS 控制面）** —— 拒绝：
   与产品边界收口方向相反（要做的是**减少** Agent 级写者，而不是给它建审计通道）；
   且无 Harmony/引擎 hook 无法拦截原版写入，仲裁语义会退化为「比谁后写」。
3. **用更大的个体模块替换旧个体模块（重写而非退役）** —— 拒绝：
   任何「为每个士兵跑独立决策器」的实现都会重新引入双重控制、抖动与性能预算问题（铁律②：聚合 > 个体遍历）。
4. **保留 X2 手活但只降强度（`HandSkillStrength` 调小）** —— 拒绝：
   仍属「改玩法能力数值」，且强度开关当前**无法从 MCM 调整**（死开关）。
5. **整个 X4 阵框释放一并删除（一刀切）** —— 未采纳为默认：
   语义条件 5/6 成立且观感收益明确（RBM 同款犬牙交错战线）；但保留需运行期证据背书 → 交裁决。
6. **保留 F12 个体抖动作为「拟人」卖点** —— 部分拒绝：
   「指挥官会迟疑、意图需要稳定期」保留并上收至指挥层；「每个士兵随机抽一次是否执行」删除
   （个体抽签正是观感上「小碎步/反复改变方向」的放大器）。

## Rationale

- **产品一致性**：核心目标（军队/编队战术大脑）与逐兵人格+格斗能力+微观路线是两套产品方向；同时做会互相破坏。
- **技术正确性**：单兵动作由原版状态机（`HumanAIComponent`）驱动，CB 侧旁路写入必然产生争抢；0.1–0.3s 周期叠加
  正是「0.1/0.2/0.3 秒动作翻转」的时序来源。
- **可观测性**：退役后 CombatBrain 的写入面收敛到 CPS 已收口的编队三域，归因链（`CommandProvenance`）可覆盖全部写入，
  「谁写的」不再有盲区（与 ADR-0004 的归因纪律同向）。
- **性能纪律（IL-2）**：逐兵决策/逐 Agent 状态字典本就是「个体遍历」开销；退役直接移除这类开销，
  有利于 500 人 30 FPS ≤10% 帧预算的红线（`CONSTITUTION#performance-discipline`）。
- **合规边界**：能力数值改动虽不违信息公平（IL-1），但属「玩法数值」，与 §3.2 产品边界冲突；
  退役使规范与产品方向重新一致。

## Consequences

**正面**：

- 玩家队 AI 士兵动作回归原版；敌方仍保留军队级/编队级战术差异（观感目标见派单 §九）；
- CombatBrain 写入面 = 编队三域（+ 待裁决的阵框释放），CPS 归因完整；
- 移除逐兵决策与每 Agent 状态字典 → 兼顾性能预算与「Mission 结束无残留状态」要求。

**代价 / 风险**：

- 失去 F4 预测式格挡、F6 个体走位、F7 单兵求生、F9 个体选敌、F12 个体抖动、X2 手活带来的**个体级观感增强**
  （这些是早期「观感补强」的成果，退役是明确的方向取舍）；
- 需要清理大量既有离线断言与状态字典（`OfflineTests/Program.cs` 中 `TestHandSkillProfile` :1382、
  `TestAttackSuppression` :908、`TestX5ExecutionPolicy` :1904 等），测试基线会有实质变化（只增不减纪律下需显式说明删改理由）；
- **运行期验证债**：本决策的观感收益（动作翻转消失、小碎步消失、盾向稳定、500 人性能）**必须靠游戏内 A/B 验证**，
  离线测试与静态扫描只能证明「写入为零」，不能证明「观感变好」——未做 A/B 前不得写成无条件 pass；
- 与 CPS 功能冻结令的交互：本任务为**退役/减写者**，与冻结令目标同向，但仍属架构级变更，
  合并时点需 Integration Owner 裁决并留 waiver/architecture decision 记录。

## Related Canonical Rules

- `CONSTITUTION#architecture-principles`（§3.1 三层两横切；§3.2 产品边界）—— **M1/M3/M4 待裁决**
- `CONSTITUTION#design-principles`（P2 分层决策架构；P4 性能纪律）—— **待裁决**
- `CONSTITUTION#hard-invariants`（IL-1 信息公平 / IL-2 性能预算 / IL-3 剧场效应）—— 本决策不触碰 IL-1
- `CONSTITUTION#performance-discipline`（§8.1 聚合 > 个体遍历）—— 本决策的支撑条款
- `CONSTITUTION#command-ownership`（§7.1 归因纪律 / §7.2 冻结令 / §7.3 命令写入纪律 / §7.4 Waiver）
- `docs/knowledge/INDEX.md` §4 Waiver Contract（冻结令冲突需显式记录）
- `docs/knowledge/NOMENCLATURE.md` §2（F/X 编号体系 → 退役项标 RETIRED，**待裁决**）
- `docs/adr/ADR-0001`（分层架构）、`ADR-0002`（反作弊闸）、`ADR-0004`（命令所有权与归因纪律）

> 规则冲突点：`CONSTITUTION.md:40/64` 现把「个体层（走位/格斗/求生）」列为 CombatBrain 正式架构层，
> 与本 ADR 的「个体层 = 原版 AI 执行层」定义**直接冲突**。按 Authority 协议：
> **ADR 不静默覆盖 CONSTITUTION**；本 ADR 保持 `proposed`，直到 Integration Owner 显式更新 CONSTITUTION。

## Related Evidence

- `docs/agent-control-surface-audit.md`（本任务审计表：全部写入点、节拍、玩家队判定、结论与 `文件:行` 证据）
- CPS 第 1 场游戏内测试的抽搐现象（用户派单 §一 的观测描述；运行期证据由 CPS 侧报告域承载）
- 静态扫描（`CombatBrain/**/*.cs` 去注释/去字符串后扫描 Agent 级 API 实调用；见审计表 §1）
- 源码：`CombatBrain/Ai/MovementGlue.cs`、`SurvivalGlue.cs`、`RangedGlue.cs`、`FormationSlotGlue.cs`、
  `FrameReleaseGlue.cs`、`DecisionJitterGlue.cs`、`CombatAiMissionLogic.cs`、`AttackSuppression.cs`、
  `AttackSuppressionState.cs`、`CombatBrainAgentStatCalculateModel.cs`、`HandSkillProfile.cs`、`SubModule.cs`、
  `CombatBrain/Compliance/DecisionJitter.cs`、`CombatBrain/Formation/X5ExecutionPolicy.cs`、`CombatBrain/Mcm/CombatBrainMcmSettings.cs`

## Supersedes / Superseded by

- **Supersedes**：无既有 ADR。实际取代的是**早期产品方向**——「CombatBrain 同时承担单兵人格/格斗能力/微观移动」
  （体现为 F4/F6/F7/F9/F12/X2/X5 的个体层实现，含 `HandSkillProfile` 的 RBM 蓝本口径）。
- **Superseded by**：无。若未来需要重新引入个体级控制，必须走新 ADR + 显式 Waiver，不得以「恢复旧功能」名义复活。

## Rollback / 可回滚点

- 实施在独立分支 `feature/agent-control-retirement`（worktree `E:\Agent\cb-agent-retirement`），**未合入 main、未部署**；
- 回滚 = 丢弃该分支 / checkout 回分叉点（worktree 建立时 HEAD `f6634f7`）；
- 本 ADR 若被 Integration Owner 拒绝（rejected），退役实现不得合并；审计表可按需保留为纯诊断证据。
