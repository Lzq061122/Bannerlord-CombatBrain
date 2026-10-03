# 旧资产可复用性审计 + RBM 参照证据报告（r4）

> 角色：代码分析师 + 旧版研究 + RBM 研究（三合一资产审计）｜只读检索/只读分析，未修改任何生产代码、未运行实验。
> 唯一需求源：`Charter V0.2.md`。旧版 1.x = 被审计对象；RBM = 参照证据，非需求源。
> 证据强度列：**[本机实证-源码]**=已读 `overseer/_work/combatbrain-d-refactor` 或 `FIX/rbm_decompiled` .cs；**[本机实证-dll符号]**=在 `_backups/CombatBrain-*.dll` 用 `grep -a` 验得类名；**[r1报告转述]**=仅 r1 文档声称，本次未独立核验；**[未验证]**=无证据。

---

## 第一部分：资产发现（实证）

| 资产位置 | 状态 | 证据 |
|---|---|---|
| `E:\MBmod\overseer\_work\combatbrain-d-refactor\CombatBrain\**\*.cs` | **源码在机**（含 CombatLog/VecMath/TeamGeometry/BufferPool/CombatEventBus/SituationVector/IntentEngine/BehaviorLodScheduler/CombatAiMissionLogic/TerrainSampler/KnowledgeView/VisibilityOracle/DecisionJitter/TacticManual/CavalryWaveFSM/CounterMatrix/CampaignHabits/OpponentModel/Combat/*/Ranged/RangedBrain/Survival/* 等 54 个 .cs） | [本机实证-源码] |
| `E:\MBmod\_backups\CombatBrain-{diag,patch,r3-395fe13,telemetry}-*.dll` | 4 个 dll 备份；用于符号交叉验证 | [本机实证-dll符号] |
| `E:\MBmod\CAI\src\CAI\**` + `CAI/.tools/ilscan/ilscan.cs` + `CAI/tests/CAIRegress/Program.cs` | CAI 战役层源码、IL 原生 API 扫描工具、离线纯逻辑回归工程均在机 | [本机实证-源码] |
| `E:\MBmod\FIX\rbm_decompiled\RBM` + `RBMAI`（RBM/RBMAI 反编译 .cs）及 `E:\MBmod\IDEA\RBM\bin\**\RBM*.dll` | RBM 反编译源码 + 本体 dll 在机 | [本机实证-源码]/[本机实证-dll符号] |
| `CombatBrain-d-refactor` 中的 **CPS / ATC** 源码 | **不在机**：对全树 `grep -rIl "CPS|ATC-|FormationCommandExecutor|ControlPlane"` 返回空；`_patch/` 为空；`.agent-teams/` 无文件 | [本机实证-源码]（负结果） |
| dll 符号交叉验证 | 4 个 dll 均含 `CPS`、`FormationCommandExecutor` 字符串；**不含 `ATC` 字符串** | [本机实证-dll符号] |

> **关键纠正（相对 r1）**：r1 将 `CPS 控制面` 标为"高·沿用为 2.0 基座 / REUSE"。本机实证显示 **CPS/ATC 源码根本不在可用源码树中**——仅 dll 残留 `CPS`/`FormationCommandExecutor` 符号，而 `ATC` 连 dll 字符串都无。故治理层**无法 REUSE**，最多 REFERENCE（设计概念），2.0 必须按 Charter 重新实现（即 CB-002）。

---

## 第二部分：旧版逐项审计

判定含义：REUSE=设计理念+实现均符 Charter，可直用；ADAPT=机制有价值但职责/语义需改造；REFERENCE=仅作技术/经验参考；REJECT=冲突或限制新架构。

### A. 工程基建类（预期 REUSE/ADAPT）

| 资产 | 证据 | 判定 | 宪章依据 | 理由 |
|---|---|---|---|---|
| CombatLog.cs（缓冲异步落盘） | [本机实证-源码] | **REUSE** | §3/§14 无隐藏信息、纯基建 | 零游戏依赖、零越权，直接搬入 2.0 遥测。 |
| VecMath.cs（Dist/Clamp01/LogMapRatio） | [本机实证-源码] | **REUSE** | 通用数学 | 纯函数，直接搬。 |
| CombatEventBus.cs（发布/订阅总线） | [本机实证-源码] | **REUSE** | §13/§24 反馈机制基建 | 作 2.0 反馈总线（CB-008 观测源），直接用。 |
| TeamGeometry.cs（编队/队中心聚合） | [本机实证-源码] | **REUSE** | §5/§6 己方/敌方位置认知 | 仅读 `Formation.CachedMedianPosition`/`SmoothedAverageUnitPosition` 公开缓存（代码含"反作弊闸"注释），纯 Formation 级，无单兵越界。直接复用。 |
| BufferPool.cs（[ThreadStatic] 对象池） | [本机实证-源码] | **REUSE** | 性能基建 | 零依赖，直接搬。注：其 `GetAgentList` 槽原供单兵缓冲用，2.0 仅用编队级槽。 |
| BattleBenchmark.cs（帧耗时分节） | [本机实证-源码·头] | **REUSE** | §16 可审计性/遥测 | 性能红线监测，直接复用框架。 |
| ReplayRecorder.cs（环形事件录制） | [本机实证-源码] | **ADAPT** | §13 观察战果 | 机制（环形缓冲订阅事件总线）可复用为"决策回放/指令溯源"；但其消费方原是 F10 跨战役记忆（REJECT），2.0 仅复用录制机制，不改写玩家习惯。 |
| OfflineTests / CAIRegress + ilscan + CaiHarmony | [本机实证-源码] | **REUSE** | Charter 离线测试义务 | 离线纯逻辑回归工程、IL 原生 API 扫描、Harmony 挂载模板均可直接借力（r1 结论成立）。 |
| CombatAiMissionLogic.cs（MissionBehavior 宿主） | [本机实证-源码] | **ADAPT** | §19 Formation 层挂载点 | 宿主骨架（OnMissionTick 低频分组、每 Team 状态、Benchmark、事件总线接线、真实地形预采样模式）可复用；**但其内部大量直接驱单兵**（`defender.EnforceShieldUsage`、`PredictiveBlock`、`FighterProfile`/`DecisionJitter` 逐兵注入）→ 必须剔除全部单兵 Glue，仅留宿主与 Mission/Team/Formation API 用法。 |

### B. 数据/机制类（预期 ADAPT）

| 资产 | 证据 | 判定 | 宪章依据 | 理由 |
|---|---|---|---|---|
| SituationVector（12 维归一化向量） | [本机实证-源码] | **ADAPT** | §26 共享战场状态表示 | 数据骨架（struct + 索引器 + 标签）可重用作 `BattleState`；但须去"单兵语义"、去迷雾字段，重定义为"己/敌/战场"三类通用状态（r2 CB-001）。不可沿用其"12 维固定含义"作为评价结论。 |
| IntentEngine（hold_time/switch_margin 防抖） | [本机实证-源码] | **ADAPT** | §16 决策节奏/最小稳定时间 | **仅** `switchMargin=0.15`、`holdTime=6s` 的"计划最小稳定时间"机制可保留，转作 CadenceController；其 `score=ΣV·W` 选固定意图的加权打分逻辑随 TacticManual 一并 REJECT。 |
| BehaviorLodScheduler（LOD/决策步长） | [本机实证-源码] | **ADAPT** | §16 Decision Cadence | 阈值/决策步长模型可借作指挥节奏；但 `PlayerFocusRadius=70f` 的"剧场效应"是原生游戏特性，r2 明确"原生无剧场效应须自实现距离闸"，故剔除该包装、自实现距离闸。 |
| TerrainSampler.cs（开战一次性地形预缓存查表） | [本机实证-源码] | **ADAPT** | §6/§9 地形站位认知 | 实现干净：真实 `Scene.GetTerrainHeight` 委托注入、开战预采样、战中 O(1) 只读、失败降级中性 0.5（"宁缺毋假"）。直接复用查表机制；仅剔除其向 KnowledgeView/VisibilityOracle 的迷雾接线（r2 CB-013）。 |

### C. 冲突类（预期 REJECT/REFERENCE）

| 资产 | 证据 | 判定 | 宪章依据 | 理由 |
|---|---|---|---|---|
| KnowledgeView.cs（迷雾置信衰减+位置噪声） | [本机实证-源码] | **REJECT** | §14 信息公平 | 主动对 AI "加噪致瞎"（注释直书"先让 AI 瞎再谈聪明"），与 §14 信息公平直接冲突；且源码中 `PerceptionEnabled=false` 已停用、无消费者。 |
| VisibilityOracle.cs（O(格) 可见性+地形遮挡） | [本机实证-源码] | **REJECT** | §14 | 构建视野遮挡/未知区域，§14 明确"当前不构建视野遮挡/未知区域"。 |
| DecisionJitter.cs（拟人迟疑/犹豫/走形） | [本机实证-源码] | **REJECT** | §15 + §16 | §15"不主动制造错误"；且它是 **agent 级**抖动（DelayBase/Hesitate/Wobble per soldier），不是 §16 的 Formation 指挥延迟。宿主 `CombatAiMissionLogic` 中 `_jitterBlock.ShouldAct` 直接改单兵举盾 → 双违。 |
| TacticManual.cs（9 意图×12 维权重 + ProtectArcher/BraceFormation） | [本机实证-源码] | **REJECT** | §26 专用情形规则链 | 固定战术枚举+写死权重即宪章禁止的"专用情形规则链"；`ProtectArcher`/`BraceFormation` 是定制战术。2.0 由 CB-006/CB-007 通用任务取代。 |
| CavalryWaveFSM.cs（骑兵专用多波次 FSM） | [本机实证-源码] | **REJECT** | §26 + §23 | 兵种专用 FSM（Scout→Setup→Charge→Recall→Reform）违背 §23"不把骑兵设计为独立指挥系统"与 §26 通用优先。骑兵只是承担"突破"任务的 Formation。 |
| Combat/AttackStats.cs, FighterProfile.cs, PredictiveBlock.cs | [本机实证-源码] | **REJECT** | §20 单兵层 | 逐兵格挡预测 + `defender.EnforceShieldUsage` 直接控制单兵举盾，硬越界 §20。 |
| Ranged/RangedBrain.cs（单兵选靶/弹药） | [本机实证-源码] | **REJECT** | §20 | 单兵目标选择+弹药取舍，非 Formation 层职责。 |
| Survival/SurvivalInstinct.cs（逐兵求生） | [本机实证-源码] | **REJECT** | §20 | 逐兵 `HpRatio` 决定退避，越界。其 `CasualtyReformSignal`（编队级重伤脱队统计→REFORM）**概念**可作 §11/§12 反馈参考 → REFERENCE。 |
| Experience/CounterMatrix.cs（被反制降置信换招） | [本机实证-源码] | **REJECT** | §26（绑定固定意图） | 机制（从战场反馈学"被反制→降置信→换招"）与 §12 损失控制精神相容，但**绑定 TacticManual 固定意图枚举**，随 TacticManual REJECT。"反馈学习"概念 → REFERENCE。 |
| Experience/CampaignHabits.cs（跨战役套路记忆落盘） | [本机实证-源码] | **REJECT** | §17/§21 范围冻结 | 跨战役记忆超当前范围；源码 `_habitsEnabled=false` 已自停（循环自证教训）。 |
| ExperienceGlue.cs（F10 反制+跨战役记忆胶水） | [本机实证-源码] | **REJECT** | §21 | 跨战役记忆部分 REJECT；逐场反制部分绑定旧意图枚举 → 概念 REFERENCE。 |
| Intent/OpponentModel.cs（敌意图信念 B[6]） | [本机实证-源码] | **REFERENCE** | §6/§10/§24 | 概念（从可观察编队信号推断敌意图、置信/熵退化）与 §10/§24 相容；但实现绑定旧 `EnemyIntent`/`TacticManual` 计数，且 r2/r3 已将 CB-011 降为"玩家变化检测器"，故全量互建模不在范围。仅作 CB-011 概念参考。 |
| Enums.cs（IntentType 9 战术枚举等） | [本机实证-源码] | **REJECT（部分）** | §26 | `IntentType`（Attack/Defend/.../ProtectArcher/BraceFormation）即专用战术枚举，不沿用；`SoldierKind`/`ArmorGrade` 等工具枚举无害但建议由 2.0 重定义。 |

### D. 治理类（r1 标 REUSE，本次独立复核→下调）

| 资产 | 证据 | 判定 | 宪章依据 | 理由 |
|---|---|---|---|---|
| CPS 控制面（唯一仲裁 + 硬门 GO） | dll 含 `CPS`/`FormationCommandExecutor` 符号 [本机实证-dll符号]；**源码不在机** [本机实证-源码]；派单文档详述 | **REFERENCE** | §16/§19/§20 | 概念（唯一出口、硬门、可审计）正确且为 2.0 必需，但**无源码可复用**，且 2.0 命令层约束（Route A/B、hybrid 短租约、主线程收敛）与旧 CPS 不同 → 必须按 Charter 重新实现为 CB-002。r1"REUSE"不成立。 |
| ATC-0 命令溯源 / ATC-3 FormationMission 八类 / ATC-5 命令门控 / ATC-5R 控制权租约 | 仅派单文档 [r1报告转述]；dll 不含 `ATC` 字符串 [本机实证-dll符号]；源码不在机 | **REFERENCE** | §16/§19 | 纯设计提案，无代码实证。其"控制权租约/命令溯源/许可门控"思路可作 CB-002 设计输入，但不得声称"已验证可复用"。 |

### 资产判定分布（本部分）
- **REUSE（8）**：CombatLog, VecMath, CombatEventBus, TeamGeometry, BufferPool, BattleBenchmark, OfflineTests/CAIRegress, ilscan/CaiHarmony 模板。
- **ADAPT（7）**：CombatAiMissionLogic(宿主), ReplayRecorder, SituationVector, IntentEngine(仅防抖机制), BehaviorLodScheduler(去剧场), TerrainSampler, + 机制类合计。
- **REFERENCE（8）**：CPS, ATC 系列, OpponentModel, CounterMatrix(概念), ExperienceGlue(概念), SurvivalInstinct.CasualtyReformSignal(概念), DecisionObservatory(未列但同属), KnowledgeView/VisibilityOracle 作为"§14 反面教材"。
- **REJECT（14+）**：KnowledgeView, VisibilityOracle, DecisionJitter, TacticManual, CavalryWaveFSM, Combat/*, RangedBrain, SurvivalInstinct, CampaignHabits, IntentType 枚举, 及绑定旧意图的 CounterMatrix/ExperienceGlue 具体实现。

---

## 第三部分：旧版失败教训 → 2.0 强制纪律

| # | 旧教训（r1-cb-legacy §4） | 转化为 2.0 纪律 / 验收条件 | 复发风险 |
|---|---|---|---|
| 1 | 整份下发推倒已验证代码 | 任何新增先对现状映射（工程基建可复用 / 逻辑专用链不复用），禁止"从零重写"假设。 | 低（r2 复用纪律已立） |
| 2 | 测量系统自证为伪（OrderTrace 把内部 Record 误标"原生覆盖"） | **强制独立观测**：帧级 Formation 指令溯源观测器，只读 `Formation` 当前 Order + `IsAIControlled` 边沿，来源判定=CB网关/原生/玩家，不依赖 CB 内部 Record。 | **高**（R-02，S1 前必须定稿） |
| 3 | ATC-4 四个 P0 全为自身感知缺陷（刷新失效/未知敌算已接敌/兵力算两遍/预备队误判） | CB-001/004/005 共用刷新逻辑须独立验证"刷新时效/敌我自反/兵力去重/预备队识别"四项，指标不可自证。 | **高**（R-04） |
| 4 | D 组零 Acquire（实验条件过窄） | 对照实验设计须覆盖真实战场分布，避免"远出且 Denied 的步兵"式空窗；RBM 对比实验条件须广谱。 | 中 |
| 5 | 命令互刷催生冻结令（后写者胜） | CB-002 唯一仲裁 + 硬门；废弃"靠调用顺序表优先级"。 | 低（CPS 概念继承） |
| 6 | 个体层空白却占 80% 观感 | 明确边界：§19-20 不进单兵层，移交原生 AI；该"聪明感缺失"是宪章取舍非缺陷。 | 低（已接受张力） |
| 7 | 架枪过早触发（头号"看起来蠢"） | 属原生行为，非 CB 职责边界；2.0 不碰单兵格挡时机。 | N/A |
| 8 | 范围蔓延（F13–F25 统帅/观战/导播） | §21 范围冻结 + 不继承 T/F/X 代号；任何新系统先过 §21 四问。 | 低 |
| 9 | 规范一致性债（Route A 玩家豁免未读 MCM 开关） | CB-002 硬门统一治理，所有 Formation 命令经同一出口。 | 低 |
| 10 | 文档债（跨 4–6 份文档复写裸"文件:行"） | 单一信息源 = Charter；本报告证据强度列取代裸行号引用。 | 低 |

> 复发高危：#2（指标自证）、#3（感知 bug）——二者须在 S1 前建立独立观测与四项验证，否则重蹈旧版三场数据全废。

---

## 第四部分：RBM 参照（只答三件事，顺序=Charter→需求→Bannerlord能力→设计→RBM仅证据）

**1. RBM 是否遇到类似问题？**
- **给 AI 队下达 Formation 指令**：RBM 用 `Team.AddTacticOption` + `AI.SetBehaviorWeight<T>(w)`（Tactics.cs:222/457-522 实证）向原生 `TeamAIComponent` 注入自定义 `TacticComponent`/`BehaviorComponent` 提权——这正是 2.0 Route A 的同一思路。**RBM 不直接持有 OrderController**，靠权重让引擎"选中"我们，印证 §19 边界可行。
- **避免被原生 AI 覆盖**：RBM 用 Harmony 逐 patch 拦截命令重设（`OnBehaviorActivatedAux` 在 OverrideBehaviorProtectFlank.cs 实证；`aiDecisionCooldownDict` 在 Frontline.cs:909 实证）。这印证 §16 指挥延迟/决策限频的必要性，也佐证 2.0 Route B 若被覆盖时 Plan B（拦 `OnBehaviorActivatedAux`）可行且不违反 §20（拦命令重设≠改单兵定位数学）。
- **战术/行为分层**：RBM 三级（Tactics→Behavior→Frontline 单兵）印证"计划层不算坐标、执行层算相对几何"可行（r1-rbm §2）。2.0 据此落地 §8 Plan/Command 分层。

**2. RBM 用了哪些 Bannerlord API / Hook（本机实证）**

| 证据 | 文件:行 | 说明 |
|---|---|---|
| `SetBehaviorWeight<T>(1.5f)` / `AddTacticOption` | Tactics.cs:222,457-522 [本机实证-源码] | Route A 软注入：提权让原生引擎选中自定义行为 |
| `HasBattleBeenJoined(Formation, bool, 75f)` | Utilities.cs:220 [本机实证-源码] | 接战判定+两阶段切换 |
| `FindSignificantEnemy(formation, includeInfantry, includeRanged, …)` | Utilities.cs:979 [本机实证-源码] | 目标"有意义化"：大编队优先，不追零散小股 |
| `GetOrderPositionOfUnit` / `GetDirectionOfUnit` Prefix | Frontline.cs:231/159 [本机实证-源码] | **逐兵**改阵位/朝向（单兵层，§20 禁止照搬） |
| `aiDecisionCooldownDict` | Frontline.cs:909 [本机实证-源码] | 单兵决策冷却（印证 §16 限频，但放错层） |
| `OnBehaviorActivatedAux` patch | OverrideBehaviorProtectFlank.cs [本机实证-源码] | 拦命令重设（Plan B 证据） |
| 12 个 OverrideBehavior* + 7 个 RBMBehavior* + 6 个 RBMTactic* | `ls` 实证 [本机实证-源码] | 专用行为类堆积（§26 反面） |

**3. 哪些经验降风险 / 哪些绝不能照抄（按宪章）**

可降低技术风险：
- Route A 软注入 + `SetBehaviorWeight` 提权模式 → 直接支撑 2.0 CB-002 Route A（§19）。
- Harmony 拦 `OnBehaviorActivatedAux` 解决 Route B 覆盖 → Plan B 已备（r3 R-01），且**不违反 §20**（只拦命令重设）。
- "目标有意义化"（FindSignificantEnemy）→ 2.0 CB-005 取"通用价值评价"思想，非写死兵种过滤（§26）。
- 克制行为集自然流露意图（RBMBehaviorEmbolon 楔形可见）→ 印证 §22"玩家能识别整体意图"，2.0 更进一层加显式 `objective`。

**绝不能照抄（宪章条款）**：
- **§20 单兵层**：`Frontline.cs` 逐兵改 `GetOrderPositionOfUnit`/`GetDirectionOfUnit`、`OverrideAgent` 改架枪、`StanceLogic.cs`+`PostureDamage.cs` 逐兵姿态↔攻防数值联动 → 全越界，且 §3/§25 要求胜负来自指挥博弈而非个体数值。
- **§26 专用规则链**：7 RBMBehavior + 13 OverrideBehavior + 6 RBMTactic 按文化/兵种/场景写死 → 恰是宪章禁止的"大量独立战术模块"。
- **§17 个性暂缓**：`Tactics.cs` 按文化定制战术表（TacticDefensiveEngagementPatch 等）→  commander 个性系统雏形，当前暂缓。
- **§3/§25 不靠数值优势**：`StanceLogic` 个体数值联动即便非隐藏，也违背"胜负来自双方指挥博弈"。

> 顺序纪律：本设计 Charter→CB-00X 功能→Bannerlord 能力（Route A/B、公开 QuerySystem）→2.0 设计（CB-002/005/006）→**RBM 仅作证据**（验 Route A 可行、Plan B 可行、回避专用链）。RBM 从未成为需求源。

---

## 审计结论（回报，≤200 字）

**判定分布**：REUSE 8（CombatLog/VecMath/CombatEventBus/TeamGeometry/BufferPool/BattleBenchmark/离线测试/ilscan）；ADAPT 7（宿主、ReplayRecorder、SituationVector、IntentEngine 防抖、BehaviorLodScheduler、TerrainSampler 等机制）；REFERENCE 8（CPS、ATC 系列、OpponentModel、CounterMatrix/ExperienceGlue/CasualtyReformSignal 概念、DecisionObservatory、迷雾类作反面教材）；REJECT 14+（KnowledgeView、VisibilityOracle、DecisionJitter、TacticManual、CavalryWaveFSM、Combat/*、RangedBrain、SurvivalInstinct、CampaignHabits、IntentType 枚举等）。

**最需注意 3 条风险**：
1. **CPS/ATC 源码不在机**（仅 dll 符号）→ r1"REUSE 治理层"不成立，2.0 须按 Charter 重建 CB-002，不得假设"已有可复用控制面"。
2. **指标自证为伪**（旧 OrderTrace 教训）→ S1 前必须建独立帧级指令溯源观测器，否则验收无效。
3. **感知 bug 重演**（ATC-4 四 P0）→ CB-001/004/005 须独立验证四项刷新要件，指标不可自证。
