# CB-REPAIR-0004 r3 受控部署与运行期采样记录（2026-09-14）

> 依据：Astra 2026-09-14 离线回签「离线 PASS、允许进入受控部署采样」。
> 状态：`395fe13` **允许部署测试**；**尚未取得运行期验收**；台账 CB-REPAIR-0004 保持 `IN_PROGRESS`、`review_state=PASSED`（离线通过）。
> 约束：**条件 5 仍未满足；CPS-4/GO 仍不批准；当前无必须继续修改的阻断代码**；最终 GO 前仍须裁决玩家豁免口径。
> 本文件是本次采样的**唯一绑定表**：跑完把 run id / 日志交回 Captain 登记；未登记 `run/session` 的采样不算证据。

---

## 1. 绑定信息（进游戏前须完整）

| 项 | 值 |
|---|---|
| 代码 commit | **`395fe13`**（CB-REPAIR-0004 r3） |
| 部署时 repo HEAD | `ae533ec`（仅含 `395fe13` 之后的文档提交，无代码差异） |
| 仓库构建产物 SHA256 | `7E5FC25A0EF0559D718B0D5E42994141846291501E3342F94E6C0B1F713FD8D6` |
| 部署目标 DLL SHA256 | **`7E5FC25A0EF0559D718B0D5E42994141846291501E3342F94E6C0B1F713FD8D6`**（= src，MATCH=True） |
| 部署时间 / 游戏进程 | **2026-09-14**（deploy.ps1 EXIT=0，ModuleData 已同步）；部署前硬门：`Get-Process Bannerlord` 为空 ✓ |
| MCM 配置 | `Cps3Mode = E_RouteA`；**`Cps3ObserverEnabled = true`**（G-1 硬约束：无观测 Route A 不执行）；RouteA 专用开关按 E 模式自动武装 |
| 战斗配置 | 自定义战斗、**200v200 步兵野战**；**BattleSize 建议恢复为 6**；**本轮不要用游戏快进**（前一轮快进使响应延迟不可作证据） |
| run / session | **`c81c4f5e6d7d47bba5a81b7174eb1f0e`**（session=1；2026-09-14 10:59:57 → 11:05:24） |
| 战斗规模 / 时长 | `[Bench] agents=401`（200v200）、`BattleDuration=314.38s`、无快进痕迹 |
| 观测确认 | `10:59:52.071 [SetOrderObserver] 观测已挂载 prefixes=3` ✓ |

> **无效场次记录（2026-09-14 12:18–12:32，共 5 场：269.84s / 239.82s / 185.41s / 242.4s / 137.13s / 272.92s）**：这几场**不计入任何证据**——日志中 `[RouteA]` / `[RouteAEvent]` 行数为 **0**（最后一条 Route A 行停在 12:18:17.477，属样本②的尾巴），而同期观测层在跑（`[Cps2Observation]` 97 条、`[Obs]` 385 条、`[ControlLease]` 997 条）→ 说明 `RouteAExecutionEnabled` 的四个条件里 **`Mode == E_RouteA`（或 RouteA 专用开关）这一项未满足**，Route A 根本没有启动。判定前置：**下一场必须在 MCM 把模式选为 `E_RouteA`**（选 E 会自动武装 RouteA 专用开关），并在开打后确认日志出现 `[RouteA] run=…` 行。

**采样前操作**：① 确认无 `Bannerlord` 进程后再部署；② 部署后核对 src/dst SHA 一致；③ 场次隔离由 Captain 侧用"日志字节偏移 + 新 run id"实现（无需用户改名日志）；④ 首查 `[SetOrderObserver] 观测已挂载 prefixes=3` 确认观测在位。

## 2b. 本轮采样结果（run=`c81c4f5e…`）

**事件链**：`t=0.550 Intent(Hold, rev=1)` → `t=10.550 Exit(reason=SelectionTimeout)` + `Restore(SelectionTimeout)` → 之后约 304s 全为 `ExternalAfterExit`（721 条原版 Movement 写）、**无再武装**。
**汇总（末）**：`selected=0 applied=0 replaced=0 externalWhileActive=0 edgeRetries=0 externalDuration=0.000s stableSoFar=0 intentChange=0 weightFlap=0 lastSelectedAt=never suspensionRetries=0 restoreFailures=0`；全窗口 4525 行**异常 0**。

| # | 结果 |
|---|---|
| S1 连续变号不重取 | **PASS（强）**：释放后 304s 零再武装；`suspensionRetries=0` |
| S2 合格意图变化仅一次 `SuspensionRetry` | **本场未触发**（`intentChange=0`）→ 无样本，不判 fail |
| S3 `SelectionTimeout` + `Restore` | **PASS**：武装后**恰好 10.00s** 触发，`Restore` 紧跟，`restoreFailures=0` |
| S4 释放后执行器持续可用 | **间接 PASS**：释放后不再阻塞，721 条原版 Movement 写贯穿余场；本场 CB 侧无 Movement 提案样本 |
| S5 健康 `EdgeRetry`／归因／`RestoreFailures=0` | **部分**：`restoreFailures=0`、`weightFlap=0`、0 异常 ✓；本场无外部覆盖（`replaced=0`）→ edgeRetry 路径无样本 |

**待补样本**：①S2（暂停期间出现合格意图变化）②S5 的 edgeRetry 路径（需发生外部覆盖，形态参照 `run=ba84a11b`）。
**附加事实**：本场 Route A 未落地任何命令（`applied=0`，引擎从未选中其 Behavior）——即「占位不开火」成因；r3 把该状态限制为 10s 并令本场退役（符合 J-2 语义）。

## 2c. 第二个运行期样本（run=`6223ad9b…`）——健康路径 + 多轮生命周期

同部署（`395fe13` / `7E5FC25A…`）；`agents=601`、`BattleDuration=505.51s`、无快进、窗口 6668 行**异常 0**。
汇总（末）：`selected=746 applied=9 replaced=0 externalWhileActive=1 edgeRetries=0 stableSoFar=9 intentChange=3 weightFlap=0 lastSelectedAt=493.531s suspensionRetries=0 restoreFailures=0`。

| # | 结果 |
|---|---|
| 健康路径回归 | **PASS（强）**：`applied=9`/`stableSoFar=9`、`lastSelectedAt=493.531s`（交付窗口持续刷新）、`weightFlap=0`、`restoreFailures=0`、`unverified=0`、`dropped=0` |
| 生命周期恢复 | **PASS**：`Exit`×6（`LostAIControl`×5 + `EmptyOrInvalid`×1）**全部配对 `Restore`**；每次资格恢复后都能重新武装 |
| **意图真变化链** | Hold→Advance→Retreat→Hold（`intentChange=3`），**每次都 `OrderApplied` 落地** |
| S4 释放期间执行器可用 | **PASS（直接形态）**：释放期间原版写入正常（`ExternalAfterExit` 21 条），未占位阻塞 |
| S2 | **仍无样本**（本场未进入暂停、无 `SelectionTimeout`） |
| S5 edgeRetry | **仍无样本**（`replaced=0`，本场无外部覆盖） |

## 2d. 第三个运行期样本（run=`ce970979…`）——短样本，无新验收点

同部署；`13:01:10.447` 观测挂载；`BattleDuration=334.76s`，85 行 Route A 记录。
时间线：`t=0.536 Intent(Hold)` → `t=1.463 ExternalWhileActive×3`（非覆盖）→ `t=1.529 Exit(LostAIControl) + Restore` → **余场无任何 Route A 事件**。
汇总（末）：`selected=0 applied=0 replaced=0 externalWhileActive=3 edgeRetries=0 intentChange=0 lastSelectedAt=never suspensionRetries=0 restoreFailures=0`。

| # | 结果 |
|---|---|
| 生命周期 | `Exit(LostAIControl)` **配对 `Restore`** ✓、`restoreFailures=0` ✓ |
| S2 | **无样本**（未进入暂停：退出原因是 `LostAIControl` 而非 `SelectionTimeout`） |
| S5 edgeRetry | **无样本**（`replaced=0`；3 次外部写均为 `ExternalWhileActive`，按 F-2 不释放不消耗预算） |

## 2e. 第四个运行期样本（run=`d43c302e…`，1501 人 / 1365.66s 长局）——**S2 补齐**

同部署；窗口 15339 行异常 0。汇总（末）：`selected=61 applied=2 replaced=0 externalWhileActive=94 edgeRetries=0 externalDuration=649.652s stableSoFar=2 intentChange=4 weightFlap=0 lastSelectedAt=532.029s suspensionRetries=3 restoreFailures=0`；`Exit` = `SelectionTimeout`×4 + `LostAIControl`×2，**全部配对 `Restore`**。

| # | 结果 |
|---|---|
| **S2 合格意图变化仅一次 `SuspensionRetry`** | **PASS（首次取得样本）**：3 次 `SuspensionRetry` 各对应一次意图真变化（rev3→4、rev6→7、rev9→10），每次合格事件只授予一次 |
| **S1 连续变号不重取** | **PASS（进一步加强）**：`t=685→1179` **494 秒**持有资格却不重新武装（同期 94 次外部写），只在合格离散事件出现时重取一次 |
| **S3 `SelectionTimeout` + `Restore`** | **PASS（×4）**：四次均在武装/重取后 **10.000–10.006s** 触发并配对 `Restore` |
| 冷却门禁 | **实机确认**：冷却内意图变化（t=590.534）未授予重取 |
| 预算上界 | **实机确认**：第 2 次重取超时后不再重取（`MaxSuspendedReattempts=2`，符合 J-2「本场退役」） |
| S5 edgeRetry | **仍无样本**（`replaced=0`，94 次外部写全为 `ExternalWhileActive`） |

## 2f. 第五个运行期样本（run=`0496f7b6…`，1426 人 / 3809.58s 超长局）——S5 负样本

同部署；窗口 20147 行异常 0；本场武装对象 `f=1`。汇总（末）：`selected=295 applied=2 replaced=1 externalWhileActive=60 edgeRetries=0 externalDuration=962.490s stableSoFar=1 intentChange=2 weightFlap=0 lastSelectedAt=1114.587s suspensionRetries=2 restoreFailures=0`。

| # | 结果 |
|---|---|
| **S5 edgeRetry** | **负样本（关键）**：`t=1115.090 ExternalReplaced`（距上次落地 4.0s）→ **`EdgeRetry` = 0**：覆盖后 Behavior 再未被选中（`lastSelectedAt=1114.587`），armed 重试从未兑现；`t=1124.593`（Δ=9.5s）按期限 `Exit(SelectionTimeout)` + `Restore`。→ **S5 正向形态需"覆盖后 10s 内仍被选中"** |
| S1 | **再次加强**：`1171.8 → 2067.7` **895.9s 零再武装**（同期 60 次外部写） |
| S3 | 本场 3 次 `SelectionTimeout` 均 Δ≈10.0s，全部配对 `Restore` |
| 超长局稳定性 | 63.5 分钟：`weightFlap=0`、`restoreFailures=0`、`unverified=0`、`dropped=0`、异常 0 |
| 预算上界 | 第 2 次重取超时后不再重取（符合 J-2） |

**归档**：全量 `logs/CombatBrain-2026-09-14-full.log`（SHA256 `63ECC4A9…`）；切片 `logs/run-0496f7b6-20260914-longbattle.log`（SHA256 `585DCEAD…`，5194 行）。

## 2g. 第六个运行期样本（run=`4156cc25…`，一个 run 两场）——Rejoin 样本 + S5 负样本再现

同部署（`395fe13` / DLL `7E5FC25A…`，DLL mtime `09-14 09:34:23`，本 run 前无重部署）；`15:34:58.687` 观测挂载 `prefixes=3`；**无快进**（mission/墙钟 ≈ 1.0）；新窗口 1,430,342 bytes / 12,563 行**异常 0**。

> **run / session 语义**：`RunId` 为进程级 `static readonly`（`RouteAOrderAdapter.cs:95`），`session` 每场 mission 递增 → **一个 run 可跨多场**。此前样本 ②–⑤ 均 `session=1`。

| 场次 | 时间 / 时长 | 实测开战编队 | 汇总（末） |
|---|---|---|---|
| **session=1**（非申报场，混编） | 15:35:05–15:48:48 / `BattleDuration=770.9s` | Team 0 = 635（步158/射159/骑80/骑射158/骑80）、Team 1 = 616（步173/骑96/骑97/骑射250） | `selected=282 applied=11 replaced=0 externalWhileActive=3 edgeRetries=0 externalDuration=0.000s stableSoFar=11 unverified=0 dropped=0 intentChange=3 weightFlap=0 lastSelectedAt=657.527s suspensionRetries=0 restoreFailures=0` |
| **session=2**（**本次申报**：约 1020v1020 纯步/射各半） | 15:50:10–15:59:19 / `BattleDuration=548.99s` | Team 0 = **1016**（步507/射508/骑1）、Team 1 = **994**（步489/射505）；`agents=2032` | `selected=221 applied=1 replaced=1 externalWhileActive=18 edgeRetries=0 externalDuration=9.003s stableSoFar=0 unverified=0 dropped=0 intentChange=0 weightFlap=0 lastSelectedAt=325.030s suspensionRetries=0 restoreFailures=0` |

session=2 关键链：`t=320.027–325.030` 连续选中×11 → **`t=325.529 ExternalReplaced`** → `t=335.035 Exit(SelectionTimeout)` + `Restore`（Δ=9.507s 自覆盖 / 10.005s 自末次选中）→ `t=335.035–548.761` `ExternalAfterExit`×188，**零再武装**。`Exit`×2 全配对 `Restore`。

| # | 结果 |
|---|---|
| **Rejoin 分支** | **首次取得运行期样本（session=1）**：部署期（`15:35:00.695–15:35:05.285 phase=Deployment`，`Team 1 f=0 mission=SupportMainEffort`，`MB=(844,729) str=366 conf=0.59` 有效）→ `t=0.542 Intent(Rejoin rev1)` → `t=4.034 BehaviorSelected + OrderApplied(ot=1)`；`NavigationFailed=0`。**仅成功路径**，失败路径仍无样本 |
| **S5 edgeRetry** | **负样本（第 2 次）**：覆盖真实发生但 `edgeRetries=0`、覆盖后再未被选中（`lastSelectedAt=325.030 < 325.529`）→ 正向形态仍缺样本 |
| S1 | **PASS（再次）**：超时释放后 **213.7s 零再武装**（同期 188 次外部写） |
| S2 | **无样本**（session=2 暂停期 `intentChange=0`；session=1 无 `SelectionTimeout`） |
| S3 | **PASS**：Δ=10.005s（自末次选中）/ 9.507s（自覆盖），`Restore` 紧随 |
| S4 | **PASS（直接）**：释放后 188 次连续 `ExternalAfterExit` 跨越 213.7s |
| 健康路径（session=1） | `applied=11 / stableSoFar=11`、`lastSelectedAt` 至 657.527s、`Exit`×8 全 `LostAIControl` 且全配对 `Restore`、`weightFlap=0`、`restoreFailures=0`、异常 0 |
| 归因（新观察） | `[RouteAEvent] source=CombatBrainRouteA` 36 条 = 12 次落地×3，自洽；但 `[OrderTraceAgg]` 两场均 `CombatBrainRouteA=0/o0`、`[OrderTrace]` 31 条中 0 条。结构原因：`RouteAOrderAdapter.cs:33-36` 不调用 `CommandProvenance.Instance.Record`（唯一调用点 `FormationCommandExecutor.cs:147`）→ 需口径裁决，本轮不改码 |

**归档**：全量快照 `logs/CombatBrain-2026-09-14-full-1600.log`（52,159,044 bytes / 402,155 行，SHA256 `D6B80D36…`）；本 run 切片 `logs/run-4156cc25-20260914-twobattles.log`（488,112 bytes / 1594 行，SHA256 `54374180…`）。

**遗留缺口**：**S5 正向形态（`ExternalReplaced → EdgeRetry → OrderApplied`）**连续两场未取得；是否继续等样本或接受离线证据替代 → 交 Astra 裁定（交接件 §4-1）。

## 2h. 第七个运行期样本（run=`4e885a4e…`，健康局中途覆盖 → 全场断流）——S5 负样本第 3 次（最强）

同部署（`395fe13` / DLL `7E5FC25A…`）；`20:49:48.537` 观测挂载 `prefixes=3`；**无快进**（mission 346.7s / 墙钟 355.6s ≈ 0.975）；窗口 5,613 行**异常 0**。
战斗（观测值）：Team 0 = 635（步158/射159/骑160/骑射158）、Team 1 = 616（步173/骑96/骑97/骑射250）——**混编场**（同 §2g session=1 型）；`agents=2032`、`BattleDuration=347.43s`。

关键链：`t=5.536–220.029` **连续选中 379 次、落地 15 次**（健康）→ **`t=220.528 ExternalReplaced`**（覆盖前 0.5s 仍在选中）→ `t=230.030 Exit(SelectionTimeout)`+`Restore`（Δ=9.507s 自覆盖 / 10.001s 自末次选中）→ `t=240.035 SuspensionRetry`+`Intent(Advance rev17)`（第 1 次重取）→ `t=250.039` 超时 → `t=280.705 SuspensionRetry`+`Intent(Hold rev18)`（第 2 次重取，达上限）→ rev19/20/21 冷却内未授予 → `t=290.706` 超时。
汇总（末）：`selected=379 applied=15 replaced=1 externalWhileActive=61 edgeRetries=0 externalDuration=285.233s stableSoFar=14 unverified=0 dropped=0 intentChange=15 weightFlap=0 lastSelectedAt=220.029s suspensionRetries=2 restoreFailures=0`；`Exit`×6（`LostAIControl`×3 + `SelectionTimeout`×3）全配对 `Restore`。

| # | 结果 |
|---|---|
| **S5 edgeRetry** | **负样本（第 3 次，最强）**：覆盖发生在**每 0.5s 连续选中的健康局**中；覆盖后 `BehaviorSelected`=**0**，**持续 127.3s 至战斗结束**（`lastSelectedAt=220.029s` 冻结），期间 CB 两次重取（各 10s 窗口）**引擎仍未回调一次** |
| **S2** | **PASS（第 2 次样本）**：两次 `SuspensionRetry` 各对应一次具名意图真变化（rev16→17、rev17→18），每次仅授予一次；冷却内 rev19/20/21 与达上限后的变化一律未授予（`suspensionRetries=2` 恰达上限） |
| S1 | **PASS（再确认）**：连续变号（rev19/20/21，间隔 1–2s）零重取 |
| S3 | **PASS（×3）**：三次 `SelectionTimeout`（覆盖后 9.507s、重取后 10.0s ×2）全部配对 `Restore` |
| S4 | **PASS（直接；口径更正）**：首次超时释放后 `ExternalAfterExit`×190，分布 230.030→347.218（总跨度 117.2s），**但区间内含 CB 两次重新持有所有权**（240.035→250.039、280.705→290.706 各约 10.0s）⇒ 实得三段连续窗口 ≈**10.0s / 30.7s / 56.5s（合计 ≈97.2s）**，不得写作"单一 117.2s 连续放行窗口" |
| 健康路径 | **PASS（强）**：覆盖前 `applied=15 / stableSoFar=14 / selected=379`、`weightFlap=0`、`unverified=0`、异常 0；3 次 `LostAIControl` 后均恢复接管 |
| 归因 | 复现 §2g 观察（第 4 次）：`[OrderTraceAgg] CombatBrainRouteA=0/o0`（applied=15） |
| **新增判别事实（口径更正）** | `ExternalWhileActive` **覆盖前 3 次（全在部署拍 5.295）/ 覆盖后 58 次**（221.0295→290.5281，**分布非均匀：含 11.0s 与 31.0s 两处间隔，最大间隔 31.0s**）⇒ 只能报告"覆盖后外部 Movement 写入显著增加"，**来源仍是 `ExternalUnknown`，不得写成"已证明原生持续胜出"**；全场 `BehaviorSelected` 仅两处 >3s 空档（均在 `LostAIControl` 窗口 19.9s / 5.8s）⇒ 127.3s 静默**不是节拍问题** |

**归档**：全量 `logs/CombatBrain-2026-09-14-full-2056.log`（53,009,560 bytes / 407,767 行，SHA256 `7223BB15…`）；切片 `logs/run-4e885a4e-20260914-mixed-battle.log`（403,826 bytes / 1330 行，SHA256 `E3E60C97…`）。

**结论**：S5 正向形态 **3/3 全负**（§2f/§2g/§2h），且本次证明"覆盖后断流"与"是否健康选中"无关——覆盖前一秒引擎仍在正常选中，覆盖后立刻永久停止。**是否继续等样本或接受离线证据替代 → 交 Astra 裁定**；机制侧判定改由 CB-REPAIR-0005 只读探针（已实现、离线验收完成、**未部署**）承接。

**口径更正（Astra P1 复审，2026-09-14 晚）**：① 上文 S4 的 `117.2s` **不得**写作"单一连续放行窗口"（区间内 CB 两次重新持有所有权，实得三段窗口 ≈10.0s/30.7s/56.5s，合计 ≈97.2s）；② 58 次外部写**分布非均匀**（含 11.0s 与 31.0s 间隔，最大 31.0s），来源仍是 `ExternalUnknown`，**不得**写作"已证明原生持续胜出"——"原生行为胜出"仍是待 `[RouteASilence]` 探针确认的推断。

## 2. 本轮采样验收点（Astra 回签要求的最小集）

| # | 验收点 | 判定方法 | 通过标准 |
|---|---|---|---|
| S1 | **连续变号不重取** | 零 `BehaviorSelected` + 每拍态势变化（自然局中体现为被武装编队长期不被选中） | `suspensionRetries=0`；超时释放后**长时间不重新武装**（不再每 10s 重取） |
| S2 | **合格意图变化仅一次 `SuspensionRetry`** | `event=SuspensionRetry` 计数 | 出现**恰好一次**，且伴随可见的意图变化；不得出现第 2 次以上周期重取 |
| S3 | **零选中后按期限 `SelectionTimeout` + `Restore`** | `event=Exit reason=SelectionTimeout` 与随后 `event=Restore` | `ExternalReplaced`（或被选中停止）后 **≤10s** 出现；`Restore` 紧随、`restoreFailures=0` |
| S4 | **普通 Movement 持续恢复** | 该编队执行器写入 | 释放后普通执行器获得**持续**（非单帧）Movement 写窗口，之后原版正常接管（`ExternalAfterExit`） |
| S5 | **健康路径与归因** | `[RouteA]` 汇总 + `[OrderTraceAgg]` | 正常选中场景 `edgeRetries` 语义正确（=授权次数）；`OrderTraceAgg` 归因与执行器一致；`restoreFailures=0`、无异常行 |

**判定命令**：

```powershell
# 场次关键事件
Select-String $env:TEMP\CombatBrain.log -Pattern 'run=<RUN_ID>' |
  Where-Object { $_.Line -match 'event=(OrderApplied|ExternalReplaced|EdgeRetry|SuspensionRetry|Exit|Restore)\b' }
# 汇总（new: suspensionRetries / lastSelectedAt）
Select-String $env:TEMP\CombatBrain.log -Pattern '\[RouteA\] run=' | Select-Object -Last 3
# Route A 相关实时盯屏
Get-Content $env:TEMP\CombatBrain.log -Wait -Tail 200 | Select-String -Pattern '\[RouteA'
```

## 3. 采样后动作（Captain）

1. 记录 `run/session`、MCM 与战斗配置、日志归档路径到本表；
2. 在 `docs/cps3r-routea-runtime-scenarios-2026-09-13.md` 新增 §3e：本场逐项（S1–S5）判定与证据；
3. 更新 `docs/knowledge/CURRENT_STATE.md`：运行期证据、条件 5 状态（**仍不判 GO**，由 Astra/用户裁）；
4. 台账 CB-REPAIR-0004 仍保持 `IN_PROGRESS`——**只有运行期验收齐备 + Astra 复核通过才可 `repair-close`**。

## 4. 非阻断待整理（P2，Astra 标注）

- P2-1：**重试事件 revision 配对**（`SuspensionRetry` 事件与 revision 的配对关系待整理）；
- P2-2：**t12 源码不可重建**（质检 harness 的 r3 侧源码抽取/重建方式待整理）。

两项**不阻断本轮采样**，在后续证据口径整理中处理（不得借此在本轮改代码）。
