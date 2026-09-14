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
