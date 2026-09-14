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
  - **r3 派单 = `prompts/active/CombatBrain-CB-REPAIR-0004-r3-周期重占位返修派单-2026-09-14.md`（registry CB-PROMPT-0041，ACTIVE）**：要求重武装只能由**具名、离散、可解释、有界**的条件触发（不得再把原始 `situationSignal` 任意变化当作战术变化），并补 6 项组合回归 + 反向验证；**台账 CB-REPAIR-0004 保持 `IN_PROGRESS`**，不部署、不采样、不推进部署基线。
- **未定论部分**：引擎侧「为何停止选中」为 Inferred（窗口内每 0.5s 恰 1 次原生 Movement 写 ⇒ FormationAI 节拍仍在，胜出者非 Route A Behavior）；建议并入下轮采样的最小观测，不单独立卡。
- **下轮采样验收点（部署与采样经用户裁决排在 Astra 回签之后）**：`ExternalReplaced` 后 **≤10s** 应出现 `Exit(reason=SelectionTimeout)`；超时释放后普通执行器须获得**持续**（非单帧）Movement 窗口；`SelectionTimeout` 触发频率理想为 0 或仅出现于真实覆盖失效处。

## 4. 本轮未证明（运行期债，不得据本文件判 GO）

1. **可执行的五场场景已全部执行完毕**：场景 1、2、5 = **PASS**；场景 3（玩家接管）、场景 4（Rejoin）与场景 5 子项（拆分）在**本次采样口径下不适用**（§3a/§3b/§3c），其中 **Rejoin 分支仍未验证**（不得声称结构上永久不可达）。这不等于"条件 5 已满足"——条件 5 的判定权在 Astra 复审与用户，本文件不判 GO。
2. **未取得样本的分支**：① "新 Formation 对象身份隔离"（对象未销毁重建）；② **Rejoin 分支及其 `NavigationFailed` 路径**（本次采样不适用，未验证）；③ F-2「重试已用后再次 `replaced=true` → 立即 `Release(ExternalOverridePersisted)`」；④ §3c 的 `edgeRetries=0` / 占位不开火——根因已定位，处置**仍在返修中（r3）**。
3. `replaced=false` 路径（不释放、不消耗重试、仅计数）被大量观测（`externalWhileActive` 3 / 12 / **1780**），但其对所有权与抖动的实际影响仍无定论（§3c 的占位不开火现象正属此域）。
4. F-5（Release 位于 Harmony postfix 栈内改权重）无运行期证据；H-2（E 模式帧成本须按「RouteA + 观测」复合口径评定）未评定——另注意场景 5 采样使用了快进与低 BattleSize，其数字不得与其他场次混比。
5.「玩家队豁免生效」验收点（§3a 表格末行）尚未采样。
6. CPS §14.1 **条件 5 仍未满足**（Astra 复审未回签；本文件不判 GO）。

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
- 本次采样不适用（分支仍未验证）：场景 3（玩家接管，§3a，口径属产品/规范裁决项）、场景 4（**Rejoin，仍未验证**，§3b）、场景 5 子项「拆分」（§3c）。
- 仍待采样：「玩家队豁免生效」验收点（§3a 表格末行）。
- 已开卡处置：**CB-REPAIR-0004**（占位不开火，`runtime_failure`）——**仍在返修中（r3，派单 CB-PROMPT-0041）**，台账 `IN_PROGRESS`；r1 `723b457` → 质检 t8 needs_revision（R4-1）→ r2 `bc8449a` → **Astra 判 FAIL（P1 周期重占位）**；§3c 数据已按实测更正（699 = 425 + 窗口内 0 + 其后 274）。
- 未定论：引擎侧「为何停止选中」（Inferred，建议并入下轮采样最小观测）；**下轮采样验收点**见 §3c 末行（部署与采样排在 Astra 回签之后）。
- 辅助工具：`MBmod/CombatAI/tools/routea-rejoin-check.ps1`（Rejoin/场景条件只读体检）。
- 辅助工具：`MBmod/CombatAI/tools/routea-rejoin-check.ps1`（Rejoin/场景条件只读体检）。
