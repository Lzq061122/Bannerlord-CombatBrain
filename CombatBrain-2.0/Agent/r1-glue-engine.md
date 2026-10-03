# R1 — CombatBrain 2.0 引擎能力胶水报告（"游戏到底给不给我们权限"）

> 胶水设计师产出。所有 API 名来自本机反编译源码 / RBM / RTSCamera / TAOM 文档 / 既有 CombatBrain 研究（v1.4.x 反编译）。
> 版本提示：枚举**整数值**随版本漂移，命名常量（`MovementOrderType.Charge` 等）稳定；标「需实测」者为未逐字核验签名。

## 结论先行（最关键的一条）
**AI 队伍没有、也不拥有 `OrderController`。** 全引擎唯一的发令接口是 `Team.PlayerOrderController`（仅玩家队，RTSCamera/IB 均只在此用）。AI 队的 Formation 指令只能走两条路：
- **Route A（软注入）**：把自定义 `BehaviorComponent`/`TacticComponent` 塞进原生 `TeamAIComponent` 决策管线（`Team.AddTacticOption` / `FormationAI.AddAiBehavior` / `SetBehaviorWeight`），让引擎自己"选中"我们；
- **Route B（硬接管）**：`Formation.SetControlledByAI(false)` 关掉原生 AI，再在主线程经 `Formation.Set*Order` 直接写命令。
玩家式 `OrderController.SelectFormation/SetOrderWithFormation` 路径**对 AI 队完全不可用**。这是草案能否落地的第一性约束。

---

## §1 引擎能力矩阵表

| 机制 | 真实 API / 类 | 可用于 AI 队伍 | 宪章条款 | 限制 |
|---|---|---|---|---|
| 玩家发令 | `Team.PlayerOrderController`（OrderController） | **否（仅玩家队）** | — | 唯一 OrderController，AI 队无 |
| AI 队移动/冲锋/阵型 | `Formation.SetMovementOrder`/`SetFacingOrder`/`SetArrangementOrder`/`SetFormOrder`/`SetFiringOrder` | 是（直接写编队） | §8/§9/§19 | 会被原生 Behavior 激活覆盖（§3） |
| 战术层注入 | `TeamAIComponent`/`TacticComponent`/`BehaviorComponent`、`Team.AddTacticOption`、`FormationAI.AddAiBehavior`、`SetBehaviorWeight`、自定义 `GetAiWeight`/`CalculateCurrentOrder` | 是（软，仅 Movement 域） | §7/§19 | Facing/Arrangement 不经 Behavior |
| 编队信息只读 | `Formation.QuerySystem`（含 `ClosestSignificantlyLargeEnemyFormation` 等敌我查询） | 是 | §5/§6/§14 | 公开查询，RTS 等价；局部 O(兵数) |
| 编队属性 | `CountOfUnits`、`Width`、`Direction`/`CurrentDirection`、`CurrentPosition`/`OrderPosition`、`CachedFormationIntegrityData` | 是 | §5/§6 | 公开属性 |
| 友敌判定 | `Team.IsEnemyOf`/`IsFriendOf`（委托原生 `MBTeam`） | 是 | §6 | `IsFriendOf(team)==true` 自反，需自判同队 |
| 地形高度 | `Scene.GetGroundHeightAtPosition(Vec3, BodyFlags=CommonCollisionExcludeFlags)`、`GetGroundHeightAndBodyFlagsAtPosition`（out BodyFlags） | 是（双方同权） | §9/§13 | 经 `Mission.Scene`；低开销 |
| 可行走性/地形类型 | `BodyFlags`（上述 out 参数）+ NavMesh 查询 | 部分（**需实测**） | §9 | 面类型细节未逐字核验 |
| 时序/计时 | `MissionBehavior.OnMissionTick(dt)`（主线程）、`Mission.Current.CurrentTime`(float 秒)、`MissionTimer`(TaleWorlds.Library) | 是 | §16 | 决策循环挂主线程 |
| 硬接管编队 AI | `Formation.SetControlledByAI(bool)`、`Formation.IsAIControlled{get}` | 是 | §19 | 关闭后原生不再写 Movement；Facing/Arrangement 覆盖风险（§3） |
| 编队事件挂钩 | `Formation.OnAfterArrangementOrderApplied`、`Team.OnFormationAIActiveBehaviorChanged` | 是 | §11/§13 | 仅观察，不写 |

---

## §2 权限边界专项：AI 队下达 Formation 指令的路径排序

1. **首选 — Route A（软注入原生管线）**
   - 依据：自定义 `BehaviorComponent`（继承 `BehaviorComponent`）经 `FormationAI.AddAiBehavior` + `SetBehaviorWeight` 或 `Team.AddTacticOption` 注入；`GetAiWeight` 提权让原生 GOB 选中我们。无"抢方向盘"，与玩家接管天然兼容（AI 停即不写）。
   - 风险：Behavior **只表达 Movement 域**（Facing/Arrangement 不走 Behavior）；与 RBM 等同层做权重竞争；无法硬排他（仅降权）。

2. **备选 — Route B（硬接管）**
   - 依据：`Formation.SetControlledByAI(false)` 令原生 `FormationAI.TickOccasionally` 对该编队停止写 Movement，自己经 `OnMissionTick` 直接写四域命令（移动/朝向/阵型/开火）。三域都能管、控制确定。
   - 风险：① 命令写入须在主线程，与**异步 AI tick（worker 线程 `doAsyncAITick→BehaviorXxx.TickOccasionally`）** 竞态（见 §3）；② 玩家 F6 委托/阵亡接管会改变 `IsAIControlled`/`OrderController.Owner`，须监听并立即归还；③ ATC-5R 实测提示：`SetControlledByAI(false)` 后原生 Facing/Arrangement **仍可能持续覆盖**——需实测确认，必要时用 Harmony 拦 `OnBehaviorActivatedAux`（注意项目红线）；④ 关闭期间原生跨编队协调（支援邻居等）丧失。

3. **兜底 — 窄保险丝（hybrid）**
   - 平时 Route A 表达意图；仅在"关键安全命令"用 Route B **短租约**（TTL 1.25–1.5s、最短持有 1.0s、`watchdog` 到期 fail-open 自动归还，玩家接管立即释放且只归还"自己借来的"）。既保意图软表达，又对关键命令保排他。

---

## §3 指令被原生 AI 覆盖的风险清单 + 规避

| 风险 | 证据 | 规避手段 |
|---|---|---|
| 原生 Behavior 激活即重设全命令（`OnBehaviorActivatedAux` 写 SetMovementOrder+Facing+Arrangement(Line)+Firing+FormOrder） | h4 报告铁证；RBM 逐个 patch 这些 OnBehaviorActivatedAux | Route A 在 Behavior 内表达；或 Route B 关 AI；或 Harmony 拦激活（红线慎用） |
| 异步 AI tick 竞态 | `doAsyncAITick→TickOccasionally→SetMovementOrder` 跑 worker 线程 | 命令写入统一收敛到主线程 `OnMissionTick`；不在 worker 线程读改写编队状态；若须 patch 加锁/按 Team 过滤 |
| GOB 权重切换覆盖（PreserveExpireTime=10f、BehaviorPreserveTime=5f） | FormationAI 权重制 | 持续维持权重或续租约，避免 ActiveBehavior 变更 |
| `SetArrangementOrder` 节流（`ResetArrangementOrderTickTimer`=0.5s，仅 Scatter 触发重排） | Formation.cs | 意图变化才调用，禁每 tick 冲刷 |
| `_MT` 后缀多线程方法（如 `CreateNewOrderWorldPositionMT`） | TAOM 线程提示 | patch 这些方法须 `TWSharedMutexReadLock` 保护 |
| 玩家接管/阵亡改变控制权 | RTSCamera 置 `PlayerOrderController.Owner` 空防 AI 托管 | 监听 `OnFormationAIActiveBehaviorChanged`、检测 `IsAIControlled` 边沿，及时释放租约 |

---

## §4 "决策节奏 + 指挥延迟"的引擎手段（§16）

- **主循环**：自建 `MissionBehavior` 挂 `OnMissionTick(dt)`，主线程每帧触发，作为决策与命令落地的唯一出口。
- **计时原语**：`Mission.Current.CurrentTime`（float 秒）做时间戳累加；`MissionTimer`（TaleWorlds.Library，支持 `Reset`/`Check` 到期）做命令延迟与冷却队列（IB 即用 `ConcurrentDictionary<Guid,IB_CoolTime>`+`MissionTimer` 限流 0.8–1.5s，可作样板）。
- **决策节奏**：用累积时间门控——高层规划每 N 秒一次，非每微小变化即重规划（满足 §16「Decision Cadence」）。
- **命令生成/传递延迟 + Formation 响应前时间差**：命令先入队，`MissionTimer` 延时落地；落地后等原生定位/Behavior 生效，各编队错峰响应，形成「Command Latency」。
- **约束**：不要在 `OnMissionTick` 每帧调 `SetArrangementOrder`（性能 O(n)+节流）；意图变化才落命令。

---

## §5 明确的「不能做」清单（游戏不给途径）+ 替代

1. **不能给 AI 队用 OrderController 选编队/发令**——无此接口。替代：Route A/B 直接写 `Formation.Set*Order`。
2. **不能获得"玩家原则上看不到"的隐藏信息**——宪章 §14 本就禁止，且引擎本就公开。替代：用 `Formation.QuerySystem`/`Team` 公开查询，与 RTS 视角等价。
3. **不能改单兵攻击/格挡/目标选择**——宪章 §20 边界 + 单兵行为在 native `AutoGenerated.dll`。替代：只控 Formation 级，把意图交给原生 Agent AI。
4. **不能改原生每帧 agent 定位/碰撞数学**（native）。替代：经阵型几何与命令间接影响。
5. **不能"裸调 SetArrangementOrder 且不被覆盖"**——被原生 Behavior 覆盖。替代：Route A/B（§3）。
6. **不能从 worker/async AI tick 线程安全改编队命令**——竞态。替代：主线程 `OnMissionTick` 写。
7. **不能依赖原生"视野外不触发"机制**——原生无剧场效应。替代：自实现距离闸（`MainAgent` 距离 / L2+ 编队跳过刷新）。

---

## §6 给总设计师的 5 条落地建议（按优先级）

1. **架构必须接受「AI 队无 OrderController」**：所有 Formation 指令一律走 Route A/B 写 `Formation.Set*Order`，彻底放弃复用玩家 `OrderController` 路径的幻想。
2. **命令读写收敛到主线程 `OnMissionTick`**：绝不在异步 AI tick / worker 线程路径触碰编队命令状态，避免竞态崩溃（既有 CB 项目已验证此为安全基线）。
3. **决策节奏与指挥延迟用 `MissionTimer`+`CurrentTime` 时间门控实现**（§16）：意图变化才落命令，禁止每帧冲刷触发原生 0.5s 节流。
4. **采用 hybrid：Route A 主路表达意图，Route B 仅作窄保险丝**（关键安全命令短租约、fail-open 自动归还、玩家接管立即释放）——兼顾原生兼容与关键排他。
5. **§14 信息公平落地为「读公开 QuerySystem」**：敌情已 RTS 等价可得，无需也不应造隐藏信息；同时自实现剧场效应距离闸（原生无此机制），并监控 `IsAIControlled` 边沿以安全释放租约。
