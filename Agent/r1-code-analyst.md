# CombatBrain 2.0 代码资产盘点报告（R1-代码分析师）

> 结论先行：**原 CombatBrain（combatbrain-d-refactor）整体是「单兵层 + 专用战术规则链」范式，与新宪章第 19/20/26 条根本冲突，不可直接复用**。可复用的几乎全是**工程基建（日志/数学/事件总线/几何/对象池/离线测试/Harmony 模板）**，以及**架构骨架的「机制」而非「逻辑」**（态势向量数据模型、意图防抖=计划最小稳定时间、指挥队列=指挥延迟）。CAI 的战役层基建与 ilscan 工具可直接借力。

---

## §1 资产清单表

证据等级：**[读]**=已读代码确认；**[档]**=文档/指南声称；**[未]**=未验证仅按文件名与引用推断。

| 路径 | 文件/类 | 原始职责 | 可复用性 | 判断依据(宪章) | 复用方式建议 |
|---|---|---|---|---|---|
| CombatBrain/CombatLog.cs **[读]** | CombatLog | 缓冲异步落盘日志 | **高** | 第3/14条(无隐藏信息,纯基建) | 直接搬入2.0 |
| CombatBrain/VecMath.cs **[读]** | VecMath | 距离/Clamp/对数映射 | **高** | 通用数学 | 直接搬入 |
| CombatBrain/Events/CombatEventBus.cs **[读]** | CombatEventBus | 事件总线 | **高** | 第13/24条(反馈机制基建) | 复用为反馈总线 |
| CombatBrain/Ai/TeamGeometry.cs **[读]** | TeamGeometry | 编队/队中心聚合 | **高** | 第5/6条(己方/敌方位置认知) | 直接复用(只聚合编队级) |
| CombatBrain/Ai/BufferPool.cs **[读]** | BufferPool | [ThreadStatic]对象池 | **高** | 性能基建 | 直接搬入 |
| CombatBrain/Bench/BattleBenchmark.cs **[读-头]** | BattleBenchmark | 帧耗时分节基准 | **中** | 遥测基建 | 复用性能红线监测 |
| CombatBrain/Events/ReplayRecorder.cs **[未]** | ReplayRecorder | 事件回放录制 | **中** | 第13条(观察战果) | 借鉴做决策回放 |
| CombatBrain/Intent/SituationVector.cs **[读]** | SituationVector | 12维归一化战场向量 | **中** | 第26条(共享战场状态表示) | 复用数据骨架,重定义为通用战场状态 |
| CombatBrain/Intent/IntentEngine.cs **[读]** | IntentEngine | 加权打分选意图+防抖 | **中** | 第16条(仅"防抖/最小稳定时间"机制可留) | 只借鉴hold_time/switch_margin机制 |
| CombatBrain/Lod/BehaviorLodScheduler.cs **[读]** | BehaviorLodScheduler | 按焦点/距离分LOD+决策步长 | **中** | 第16条(Decision Cadence) | 借阈值模型做指挥节奏,但去"剧场效应" |
| CombatBrain/Ai/CombatAiMissionLogic.cs **[读]** | CombatAiMissionLogic | MissionBehavior宿主/低频tick | **中** | 第19条(Formation层挂载点) | 复用宿主骨架与Mission API用法,剔除内部单兵逻辑 |
| CombatBrain/Perception/TerrainSampler.cs **[未]** | TerrainSampler | 开战一次性地形预缓存 | **中** | 第6条(地形站位认知) | 复用地形查表(去掉迷雾包装) |
| CombatBrain/Intent/OpponentModel.cs **[未]** | OpponentModel | 敌方意图信念 | **低** | 第6/10条(敌情/试探) | 仅概念参考,原模型绑旧意图 |
| CombatBrain/Enums.cs **[读]** | IntentType等 | 战术枚举(含ProtectArcher/Brace) | **低** | 第26条(专用情形) | 不沿用,改§23任务型枚举 |

---

## §2 明确不能复用的部分（对照第20/21/26条）

- **单兵行为层（违反第20条）**：`Combat/AttackStats.cs`、`Combat/PredictiveBlock.cs`、`Combat/FighterProfile.cs`**[读]**、`Ranged/RangedBrain.cs`**[读]**(单兵选靶/弹药)、`Survival/SurvivalInstinct.cs`**[未]**(求生)、`Ai/*Glue`中的 Movement/Survival/Ranged 个体分支。宪章19/20明确不进入单兵层。
- **专用情形规则链（违反第26条）**：`Intent/TacticManual.cs`**[读]**(9意图×12维权重+ProtectArcher/BraceFormation)、`Intent/IntentEngine.cs`**[读]**(固定战术加权)、`Cavalry/CavalryWaveFSM.cs`**[读]**(骑兵专用波次FSM)、`Ai/ProtectGlue.cs`/`BraceGlue.cs`**[档]**(专用执行层)。宪章26要求优先通用机制而非专用链。
- **跨战役经验/个性（违反第17/21条）**：`Experience/CounterMatrix.cs`、`Experience/CampaignHabits.cs`**[未]**——超出当前范围冻结。
- **旧测试指南所称95条断言（F6–F12）**[档]**——覆盖的正是上述单兵/专用战术，对2.0无验收价值，仅保留其"离线纯逻辑回归"这一**工程方法**。

---

## §3 可复用的工程基建（接入成本）

| 基建 | 来源 | 接入成本 | 说明 |
|---|---|---|---|
| 缓冲日志+遥测 | CombatLog / BattleBenchmark / DecisionObservatory **[读/未]** | 低(复制) | 直接复用 |
| 数学/几何/对象池 | VecMath / TeamGeometry / BufferPool **[读]** | 低 | 直接搬 |
| 事件总线 | CombatEventBus **[读]** | 低 | 作反馈中枢 |
| Harmony挂载模板 | CAI/Engine/CaiHarmony.cs + CaiSubModule.cs **[读]** | 低–中 | 战斗层用MissionBehavior而非劫持原生思考,但降级/异常保护模式可抄 |
| 配置/存档模式 | CAI/Core/CaiDataStore·SaveCoordinator·SaveDto **[未]** | 中 | 战斗内不需存盘,仅借鉴配置表结构 |
| 离线纯逻辑回归 | CAI/tests/CAIRegress/Program.cs **[读头]** + 原OfflineTests/Program.cs **[档]** | 低(直接建同款工程) |  Charter要求离线测试,最该直接复用 |
| IL原生API扫描 | CAI/.tools/ilscan/ilscan.cs **[读]** | 低(已可运行) | 查Formation/Mission原生下发API |
| 参考模组反编译 | IDEA/_mod_research/* **[未]** | 仅参考 | 不同Mod、反编译,不能直接复用代码,仅用于查原生API用法 |

---

## §4 与新宪章冲突的旧设计

| 文件路径 | 冲突点 | 违反宪章 |
|---|---|---|
| Perception/KnowledgeView.cs **[读]** | "迷雾知识视图":置信衰减+位置噪声,主动让AI变瞎 | **第14条**(信息公平,AI应得完整指挥视角信息) |
| Perception/VisibilityOracle.cs **[读]** | O(格)可见性判定+地形遮挡 | **第14条**(不构建视野遮挡/未知区域) |
| Compliance/DecisionJitter.cs **[读]** | 拟人迟疑/犹豫/走形(疲劳 morale wobble) | **第15条**(不主动制造错误)+第16条(此为agent层而非Formation指挥延迟) |
| Intent/TacticManual.cs **[读]** | 权重手册固化9种战术含专用ProtectArcher/Brace | **第26条**(专用情形规则链) |
| Intent/IntentEngine.cs **[读]** | 加权打分选固定战术 | **第26条** |
| Combat/*、Ranged/RangedBrain、Survival/* **[读/未]** | 单兵格挡/走位/选靶/求生 | **第20条** |
| Cavalry/CavalryWaveFSM.cs **[读]** | 骑兵专用多波次FSM | **第26条**+**第23条**(兵种独立指挥系统) |
| Ai/ProtectGlue·BraceGlue·ExperienceGlue **[档]** | 专用意图执行层 | **第26条** |

---

## §5 实现难度分级参考（以"从零写 Formation 层指令下发模块"为例）

**依赖的既有基建（低成本）**：CombatLog、VecMath、BufferPool、TeamGeometry、CombatEventBus、BehaviorLodScheduler(节奏阈值)、CAI离线测试框架、ilscan(查原生API)。

**需新增/改写的2.0文件（按难度）**：

- **低（复用为主）**：`SubModule.cs`/`MissionBehavior`宿主——抄 CombatAiMissionLogic 骨架与 Mission/Team/Formation API 用法；`BattleState`——基于 SituationVector 骨架重定义为「己方/敌方/战场」三类通用状态（去迷雾、去单兵字段）。
- **中（机制借鉴+改造）**：`CadenceController`——借 IntentEngine 的 hold_time/switch_margin 实现「决策节奏+计划最小稳定时间」（宪章16），但语义从"选战术"改为"计划稳定"；`PerceptionAdapter`——借 TerrainSampler 做地形/站位认知，剔除 KnowledgeView/VisibilityOracle 迷雾。
- **高（核心新架构,无现成可抄）**：`PlanBuilder`（作战计划语义,宪章7/8）、`TaskAssigner`（向Formation分配保护/输出/牵制/突破/支援/预备/扩大战果,宪章23,取代TacticManual加权）、`CommandDispatcher`+`CommandLatencyQueue`（指令下发+指挥传导延迟,宪章16,取代per-agent block）、`FeedbackReplanner`（正向/负向反馈→扩大优势/控制损失→重规划,宪章11/12/13）。
- **最高（博弈智能）**：试探/诱导敌方弱点（宪章10）、敌我意图互建模（宪章6/10）——OpponentModel 仅概念参考。

> 一句话：原代码的**「代码」不可复用,但其「工程脚手架与几个机制雏形」可省去2.0约30–40%的基建工期**；真正的指挥智能（Plan/Task/Feedback）必须从零按宪章设计,不抄旧战术逻辑。
