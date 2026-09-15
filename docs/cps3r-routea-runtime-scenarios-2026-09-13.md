# CPS-3R Route A 运行期验证记录（2026-09-13）

> 性质：**DESCRIPTIVE scoped claims**（运行期证据）。不改写 Normative；离线绿 ≠ 运行期绿。
> 现行验证态唯一权威 = `docs/knowledge/CURRENT_STATE.md`；本文件只登记**已完成场次**的证据与其限定。
> 关联返修：CB-REPAIR-0001 / CB-REPAIR-0002 / CB-REPAIR-0003（commit `70c9a11` / `a219c5b` / `9a2afe4`，均已 COMPLETED）。
> 部署树：`9a2afe4` 代码（`f6634f7` 仅 `测试指南.md` 文档，无代码变更）。

---

## 1. 采样前置

- MCM：`Cps3Mode=E_RouteA` + **`Cps3ObserverEnabled=true`**（G-1 硬约束：`RouteAExecutionEnabled()` 含 `&& ObserverEnabled`，CB-REPAIR-0003；关观测时 Route A 不执行且零 `[RouteA]` 行）
- 日志：`%TEMP%\CombatBrain.log`，**多场混写**，须按 `run=` 过滤
- 字段命名：汇总字段为 **`applied=`**（Astra 提交物 §11 写作 `orderApplied=`，属文档/代码命名漂移）；逐事件行为 **`event=OrderApplied`**（无空格）
- 汇总节奏：每 5 秒一条，值不变时重复打印同一行

## 2. 场景 1（E_RouteA 采样，200v200 步兵野战）：重跑 PASS（对照修复前 FAIL）

| 项 | 修复前 `run=4c6212a1…`（09-10 13:13，修复前 DLL） | 修复后 `run=ba84a11b…`（09-10 13:53，含 CB-REPAIR-0001/0002） |
|---|---|---|
| 观测挂载 | —（旧构建未含 G-1 约束） | 13:53:12 `[SetOrderObserver] 观测已挂载 prefixes=3` |
| 落地序列 | `applied=3` | 6× `OrderApplied`：t=4.53 Hold / 152.53 Advance / 175.03 Hold / 179.03 Advance / 192.03 Hold / 198.03 Advance |
| 中途外部覆盖 | t=410.53 `ExternalReplaced`（外部 orderType=5） | **t=245.53 `ExternalReplaced`（外部 orderType=5）** |
| 覆盖后行为 | **直到 t=452.89 战斗结束未再落地**（缺陷） | **t=250.54 `EdgeRetry` → `OrderApplied`（5.00s 内重落地一次）** |
| 退出 | t=452.89 `Exit(EmptyOrInvalid)` + `Restore` | t=265.76 `Exit(EmptyOrInvalid)` + `Restore` |
| 汇总 | `applied=3 replaced=1 stableSoFar=2`（**无** externalWhileActive/edgeRetries/externalDuration 字段 = 修复前构建） | `applied=7 replaced=1 externalWhileActive=12 edgeRetries=1 externalDuration=247.913s stableSoFar=6 unverified=0 dropped=0 intentChange=5 weightFlap=0 restoreFailures=0` |

判定：**原缺陷（被外部覆盖后长时间不重新落地）未复现**；「首次 `replaced=true` → 单次边沿重试」路径获得运行期样本；新证据字段在运行期真实产出。

## 3. 场景 2（战斗中热关闭 RouteA）：PASS

`run=a408fc92…`（09-13 01:11–01:17，战斗时长 362.85s）

- 01:11:07 `[SetOrderObserver] 观测已挂载 prefixes=3`（G-1 前置满足）
- RouteA 执行：6× `OrderApplied`（t=4.53 Hold / 152.53 Advance / 175.03 Hold / 179.03 Advance / 192.03 Hold / 198.03 Advance），409× `BehaviorSelected`
- 用户于战斗中主动关闭：**t=208.9818（全场 58% 处）`event=Exit reason=Disabled` + `event=Restore reason=Disabled`**
- 关闭后：344× `ExternalAfterExit`（RouteA 已退出，原版正常写入），**无任何 `OrderApplied`**（未重新武装、未抢回）
- 汇总：`applied=6 replaced=0 externalWhileActive=3 edgeRetries=0 externalDuration=0.000s stableSoFar=6 unverified=0 dropped=0 intentChange=5 weightFlap=0 restoreFailures=0`

判定：热关闭 → 立即 `Exit(Disabled)` + 权重恢复，`restoreFailures=0`；关闭后所有权彻底释放，原版接管正常。**通过**。

限定：该场 3 次 `externalWhileActive` 全部落在同一 tick（t=4.390940），故 `externalDuration=0.000s`；本场 `replaced=0`，即**未发生外部覆盖**，不能用于验收覆盖恢复路径。

## 3a. 场景 3（玩家接管）：**N/A —— 对 Route A 结构上不可执行**（用户裁决 2026-09-13）

原清单（Astra 提交物 §6 / 收工报告 §11）写作「玩家选中该编队手动下令 → CB 1 拍停、不抢回」。该条继承自 **ATC-5R 租约（路线 B）** 的验证计划，**不适用于 Route A**：

1. **候选枚举无条件排除玩家队**（`CombatBrain/Ai/CombatAiMissionLogic.cs:2387`）：
   `if (team == null || team.IsPlayerTeam || !team.MBTeam.IsValid) continue;`
2. **资格判定同样无条件**（`CombatBrain/Ai/RouteAOrderAdapter.cs:141-142`）→ `RouteAExit.PlayerOwned`：
   `f.Team.IsPlayerTeam || f.PlayerOwner != null || f.HasPlayerControlledTroop || f.IsPlayerTroopInFormation`
   叠加 `:140 e.AiControlled = f.IsAIControlled`（不满足 → `LostAIControl`）。

∴ Route A 只能武装「非玩家队 + 无领主所有者 + 无玩家兵员 + AI 控制」的编队（实测为 `team=1`）；而玩家在 UI 上无法对非玩家队编队下令 → 该动作在**本次采样口径下不适用**。**不得把场景 3 记为 FAIL，也不得记为 PASS**。

> 口径限定（Astra r3 派单 §5）：**玩家豁免属产品/规范裁决项**。本文件只陈述上述代码事实，不就其是否应永久如此下结论，也不借返修卡顺手修改实现。

**替代的可达路径（覆盖原意图「外部接管时 Route A 不得抢回」）**：

| 替代项 | 状态 |
|---|---|
| 外部 setter 覆盖当前命令 | 已有运行期样本（场景 1：`ExternalReplaced → EdgeRetry → OrderApplied`） |
| 编队失效 / 清空 | 已观察（`Exit(EmptyOrInvalid)`，场景 1、2） |
| 模式 / 开关关闭 | 已通过（`Exit(Disabled)` + `Restore`，场景 2） |
| **玩家队豁免生效（新增验收点）** | 待跑：整场日志中**玩家队编队的 `[RouteA]` 行恒为 0**，且 Route A 武装对象的 `identity` **永不属于玩家队** |

**附带记录（口径分歧，冻结令下不改代码）**：其它 7 个模块（Protect / Brace / Cavalry / Skirmish / Morale / FormationSlot / FormationControlLease）的玩家队豁免均受 MCM 开关 `CombatBrainSettings.PlayerTeamExemptionEnabled` 约束（可关闭），**Route A 完全不看该开关**（上列两处均无条件）。属 Descriptive 观察，是否收敛口径待 CPS-4 / 后续裁决，本文件不作规范结论。

## 3b. 场景 4（Rejoin）：**本次采样不适用；该分支仍未验证**（用户 2026-09-13 裁决 + Astra r3 派单口径修正）

**代码条件**（Rejoin 只有一条产生路径）：
1. `FormationCommandPolicy.cs:156-157`：仅当 `formationMission == SupportMainEffort` **且** `phase == Deployment` 才映射为 Rejoin（其余映射 Advance/Hold）
2. `RouteAOrderAdapter.cs:224-241`：MainBody 有效（`strength>0 && confidence>0`）+ **非贴身接敌**（已知敌编队 >12m，`RouteARejoinContactMeters`）+ MainBody 点在边界内 + 起终点 NavMesh 有效且**路径距离可算**
3. `RouteAControl.cs:100`：已选 Rejoin 但校验失败 → `Exit(NavigationFailed)`，不落地任何命令

**实测（本次采样不适用的判定依据）**：
- 三场 E 模式（`4c6212a1` / `ba84a11b` / `a408fc92`）：`Rejoin` 意图 **0 次**、落地 **0 次**、`NavigationFailed` **0 次**
- 全日志 ~35 场：`phase=Deployment` 均由游戏自动结束，时长 **5–20s**（绝大多数 5–6s，最长 21s）——**玩家停等无法有效延长该窗口**（实测两处 20–21s 与 5–6s 并存，与是否等待无稳定相关）
- 被武装编队在部署期内恒为 `mission=Reserve`（`ReserveClassifier`：未接敌 + 非主动任务 + 距 MainBody ≤30m → Reserve → 意图 Hold）：实测 `13:53:13.777`、`01:11:08.778` 两个部署窗口内均为 `Reserve`
- ∴ 上述采样中 `SupportMainEffort` 样本（全部出现在 Engage）与 Deployment 窗口**时间上不重叠**

> **2026-09-14 更新（见 §3j）**：Rejoin 分支**已取得 1 次运行期样本**——混编大场面（Team 1 f=0 = `SupportMainEffort`）在**部署期**满足了 `SupportMainEffort + Deployment` 映射，`Intent(Rejoin) → OrderApplied(ot=1)` 成功落地、`NavigationFailed=0`。即上文"三场 E 模式 Rejoin 为 0 次"的结论**只对当时的 200v200 步兵采样配置成立**，不是结构性不可达；**仍缺**的是 `NavigationFailed` 失败路径与更多次成功样本。

**结论（口径按 Astra r3 派单 §5 修正）**：Rejoin 在**本次采样配置下未取得样本**，故本次采样场景**不适用**；**该分支仍未验证，不得声称代码结构上永久不可达**（本文件此前"实战不可达"的表述已按此修正）。原清单该条（Astra 提交物 §6「Rejoin 触发场景：目标=MainBody、导航失败路径不执行」）仍属**未验证项**，需后续以专门配置或观测手段补验。

**替代可达路径（覆盖"目标/MainBody/导航校验"的相邻语义）**：

| 替代项 | 状态 |
|---|---|
| Route A 意图→命令落地链（Hold/Advance）与 MainBody 态势 | 已有样本（场景 1、2） |
| 编队清空/失效退出 | 已观察（`Exit(EmptyOrInvalid)`） |
| 开关/模式关闭退出 | 已通过（`Exit(Disabled)`，场景 2） |
| 增援 / 对象身份隔离 | 待跑（场景 5） |
| Rejoin 分支本身 | **无运行期样本**；若日后需验证，须先具备可观测性或调整触发条件（代码变更，冻结令下需先裁决） |

**工具**：`MBmod/CombatAI/tools/routea-rejoin-check.ps1`（只读日志，一条命令复现上述判定：部署阶段区间、mission 时间线、条件重叠判定、Rejoin/NavigationFailed 计数与结论）。

## 3c. 场景 5（增援 / 编队清空 / 拆分）：**PASS（带限定）** —— run `b0e2e0a4…`（2026-09-14 00:01–00:17）

采样条件：`BattleSize=6`、`ReinforcementWaveCount=3`；本 run 跨 3 个 mission（session 至 4）；**采样期间使用游戏快进**（第 3 场 `BattleDuration=1268.79s` 游戏时间 / 墙钟约 6.5 分钟 ≈ 3.2×）。

| 判定项 | 实测 |
|---|---|
| 退出 + 恢复配对 | 6 次 `Exit` **全部有配对 `Restore`**：`LostAIControl`×3（04:21.908 / 17:20.681 / 17:42.298）、`MissionEnding`×2（02:34.958 / 09:35.913）、`EmptyOrInvalid`×1（17:48.333） |
| 恢复无失败 / 无对刷 | 汇总 `restoreFailures=0`、`weightFlap=0` ✓ |
| 重新接管（幂等重挂载） | 每次 Exit 之后均重新 `OrderApplied`（如 17:33.255、17:44.308，紧随 17:20.681 / 17:42.298 的 LostAIControl）✓ |
| 对象身份 | **`identity` 始终 = 1** → 同一 `Formation` 对象被反复补员重用；**"新对象身份隔离"分支无样本**（对象未销毁重建） |
| 增援是否真的到场 | 是：`[Bench] agents` 在 100–200 间反复跳变（129→174→119→155→190…），波次持续补充；其中两次与 `Exit(LostAIControl)` 同时段发生 → 增援引起的生命周期事件被正确释放并再武装 |
| 采样完整性 | `session=4 identity=1 … selected=699 applied=3 replaced=1 externalWhileActive=1780 edgeRetries=0 externalDuration=1101.900s stableSoFar=2 unverified=0 dropped=0 intentChange=0 weightFlap=0 restoreFailures=0` |

**通过结论（限定三条）**：① 仅覆盖"同一对象重挂载"分支，新对象隔离未验；② 采样含游戏快进，故 **响应延迟不构成证据**（只证明"发生且正确"）；③ `LostAIControl` 的触发机制（增援/编队更新瞬间 `IsAIControlled` 翻转）为观察推断，未作定论。

**子项「拆分」→ N/A**：Route A 只武装非玩家队的 AI 编队，中途拆分编队是玩家侧操作，AI 不会自行拆分 → 对武装对象不可达（与 §3a 场景 3 同因）。可达的等价项为"增援波次补员"（本 §3c 已覆盖）。

**本轮附带观察 → 已开卡并定位（CB-REPAIR-0004）**：本 run 第 3 场出现「Route A **占位不开火**」——`replaced=1` 却 `edgeRetries=0`，同时 `externalWhileActive=1780`、`externalDuration=1101.9s`。

- **数据更正（2026-09-14，按 mission time 复核）**：本节早前写的「覆盖后仍有 333 次 `BehaviorSelected`」**有误**。实测该 session `BehaviorSelected` 共 **699** 次 = 覆盖前 **425** + 判定窗口 (215.03s, 979.43s) 内 **0** + 其后 **274**。窗口内只有 `SetterBegin/SetterCompleted/ExternalWhileActive` 各 1529 条，`Exit/Restore/Intent/EdgeRetry` 全 0。
- **根因**：`Evidence.EdgeRetry` 唯一触发点 `RouteAControl.cs:180` ← `TrySelect` ← `RouteAOrderAdapter.cs:27` ← `RouteABehavior.TickOccasionally`；引擎只对 FormationAI **胜出的 ActiveBehavior** 按 0.5s 节拍回调（反编译契约由 `OfflineTests/RouteAStaticChecks.ps1` 锁定）→ 该窗口 Route A 的 Behavior 未被选中 ⇒ `TrySelect` 从未被调用 ⇒ armed 边沿重试永不执行；且 0 Exit ⇒ 未 Release ⇒ `BlocksExecutor(true, Movement)` 仍为真（`FormationCommandExecutor.cs:117`）→ **持有所有权却不落地**。
- **处置（含返修轮次，截至 2026-09-14）**：
  - r1 `723b457`：`DeliveryDeadlineSeconds=10f` 交付上限 → `Release(RouteAExit.SelectionTimeout)` + 暂停重取；汇总新增 `lastSelectedAt=`。
  - 质检 t8 = **needs_revision**（R4-1：`_deliverySuspended=false` 位于 `previous == null` 早退之前 → no-op Release 静默作废暂停）。
  - r2 `bc8449a`（同标题的早期提交 `6f632ae` 已被 amend 取代；当前 HEAD）：交付暂停仅由**真实释放**终结（+ 具名 `HasExternal` / 授权语义修正）；离线 **1268 PASS / 0 FAIL**。
  - **Astra 独立复审判 r2 = FAIL（P1，周期重占位）**：普通态势数值变化（Astra 复现为 60Hz、每拍 `X+=0.001`、全程零 `BehaviorSelected`）在超时释放后**下一拍即重新武装** → 10.000s 释放、10.0167s 重取，占空比约 **99.83%**，普通执行器只获单帧窗口。
  - **r3 派单 = `prompts/active/CombatBrain-CB-REPAIR-0004-r3-周期重占位返修派单-2026-09-14.md`（registry CB-PROMPT-0041，ACTIVE）**：要求重武装只能由**具名、离散、可解释、有界**的条件触发（不得再把原始 `situationSignal` 任意变化当作战术变化），并补 6 项组合回归 + 反向验证；**当时的台账状态为 CB-REPAIR-0004 `IN_PROGRESS`（派单要求该轮不部署、不采样、不推进部署基线）——该卡已于 2026-09-15 关闭（`COMPLETED`，`commit=238e31a5…`，证据 4 条 PASS，`repair-verify=ALL CONSISTENT`）**。
- **未定论部分**：引擎侧「为何停止选中」为 Inferred（窗口内每 0.5s 恰 1 次原生 Movement 写 ⇒ FormationAI 节拍仍在，胜出者非 Route A Behavior）；建议并入下轮采样的最小观测，不单独立卡。
- **下轮采样验收点（部署与采样经用户裁决排在 Astra 回签之后）**：`ExternalReplaced` 后 **≤10s** 应出现 `Exit(reason=SelectionTimeout)`；超时释放后普通执行器须获得**持续**（非单帧）Movement 窗口；`SelectionTimeout` 触发频率理想为 0 或仅出现于真实覆盖失效处。

## 3d. CB-REPAIR-0004 r3 与独立复核（2026-09-14）

**SHA 对应（J-5，防下一轮对不上号）**：r1 = `723b457`；r2 = **`bc8449a`**（派单 §1 写的起点 `6f632ae` 是 r2 的**早期同标题提交、已被 amend 取代、不在祖先链**，两者差 7 行注释）；r3 = **`395fe13`**（= 当时的 HEAD）。部署基线仍为 `9a2afe4`，上述三者**均未部署**。

**r3 修复语义**（`RouteAControl.cs`）：删除 r2 的「`situationSignal` 变化即解锁」；暂停期持续让出 Movement 所有权；重武装须**同时**满足①真实战术意图变化（`intent != 暂停时锁存的 _suspendedIntent`，源自 Formation Mission/相位映射）②冷却 `ReArmCooldownSeconds=10f` ③本暂停期授予次数 `< MaxSuspendedReattempts=2`；合格事件只授一次尝试，仍无 `BehaviorSelected` → 再次期限内 `Release(SelectionTimeout)` 并恢复暂停；预算重置＝真实选中回调／非超时真实释放／`BeginSession`；可观测 `RouteAEventKind.SuspensionRetry` + 汇总 `suspensionRetries=`。

**独立复核（质检 t12，verdict = pass-with-runtime-debt）**：以 `git archive` 把 r2/r3 分别只读抽到 `%TEMP%` 并各自构建**生产** DLL，再驱动生产 `RouteAControl` + `RouteASituationStamp.Update` + `FormationCommandExecutor.ExecuteWinner`（weights 端仅 `IRouteAWeights` 替身，逻辑零复制；未 checkout、未加 worktree、未写 repo）。

| 场景（60Hz 驱动） | r2 = `bc8449a` | r3 = `395fe13` |
|---|---|---|
| 每拍 `X+=0.001`、零选中 | **复现 Astra P1**：释放@10.0167、重取@**10.0333**，每 10s 一次；100s duty **99.85%**、acquires 10、执行器仅 9 拍(0.15%) | acquires=1；40/100/150s **零重取**；duty 25%/10%/6.67%；执行器 75%/90%/93.33% 且为连续窗口 |
| 兵数变化 | duty **98.75%**（P1 之洞不限于位置变化） | 同上（acquires=1，零重取） |
| 12 维向量微变 | duty **99.88%** | 同上（acquires=1，零重取） |
| 合格离散事件 | — | acquires=2 + `SuspensionRetryEvents=1`（只授一次、具名可观测） |
| CHURN（意图每拍抖动 + 连续变号） | — | 150s acquires 封顶 **3**、Retry 封顶 **2** → 无周期重占位 |
| HEALTH（稳定选中） | `selected=80 applied=2 replaced=1 edgeRetries=1 lastSelectedAt=39.517s suspensionRetries=0` | 与 r2 **逐字段一致** → 交付窗口刷新与单次边沿授权无回归 |

质量门（t12 实跑）：`--no-incremental` build 0/0；OfflineTests ALL PASS **1297/0**（Check 站点 1100→1120）；`RouteAStaticChecks.ps1` 20 条 `[PASS]`、exit 0；trivia-strip 静态 setter **4=4**；`git diff --check` 两口径 exit 0。未部署三重确认（无 Bannerlord 进程、deployed SHA `A39AC964…` vs 新构建 `7E5FC25A…`、日志 mtime 未变且 `SelectionTimeout`/`lastSelectedAt` 各 0 行）。

**J-1（反向验证产物登记，按未验证形式闭合）**：开发侧给出的反向验证结果为 `REVERSE-CHECK PASS=1280 FAIL=17`，具名失败断言示例：`r3 position micro change: never re-arms during continuous situation changes`、`r3 unit count change: every tick's Movement proposal reaches the executor (sustained window)`、`r3 situation vector change: no suspension retry granted without a qualifying event`、`r3 qualifying event inside the cooldown is not granted (bounded rate)`、`r3 after the attempt budget is spent the control keeps yielding (executor writes every tick)`；临时改动已撤销（无 TEMP 残留），复跑 1297/0。**该产物未按契约字段登记 → 质检按「未验证」记账**；风险由更强的等价证据（真 r2 生产构建复现 P1 + r3 判否）闭合，故未阻塞本卡。本文件此处即为补登。

**J-2（口径，已裁决）**：预算重置路径实际只有「持所有权时的真实选中」「非超时真实释放」「`BeginSession`」；**预算耗尽后不再持有所有权 ⇒ 本场战斗内不可自愈**（CHURN 实测 acquires 封顶 3）。安全方向正确。**用户 2026-09-14 裁决：接受该语义**——「**一场内出现一次坏窗口 = 本场 Route A 退役**」，已写入 `docs/cps4-hardgate-checklist-2026-09-08.md`（2026-09-14 更新节），并保留在交 Astra 的问题清单（brief §5 问题 4）供外部复核。判定含义：采样中"该场后续无 Route A 活动"属既定语义，不得当作新缺陷；若未来要求自愈，须另立卡设计具名/离散/有界的自愈触发。

**J-3 / J-4（low，未修）**：J-3 证据表满时 `_suspendedReattempts` 与 `SuspensionRetry` 事件可能不一致；J-4 超时暂停依赖"先清后设"次序（r2 已留陷阱注释）。

**已闭合**：R4-1（空 Release 解锁暂停）随 r2 闭合；**F-3（`0f` 哨兵）/ F-4（`edgeRetries` 落账口径）随 r2/r3 闭合**（`HasExternal` 具名取代哨兵、`edgeRetries=授权次数` 语义与断言）；F-5 与运行期债仍在。
**复核环境**：质检 harness 留在 `%TEMP%\qc-t12`（`drv-r2`/`drv-r3` + `Program.cs`），可供 Astra 或后续复跑（临时目录，非仓库产物）。

## 3e. r3 受控部署后的运行期样本（2026-09-14，run=`c81c4f5e6d7d47bba5a81b7174eb1f0e`）

**部署绑定**：commit `395fe13`；部署 DLL SHA `7E5FC25A0EF0559D718B0D5E42994141846291501E3342F94E6C0B1F713FD8D6`（src=dst，MATCH）；MCM `Cps3Mode=E_RouteA` + `Cps3ObserverEnabled=true`（`10:59:52.071 [SetOrderObserver] 观测已挂载 prefixes=3`）；自定义战斗 200v200（`[Bench] agents=401`，avgAi 0.02~0.07ms）；`BattleDuration=314.38s`，无快进痕迹。

**生命周期事件（本场仅 3 条）**：

| 时刻 | 事件 |
|---|---|
| 10:59:57.689（t=0.550） | `event=Intent intent=Hold revision=1`（武装） |
| 11:00:07.699（t=10.550） | **`event=Exit reason=SelectionTimeout`** |
| 11:00:07.699 | `event=Restore reason=SelectionTimeout` |
| 之后 → 11:05:24（约 304s） | **721 条 `ExternalAfterExit`**（原版 Movement 写持续），**无任何再武装** |

**该场汇总（末）**：`selected=0 applied=0 replaced=0 externalWhileActive=0 edgeRetries=0 externalDuration=0.000s stableSoFar=0 intentChange=0 weightFlap=0 lastSelectedAt=never suspensionRetries=0 restoreFailures=0`；全窗口（4525 行）**异常类行 0**。

**逐项判定（Astra 回签最小集）**：

| # | 验收点 | 结果 |
|---|---|---|
| S1 | 连续变号不重取 | **PASS（强）**：释放后 304 秒零再武装（对照 r2/Astra 为每 10s 重取、duty ≈99.83%）；`suspensionRetries=0` |
| S2 | 合格意图变化仅一次 `SuspensionRetry` | **本场未触发**（`intentChange=0`，无合格离散事件）→ **无样本，不判 fail** |
| S3 | 零选中后按期限 `SelectionTimeout` + `Restore` | **PASS**：武装后**恰好 10.00s**（t=0.550 → 10.550）触发；`Restore` 紧跟；`restoreFailures=0` |
| S4 | 释放后普通执行器持续可用 | **间接 PASS**：释放后所有权不再阻塞（`BlocksExecutor=false`），721 条原版 Movement 写贯穿余场；**本场 CB 侧无 Movement 提案样本** |
| S5 | 健康 `EdgeRetry`／归因／`RestoreFailures=0` | **部分**：`restoreFailures=0`、`weightFlap=0`、0 异常 ✓；`edgeRetries=0` 因本场无外部覆盖（`replaced=0`）→ **健康 EdgeRetry 路径本场无样本** |

**附加事实（重要）**：本场 Route A **未落地任何命令**（`applied=0`）——武装后引擎**从未选中**其 Behavior（`selected=0`、`lastSelectedAt=never`），这正是「占位不开火」的成因；r3 的 10s 交付上限把该状态限制为 10 秒并令本场 Route A 退役（符合 J-2 裁决语义「一场内一次坏窗口 = 本场退役」）。

**仍需补的样本**：①**S2**——需一场"暂停期间出现合格意图变化"的战斗；②**S5 的 edgeRetry 路径**——需一场发生外部覆盖的战斗（形态参照早期 `run=ba84a11b`：`ExternalReplaced → EdgeRetry → OrderApplied`）。

## 3f. r3 第二个运行期样本：健康路径与生命周期（2026-09-14，run=`6223ad9bc00045aa8bf188cd16214883`）

**绑定**：同一部署（commit `395fe13` / DLL `7E5FC25A…`）；MCM E 模式 + 观测在位；`agents=601`；`BattleDuration=505.51s`；无快进；全窗口 6668 行**异常 0**。

**汇总（末）**：`selected=746 applied=9 replaced=0 externalWhileActive=1 edgeRetries=0 externalDuration=0.000s stableSoFar=9 unverified=0 dropped=0 intentChange=3 weightFlap=0 lastSelectedAt=493.531s suspensionRetries=0 restoreFailures=0`

**生命周期（6 组 Exit/Restore 全配对）**：

| t（s） | 事件 |
|---|---|
| 0.55 | `Intent(Hold)`；1.46 `ExternalWhileActive`（1 次外部写，**非覆盖**）；1.55 `OrderApplied(Hold)` |
| 17.15 | `Exit(LostAIControl)` + `Restore` |
| 64.39 / 101.46 / 188.91 / 221.11 / 313.61 | 重新武装并 `OrderApplied`（每次都在资格恢复后） |
| 99.46 / 185.90 / 204.52 / 260.82 | 期间 4 次 `Exit(LostAIControl)` + `Restore`（均配对） |
| **439.26 → 448.29 → 470.42** | **意图真变化链 Hold→Advance→Retreat→Hold，每次都 `OrderApplied` 落地**（`intentChange=3`） |
| 493.58 | `Exit(EmptyOrInvalid)` + `Restore`（编队清空/战斗收束） |

**判定**：
- **健康路径回归 = PASS（强）**：`applied=9`、`stableSoFar=9`、`selected=746`、`lastSelectedAt=493.531s`（交付窗口持续刷新）、`weightFlap=0`、`restoreFailures=0`、`unverified=0`、`dropped=0` → r3 未损害正常执行链；
- **生命周期恢复 = PASS**：`Exit`×6（`LostAIControl`×5 + `EmptyOrInvalid`×1）**全部配对 `Restore`**，且每次资格恢复后都能重新武装；
- **S4 = PASS（直接形态）**：释放期间原版写入正常（`ExternalAfterExit` 21 条），Route A 未占位阻塞；
- **S2 仍无样本**：本场未进入暂停（无 `SelectionTimeout`，`suspensionRetries=0`）；
- **S5 的 edgeRetry 路径仍无样本**：本场 `replaced=0`（无外部覆盖，1 次 `ExternalWhileActive` 按 F-2 语义不释放、不消耗预算）。

**结论**：本场为「健康路径 + 多轮生命周期」强样本；**S2 与 S5-edgeRetry 两类样本仍需在后续场次中自然出现**（均为条件组合型，非每场必现）。

## 3g. r3 第三个运行期样本（2026-09-14，run=`ce970979229b4b3f82d73badbad641c5`）——短样本，无新验收点

**绑定**：同部署（`395fe13` / DLL `7E5FC25A…`）；`13:01:10.447` 观测挂载 `prefixes=3`；同 run 一场 `BattleDuration=334.76s`。

**全部事件（85 行）**：

| t（s） | 事件 |
|---|---|
| 0.536 | `Intent(Hold)`（武装） |
| 1.463 | `ExternalWhileActive` ×3（外部写入，**非覆盖**；按 F-2 语义不释放、不消耗预算） |
| 1.529 | **`Exit(LostAIControl)` + `Restore`** |
| 之后 → 战斗结束 | **无任何 Route A 事件**（未再武装） |

**汇总（末）**：`selected=0 applied=0 replaced=0 externalWhileActive=3 edgeRetries=0 stableSoFar=0 intentChange=0 weightFlap=0 lastSelectedAt=never suspensionRetries=0 restoreFailures=0`

**判定**：`restoreFailures=0`、无异常行 ✓；`Exit(LostAIControl)` 配对 `Restore` ✓。**无 S2 样本**（本场未进入暂停，退出原因是 `LostAIControl` 而非 `SelectionTimeout`）；**无 S5-edgeRetry 样本**（`replaced=0`，3 次外部写均为 `ExternalWhileActive`）。本场亦未再武装（资格未恢复），属资格门控行为、非缺陷。

## 3h. r3 第四个运行期样本：**S2 补齐**（2026-09-14，run=`d43c302ee4944e7d9de445d460df2393`）

**绑定**：同部署（`395fe13` / DLL `7E5FC25A…`）；`agents=1501`、`BattleDuration=1365.66s`（长局）、窗口 15339 行**异常 0**、`AnomalyLongStalemate=1`（长僵持，与本 mod 无关的历史常见现象）。

**完整生命周期链**：

| t（s） | 事件 | 判定要点 |
|---|---|---|
| 501.995 / 502.030 | `Intent(Advance rev1)` → `OrderApplied` | 正常落地 |
| 531.752 / 532.029 | `Intent(Hold rev2)` → `OrderApplied` | 意图真变化即落地 |
| 532.255 | `Exit(LostAIControl)` + `Restore` | 配对 ✓ |
| 559.379 | `Intent(Hold rev3)`（重新武装） | — |
| 569.381 | **`Exit(SelectionTimeout)`** + `Restore` | **Δ=10.002s**，期间零选中 |
| **587.012** | **`SuspensionRetry`（rev3）** + `Intent(Advance rev4)` | **合格事件=意图真变化 → 恰好一次重取**；距超时 17.63s（>10s 冷却） |
| 590.534 | `Intent(Hold rev5)`（意图又变） | **冷却内 → 未授予重取** ✓（冷却门禁实机生效） |
| 592.548 | `Exit(LostAIControl)` + `Restore` | 非超时真实释放 → 预算重置 |
| 675.086 | `Intent(Hold rev6)` | — |
| 685.088 | **`Exit(SelectionTimeout)`** + `Restore` | **Δ=10.002s** |
| 1179.461 | **`SuspensionRetry`（rev6）** + `Intent(Advance rev7)` | **超时后 494s 零再武装**（S1 强证据），直到合格事件出现才重取一次 |
| 1189.467 | **`Exit(SelectionTimeout)`** + `Restore` | **Δ=10.006s**（重取尝试同样按期限释放） |
| 1199.470 | **`SuspensionRetry`（rev9）** + `Intent(Hold rev10)` | 本暂停期第 2 次重取（**达上限 `MaxSuspendedReattempts=2`**） |
| 1209.470 | **`Exit(SelectionTimeout)`** + `Restore` | **Δ=10.000s**；此后**无第 3 次重取**（预算耗尽 → 本场退役，符合 J-2 语义） |

**汇总（末）**：`selected=61 applied=2 replaced=0 externalWhileActive=94 edgeRetries=0 externalDuration=649.652s stableSoFar=2 unverified=0 dropped=0 intentChange=4 weightFlap=0 lastSelectedAt=532.029s suspensionRetries=3 restoreFailures=0`
`Exit` 分布：`SelectionTimeout` ×4、`LostAIControl` ×2，**全部配对 `Restore`**。

**判定**：
- **S2 = PASS（首次取得运行期样本）**：三次 `SuspensionRetry` 分别对应三次**具名/离散**的意图真变化（rev3→rev4、rev6→rev7、rev9→rev10），每次合格事件**只授予一次**重取；
- **S1 = PASS（进一步加强）**：`685 → 1179` 长达 **494 秒**持有资格却不重新武装（同期 94 次外部写），只在合格离散事件出现时才重取 → 无周期重占位；
- **S3 = PASS（×4）**：四次 `SelectionTimeout` 均在武装/重取后 **10.000–10.006s** 触发，且均配对 `Restore`；
- **冷却门禁 = 实机确认**：冷却内的意图变化（590.534）未授予重取；
- **预算上界 = 实机确认**：第 2 次重取超时后不再重取（预算耗尽）；
- `restoreFailures=0`、`weightFlap=0`、`unverified=0`、`dropped=0`、异常 0 ✓；
- **S5-edgeRetry 仍无样本**：本场 `replaced=0`（94 次外部写全为 `ExternalWhileActive`，按 F-2 语义不释放、不消耗预算、不计入 edgeRetry）。

## 3i. r3 第五个运行期样本：**S5 负样本 + 超长局稳定性**（2026-09-14，run=`0496f7b680d3455587862d570703790e`）

**绑定**：同部署（`395fe13` / DLL `7E5FC25A…`）；`agents=1426`、**`BattleDuration=3809.58s`（约 63.5 分钟，迄今最长）**、窗口 20147 行**异常 0**；本场武装对象为 **`f=1`**（非 f=0）。

**时间线**：

| t（s） | 事件 | 要点 |
|---|---|---|
| 967.540 / 967.588 | `Intent(Advance rev1)` → `OrderApplied` | 正常落地 |
| 1110.648 / 1111.083 | `Intent(Hold rev2)` → `OrderApplied` | 正常落地 |
| **1115.090** | **`ExternalReplaced`** | **外部覆盖真实发生**（距上次落地 4.0s；`lastSelectedAt=1114.587` = 覆盖前最后一次选中） |
| 1124.593 | **`Exit(SelectionTimeout)` + `Restore`** | **Δ=9.5s**；**armed 的边沿重试未执行**（覆盖后 Behavior 再未被选中） |
| 1161.798 | **`SuspensionRetry`（rev2）** + `Intent(Advance rev3)` | 合格离散事件 → 只授一次重取（距超时 37.2s） |
| 1165.330 | `Intent(Hold rev4)` | 冷却内 → 未授予 |
| 1171.802 | `Exit(SelectionTimeout)` + `Restore` | Δ=10.0s |
| 2067.656 | **`SuspensionRetry`（rev4）** + `Intent(Advance rev5)` | **距上次 895.9 秒零再武装**；本暂停期第 2 次（达上限 2） |
| 2077.659 | `Exit(SelectionTimeout)` + `Restore` | Δ=10.0s；此后无重取（预算耗尽） |

**汇总（末）**：`selected=295 applied=2 replaced=1 externalWhileActive=60 edgeRetries=0 externalDuration=962.490s stableSoFar=1 unverified=0 dropped=0 intentChange=2 weightFlap=0 lastSelectedAt=1114.587s suspensionRetries=2 restoreFailures=0`

**判定**：
- **S5-edgeRetry = 负样本（重要）**：`ExternalReplaced` 确实发生，但 **`EdgeRetry` = 0**——按 r3 语义，armed 的重试只在"下一次真实选中"时兑现；本场覆盖后 Behavior 再未被选中，故重试从未兑现，9.5s 后由交付上限释放。**结论：S5 的正向形态（`ExternalReplaced → EdgeRetry → OrderApplied`）除"发生覆盖"外还需"覆盖后 10s 内仍被选中"这一附加条件**——与 S2 同属条件组合型样本。
- **S1/S3 再次加强**：`1171.8 → 2067.7` **895.9 秒零再武装**（同期 `externalWhileActive` 60 次）；本场 3 次 `SelectionTimeout` 均 Δ≈10.0s 且全部配对 `Restore`。
- **超长局稳定性**：63.5 分钟、`weightFlap=0`、`restoreFailures=0`、`unverified=0`、`dropped=0`、异常 0；`applied=2 / replaced=1 / stableSoFar=1` 自洽。

**日志归档（供后续研究）**：
- 全量日志：`MBmod/CombatAI/logs/CombatBrain-2026-09-14-full.log`（50,728,702 bytes，SHA256 `63ECC4A976DD1EAA866197F7A11C5624F33206DE5696C3ACA2FDE92A67FD9D38`）
- 本 run 切片：`MBmod/CombatAI/logs/run-0496f7b6-20260914-longbattle.log`（1,608,480 bytes / 5194 行，SHA256 `585DCEAD2A90C9B357BD39D723E43C3085301A9360B83A995733ECBA4F6FCA14`）

## 3j. r3 第六个运行期样本：**Rejoin 分支首次取得运行期样本** + **S5 负样本再现**（2026-09-14，run=`4156cc25a2d54790820787ab08cdbb52`）

**绑定**：同部署（r3 `395fe13` / DLL `7E5FC25A…`，DLL mtime `2026-09-14 09:34:23`，本 run 前无重部署）；`15:34:58.687 [SetOrderObserver] 观测已挂载（prefixes=3）`；模式 `E_RouteA`（有 `[RouteA]` 行）；**无快进**（mission time / 墙钟 ≈ 1.0，session 2 全场 549s 对应 15:50:10→15:59:19）；新日志窗口 1,430,342 bytes / 12,563 行，**异常 0**。

> **run / session 语义更正（本场首次暴露）**：`RunId` 是 `RouteAOrderAdapter.cs:95` 的 `static readonly`（**进程级**），`session` 每场 mission 递增 → **一个 run id 可跨多场**。本 run 含两场（session=1 / session=2）；此前样本 ②–⑤ 均为 `session=1`，"一个 run = 一场"的隐含假设不成立。

### session=1（15:35:05–15:48:48，`BattleDuration=770.9s`）——用户澄清**非本次申报场次**

开战编队（t=4.5s 全编队快照）：Team 0 = 635（步158/射159/骑80/骑射158/骑80）、Team 1 = 616（步173/骑96/骑97/骑射250）；`agents=2032`（含马匹）。
汇总（末）：`selected=282 applied=11 replaced=0 externalWhileActive=3 edgeRetries=0 externalDuration=0.000s stableSoFar=11 unverified=0 dropped=0 intentChange=3 weightFlap=0 lastSelectedAt=657.527s suspensionRetries=0 restoreFailures=0`。
`Exit`×8 **全部 `LostAIControl` 且全部配对 `Restore`**，每次资格恢复后重新武装并落地（`Intent → BehaviorSelected → OrderApplied` 成链）。

### session=2（15:50:10–15:59:19，`BattleDuration=548.99s`）——**本次申报场次：约 1020v1020 纯步兵/弓箭手各半**

实测开战编队（t=4.7s）：Team 0 = **1016**（步507/射508/骑1）、Team 1 = **994**（步489/射505）；`agents=2032`；无快进。

| t（s） | 事件 | 要点 |
|---|---|---|
| 0.530 | `Intent(Hold rev1)` | 武装 |
| 1.528 | `Exit(LostAIControl)` + `Restore` | 资格丢失（非超时） |
| 215.357 | `Intent(Hold rev2)` → `BehaviorSelected` → `OrderApplied(ot=6)` | 资格恢复后重新武装并落地 |
| 320.027–325.030 | 连续 `BehaviorSelected`×11（每 0.5s 一次，seq 233–243） | 健康选中窗口 |
| **325.529** | **`ExternalReplaced`**（外部 `ot=5`） | 覆盖真实发生（`lastSelectedAt=325.030` = 覆盖前最后选中） |
| 335.035 | **`Exit(SelectionTimeout)` + `Restore`** | Δ=**9.507s**（自覆盖）/ **10.005s**（自末次选中）；**`EdgeRetry` 未兑现** |
| 335.035–548.761 | `ExternalAfterExit`×**188** | 释放后执行器连续写入 **213.7s**；**零再武装、零选中**（`selected` 停在 221） |

汇总（末）：`selected=221 applied=1 replaced=1 externalWhileActive=18 edgeRetries=0 externalDuration=9.003s stableSoFar=0 unverified=0 dropped=0 intentChange=0 weightFlap=0 lastSelectedAt=325.030s suspensionRetries=0 restoreFailures=0`；`Exit`×2 **全部配对 `Restore`**。

### 判定

- **Rejoin 分支 = 首次取得运行期样本（session=1，新）**：部署期（`15:35:00.695–15:35:05.285 phase=Deployment`；武装对象 `Team 1 f=0 mission=SupportMainEffort`；`Team 1 phase=Deployment MB=(844,729) str=366 conf=0.59` = MainBody 有效）→ `t=0.542 Intent(Rejoin rev1)` → `t=4.034 BehaviorSelected + OrderApplied(ot=1, setterSeq=131, source=CombatBrainRouteA)`。`CommandValid` 对 Rejoin 要求 `Move` **且落地坐标 == 目标坐标**，故这一次落地即证明 **Rejoin 目标解析 + 导航路径距离校验通过**；本窗口 `NavigationFailed` = **0 次**。
  **限定**：仅 1 次、且只覆盖**成功路径**；`NavigationFailed` 失败路径仍无样本（§3b 的失败路径条目不变）。
- **S1 = PASS（再次）**：session=2 超时释放后 **213.7s 零再武装**（同期 188 次外部写）。
- **S2 = 无样本**：session=2 暂停期 `intentChange=0`（无合格意图变化）；session=1 全程未进入暂停（无 `SelectionTimeout`）。
- **S3 = PASS**：Δ=10.005s（自末次选中）/ 9.507s（自覆盖），`Restore` 紧随，`restoreFailures=0`。
- **S4 = PASS（直接形态）**：释放后 188 次 `ExternalAfterExit` 跨越 **213.7s**（`t=335.035→548.761`），非单帧窗口。
- **S5-edgeRetry = 负样本（第 2 次）**：覆盖真实发生，但 `edgeRetries=0` 且**覆盖后 Behavior 再未被选中**（`lastSelectedAt=325.030 < 325.529`）；9.5s 后按期限释放。**正向形态（`ExternalReplaced → EdgeRetry → OrderApplied`）仍缺样本**——与 §3i 同形；两场负样本都呈"覆盖后引擎不再选中该 Behavior"（原因仍属 Inferred，见 §3c 未定论条目）。注：正向形态在 r3 前的构建上确实出现过（§2 `run=ba84a11b`，Δ=5.00s），故并非结构不可达。
- **健康路径（session=1）**：`applied=11 / stableSoFar=11`、`lastSelectedAt` 持续刷新至 657.527s、`replaced=0`、`weightFlap=0`、`restoreFailures=0`、`unverified=0`、`dropped=0`，异常 0。
- **归因（新观察，Descriptive；不在本轮改码）**：RouteAEvent 渠道 `source=CombatBrainRouteA` **36 条 = 12 次落地 × 3**（`SetterBegin/SetterCompleted/OrderApplied`），内部完全自洽；但 `[OrderTraceAgg]`（15:48:48.944 / 16:00:30.252）两场均为 **`CombatBrainRouteA=0/o0`**，`[OrderTrace]` 31 条中 **0 条**出现 `CombatBrainRouteA`。结构性原因：`RouteAOrderAdapter.cs:33-36` 只在 `CommandSourceScope.Enter(CombatBrainRouteA)` 内直接 `SetMovementOrder`，**不调用** `CommandProvenance.Instance.Record`（全仓唯一 `Record` 调用点 = `FormationCommandExecutor.cs:147`，route B 执行器）→ Route A 的写入不进入 5s `OrderTrace` / 终局 `OrderTraceAgg` 聚合。**属 S5「`OrderTraceAgg` 归因与执行器一致」的口径问题**，**已由 Astra 2026-09-15 裁定采用方案 A（只改文档口径、不改码）：S5 归因改为以 `[RouteAEvent]` 的 `OrderApplied`/`SetterBegin`/`SetterCompleted` 三方自洽判定，`[OrderTraceAgg]` 只验普通执行器与释放后窗口**（落点：`docs/cps3r-routea-r3-sampling-record-2026-09-14.md` §2 的 S5 行、`docs/cps4-hardgate-checklist-2026-09-08.md` 的 2026-09-15 更新节、`docs/knowledge/CURRENT_STATE.md`）；冻结令下不接线。

**日志归档（研究用途）**：
- 全量快照：`MBmod/CombatAI/logs/CombatBrain-2026-09-14-full-1600.log`（52,159,044 bytes / 402,155 行，SHA256 `D6B80D36DD7CFD07A8347166934DEF1A834F2D1892EBE5E2748D0B96A5ECEAE9`）
- 本 run 切片：`MBmod/CombatAI/logs/run-4156cc25-20260914-twobattles.log`（488,112 bytes / 1594 行，SHA256 `5437418055FD9FCAD8CA36C0482B2E00965104D67F80EF5F19177336178E3118`）

## 3k. r3 第七个运行期样本：**健康局中途覆盖 → 全场断流（S5 负样本第 3 次，最强）**（2026-09-14，run=`4e885a4e17c8421f81f9ba421ecfc0fb`）

**绑定**：同部署（r3 `395fe13` / DLL `7E5FC25A…`，本 run 前无重部署）；`20:49:48.537 [SetOrderObserver] 观测已挂载（prefixes=3）`；**无快进**（mission 346.7s / 墙钟 355.6s ≈ 0.975）；新日志窗口 5,613 行**异常 0**。
**战斗配置（观测值）**：Team 0 = 635（步158/射159/骑160/骑射158）、Team 1 = 616（步173/骑96/骑97/骑射250）——即 §3j session=1 同型**混编场**（非 1020v1020 纯步/射场）；`agents=2032`、`BattleDuration=347.43s`。

### 时间线（mission time）

| t（s） | 事件 | 要点 |
|---|---|---|
| 0.544 | `Intent(Rejoin rev1)` | 部署期 `Team 1 f=0 mission=SupportMainEffort`；**本次未落地**（t=5.085 意图转 Advance） |
| 5.536–220.029 | **`BehaviorSelected` ×379、`OrderApplied` ×15**（Hold/Advance 交替 rev2→rev16） | **健康路径**：每 0.5s 连续选中，逐次落地 |
| 18.226 / 159.816 / 179.477 | `Exit(LostAIControl)` + `Restore` ×3 | 资格丢失→恢复后**均重新武装并落地** |
| **220.528** | **`ExternalReplaced`**（外部 `ot=5`，`relatedSeq=422`） | 覆盖发生在**健康选中的正中间**（覆盖前 0.5s 仍在选中：`220.0285`） |
| 230.030 | `Exit(SelectionTimeout)` + `Restore` | Δ=**9.507s**（自覆盖）/ **10.001s**（自末次选中） |
| 240.035 | `SuspensionRetry`（rev16）+ `Intent(Advance rev17)` | 冷却满 10s 后具名意图变化 → **授予第 1 次重取** |
| 250.039 | `Exit(SelectionTimeout)` + `Restore` | 重取后仍 10.0s 无任何选中 |
| 280.705 | `SuspensionRetry`（rev17）+ `Intent(Hold rev18)` | **第 2 次重取**（达 `MaxSuspendedReattempts=2` 上限） |
| 281.7 / 283.7 / 285.2 | `Intent(Advance rev19 / Hold rev20 / Advance rev21)` | 冷却内 → **一律未授予**（连续变号不重取） |
| 290.706 | `Exit(SelectionTimeout)` + `Restore` | 第 3 次超时；此后预算耗尽，无重取；战斗至 347.43s 结束 |

**汇总（末）**：`selected=379 applied=15 replaced=1 externalWhileActive=61 edgeRetries=0 externalDuration=285.233s stableSoFar=14 unverified=0 dropped=0 intentChange=15 weightFlap=0 lastSelectedAt=220.029s suspensionRetries=2 restoreFailures=0`；`Exit`×6（`LostAIControl`×3 + `SelectionTimeout`×3）**全部配对 `Restore`**。

### 判定

- **S5-edgeRetry = 负样本（第 3 次，且是最强的一次）**：覆盖发生在**引擎正在每 0.5s 连续选中**的健康局中；覆盖后 **`BehaviorSelected` = 0、持续 127.3s 至战斗结束**（`lastSelectedAt=220.029s` 冻结），期间 CB **成功重取 2 次**（`SuspensionRetry`×2，各持有 10s 交付窗口）**引擎仍未回调一次**。→ S5 正向形态第三次未取得；三次覆盖样本（§3i/§3j/§3k）**全部**呈"覆盖后该行为不再被引擎选中"。
- **S2 = PASS（第 2 次样本）**：两次 `SuspensionRetry` 各对应一次具名意图真变化（rev16→17、rev17→18），每次只授予一次；冷却内的 rev19/20/21 与上限后的意图变化**均未授予**，`suspensionRetries=2` 恰达上限。
- **S1 = PASS（再确认）**：连续变号（rev19/20/21，间隔 1–2s）**零重取**。
- **S3 = PASS（×3）**：三次 `SelectionTimeout` 分别在覆盖后 9.507s／重取后 10.0s／重取后 10.0s 触发，全部配对 `Restore`，`restoreFailures=0`。
- **S4 = PASS（直接；口径按 Astra P1 复审更正）**：首次超时释放后 `ExternalAfterExit`×**190**，分布 t=230.030→347.218（总跨度 **117.2s**），但**不得称为"单一连续普通执行器放行窗口"**——该区间内含 CB **两次重新持有所有权**（240.035→250.039、280.705→290.706，各约 **10.0s**）；扣除此两段后普通执行器实得三段连续窗口 ≈**10.0s / 30.7s / 56.5s（合计 ≈97.2s）**。结论仍成立（释放后执行器获得**持续**而非单帧窗口），但量化口径以本条为准。
- **健康路径 = PASS（强）**：覆盖前 220s 内 `applied=15 / stableSoFar=14`、`selected=379`、`weightFlap=0`、`unverified=0`、`dropped=0`、异常 0；三次 `LostAIControl` 后均恢复接管。
- **归因（复现 §3j 观察，第 4 次）**：本场 `[OrderTraceAgg]` 亦为 `CombatBrainRouteA=0/o0`（applied=15），Route A 写入仍不进 `CommandProvenance` 聚合。
- **新增判别事实（关键，Descriptive；口径按 Astra P1 复审更正）**：本场 `ExternalWhileActive` **覆盖前仅 3 次（全部落在部署拍 t=5.295）**、**覆盖后 58 次**（t=221.0295→290.5281；**分布非均匀：含 11.0s 与 31.0s 两处间隔，最大间隔 31.0s**，其余间隔约 0.5s）。**来源仍为 `ExternalUnknown`** ⇒ 只能报告"覆盖后外部 Movement 写入显著增加 + 上述切片事实"，**不得写成"已证明原生持续胜出"**。另一侧：全场 `BehaviorSelected` 只有两处 >3s 空档（18.027→37.884 **19.9s**、159.531→165.351 **5.8s**，均在 `LostAIControl` 窗口内），其余时段严格每 0.5s 连续 ⇒ **覆盖后 127.3s 的静默不是引擎节拍问题**。
  **推断（Inferred，未证）**：`ExternalReplaced`（CB 落地后 ≤1s 内出现外部写）标记的正是"外部侧重新接管该编队 Movement"的时刻；此后外部写入持续存在而 CB 行为再未被选中，armed 单次边沿重试失去被兑现的前提。**"原生行为是否持续胜出"必须由 CB-REPAIR-0005 只读探针（`[RouteASilence]` 的 `activeIdx/activeType/weights`）判定**，不得仅凭日志形态下结论。

**日志归档**：全量快照 `MBmod/CombatAI/logs/CombatBrain-2026-09-14-full-2056.log`（53,009,560 bytes / 407,767 行，SHA256 `7223BB15…`）；本 run 切片 `MBmod/CombatAI/logs/run-4e885a4e-20260914-mixed-battle.log`（403,826 bytes / 1330 行，SHA256 `E3E60C97…`）。

## 3l. 诊断构建运行期采样（2026-09-15，`a24e921`）——**机制问题已由观测确立**

独立档：`docs/cps3r-routea-diagnostic-sampling-2026-09-15.md`。要点：4 场 mission（2 个 run，1949–2032 agents，无快进，窗口 44,497 行**异常 0**）中 **3 次 `ExternalReplaced` 覆盖**；`[RouteASilence]` 共 **14 条**采样拍，**全部**为 `activeIdx=0` / `activeType=BehaviorCharge` / `aiSame=1` / `aiCtl=1` / Route A 侧权重恰一个 `10.00`。⇒ **覆盖后引擎的胜出者是原生 charge 行为，并保持整个 10s 交付窗口**；CB 行为不再被回调（`edgeRetries=0`），由交付期限释放（M4 Δ=9.502s 自覆盖 / 10.002s 自末次选中）。

**本结论替换 §3k 的 Inferred 表述**（§3k 原文保留为历史证据）；§3i/§3j/§3k 的"覆盖后 127.3s / 223s / 2694s 零选中"现象由此获得机制解释。**不得扩大**：样本来自**诊断构建**（≠ r3 DLL，等价性待 Astra 认定）；单窗口未选中 ≠ 永久不可达；本件不判条件 5 / CPS-4 / GO。

## 4. 本轮未证明（运行期债，不得据本文件判 GO）

1. **可执行的五场场景已全部执行完毕**：场景 1、2、5 = **PASS**；场景 3（玩家接管）、场景 4（Rejoin）与场景 5 子项（拆分）在**本次采样口径下不适用**（§3a/§3b/§3c）——其中 **Rejoin 的成功路径已于 §3j 取得 1 次运行期样本**（失败路径 `NavigationFailed` 仍无样本；不得声称结构上永久不可达）。这不等于"条件 5 已满足"——条件 5 的判定权在 Astra 复审与用户，本文件不判 GO。
2. **未取得样本的分支**：① "新 Formation 对象身份隔离"（对象未销毁重建）；② **Rejoin 的成功路径**已于 §3j 取得 1 次样本（部署期 `SupportMainEffort`），**但 `NavigationFailed` 失败路径仍无样本**；③ F-2「重试已用后再次 `replaced=true` → 立即 `Release(ExternalOverridePersisted)`」；④ §3c 的 `edgeRetries=0` / 占位不开火——根因已定位，r3 已修复并经独立复核（§3d），运行期形态见 §3i/§3j。
3. `replaced=false` 路径（不释放、不消耗重试、仅计数）被大量观测（`externalWhileActive` 3 / 12 / **1780**），但其对所有权与抖动的实际影响仍无定论（§3c 的占位不开火现象正属此域）。
4. F-5（Release 位于 Harmony postfix 栈内改权重）无运行期证据；H-2（E 模式帧成本须按「RouteA + 观测」复合口径评定）未评定——另注意场景 5 采样使用了快进与低 BattleSize，其数字不得与其他场次混比。
5. **r3 新增口径（J-2，§3d）——已于 2026-09-15 由 Astra 复核裁定**：实现忠实、无更高规范硬冲突；「坏窗口」被明确为「**本暂停期两次重取耗尽且无真实选中**」（**不是**"首次超时即退役"）；预算耗尽后本场不可自愈的语义维持，自愈机制另立设计决策卡且列 **DEFERRED**（冻结期不授权改码，不阻断本轮收口）。
6.「玩家队豁免生效」验收点（§3a 表格末行）尚未采样；J-3（证据表满时计数不一致）、J-4（超时暂停依赖先清后设次序）为低危未修项。
7. CPS §14.1 **条件 5**：本件撰写时（Astra 复审未回签）记为未满足；**Astra 2026-09-15 复审已判 PASS**（裁定 6，账面以 `docs/knowledge/CURRENT_STATE.md` 为准；该 PASS 不推出 CPS-4/GO）。
8. **S5 正向形态缺样本（§3i + §3j + §3k 连续三场负样本；已由 Astra 2026-09-15 裁定收口）**：三场都呈"`ExternalReplaced` 真实发生 → 覆盖后引擎不再选中该 Behavior → 9.5–10.0s 按期限 `Exit(SelectionTimeout)`"，`edgeRetries` 均为 0。§3k 是其中最强的一次：覆盖发生于**每 0.5s 连续选中的健康局中**，覆盖后 **127.3s 零选中至战斗结束**，期间 CB 两次重取（各 10s 窗口）引擎仍未回调。按 §3j 口径，正向形态在 r3 前构建上出现过（§2 `run=ba84a11b`，Δ=5.00s），故**不是结构不可达**；r3 上 3/3 全负，**Astra 2026-09-15 裁定选①**（接受离线证据 + 本轮机制观测，停止继续等正样本、无需先取原生权重），机制侧结论见 §3l。
9. **Route A 归因渠道口径（§3j 新观察；Astra 2026-09-15 裁定采用方案 A：只改文档口径、不改码）**：Route A 的写入不进 `CommandProvenance` 聚合（`RouteAOrderAdapter` 不调用 `Record`），因此 `[OrderTraceAgg] CombatBrainRouteA` 恒为 `0/o0`；Route A 的归因只存在于 `[RouteAEvent] source=CombatBrainRouteA` 渠道。**落修口径**：S5 归因改为以 `[RouteAEvent]` 的 `OrderApplied/SetterBegin/SetterCompleted` 自洽验 Route A；`[OrderTraceAgg]` 只验普通执行器与释放后窗口（落点：`docs/cps3r-routea-r3-sampling-record-2026-09-14.md` §2 的 S5 行）。

## 5. 复现命令

```powershell
# 按场次过滤关键事件
Select-String $env:TEMP\CombatBrain.log -Pattern 'run=<RUN_ID>' |
  Where-Object { $_.Line -match 'event=(Intent|OrderApplied|ExternalReplaced|ExternalWhileActive|EdgeRetry|Exit|Restore)\b' }

# 只看汇总（字段名 applied=）
Select-String $env:TEMP\CombatBrain.log -Pattern '\[RouteA\] run=' | Select-Object -Last 3

# 实时只看 RouteA 行
Get-Content $env:TEMP\CombatBrain.log -Wait -Tail 200 | Select-String -Pattern '\[RouteA'
```

## 6. 登记状态

- 已完成场景：场景 1（重跑，**PASS**）、场景 2（热关闭，**PASS**）、场景 5（增援/清空，**PASS**，限定见 §3c）。
- 本次采样不适用（分支仍未验证）：场景 3（玩家接管，§3a，口径属产品/规范裁决项）、场景 4 的 **`NavigationFailed` 失败路径**（§3b，成功路径已由 §3j 取得 1 次样本）、场景 5 子项「拆分」（§3c）。
- r3 运行期样本累计八场：①`c81c4f5e`（§3e）②`6223ad9b`（§3f）③`ce970979`（§3g）④`d43c302e`（§3h）⑤`0496f7b6`（§3i）⑥`4156cc25` session=1（§3j，混编场，含 Rejoin 样本）⑦`4156cc25` session=2（§3j，申报场次 1016v994 纯步/射）⑧`4e885a4e`（§3k，混编场，健康局中途覆盖 → 全场断流；S2 第二样本 + S1/S3/S4 再确认）。**唯一缺口 = S5 正向形态（`ExternalReplaced → EdgeRetry → OrderApplied`），已 3/3 全负**。
- 仍待采样：「玩家队豁免生效」验收点（§3a 表格末行）。
- 已开卡处置：**CB-REPAIR-0004**（占位不开火 → 周期重占位，`runtime_failure`），**台账已于 2026-09-15 关闭（`COMPLETED`，`commit=238e31a5…`，证据 4 条 PASS，`repair-verify=ALL CONSISTENT`）**：r1 `723b457` → 质检 t8 = needs_revision（R4-1）→ r2 `bc8449a` → **Astra 判 FAIL（P1 周期重占位）** → r3 `395fe13` → **质检 t12 = pass-with-runtime-debt（双向独立复现，见 §3d）**；Astra 2026-09-15 裁定其在关闭条件上成立（裁定 7），用户授权后关闭。§3c 数据已按实测更正（699 = 425 + 窗口内 0 + 其后 274）。
- **已裁定/已登记（Astra 2026-09-15 复审）**：**J-2** 已裁（实现忠实、无更高规范硬冲突；「坏窗口」定义为「本暂停期两次重取耗尽且无真实选中」，见 §3d 与 §4.5）且机制已由 §3l 观测确立；**S5 选①**（接受离线证据 + 本轮机制观测，停止等正样本）；**条件 5 判 PASS**（不推出 CPS-4/GO）；**归因口径选 A**（只改文档口径，落点见 `docs/cps3r-routea-r3-sampling-record-2026-09-14.md` §2 与 `docs/knowledge/CURRENT_STATE.md`）；**覆盖恢复语义** 另立设计决策卡（DEFERRED）。低危未修：J-3、J-4（列 P2，不阻断 CB-REPAIR-0004）。
- 未定论：引擎侧「为何停止选中」（Inferred，建议并入下轮采样最小观测）；**下轮采样验收点**见 §3c 末行（部署与采样排在 Astra 回签之后）。
- 辅助工具：`MBmod/CombatAI/tools/routea-rejoin-check.ps1`（Rejoin/场景条件只读体检）。
