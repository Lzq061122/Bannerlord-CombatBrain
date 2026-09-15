# CPS-4 运行期补测执行清单（需用户进游戏，2026-09-15）

> 用途：CPS-4 剩余 **3 场**实机证据（条件 6 / 10 / 12）的**可照做清单**——每场写明：怎么打、开局检查、操作步骤、判定命令、通过标准、失败判读。
> 配套：`docs/cps4-hardgate-checklist-2026-09-08.md`（硬门表）、`MBmod/CombatAI/tools/cps4-gap-log-audit-2026-09-15.md`（条件 7/9/11 的既有日志审计结论）。
> **前提（每场均须满足，否则该场不计证据）**：`Cps3Mode = E_RouteA` 且 `Cps3ObserverEnabled=true`；开局出现 `[SetOrderObserver] 观测已挂载（Harmony patch OK, …, prefixes=3）`；出现 `[RouteA] run=` 行；**不使用快进**。

---

## 0. 通用准备与收场

```powershell
# 打之前：确认部署的是哪份 DLL（r3 = 生产语义）
Get-FileHash 'E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\CombatBrain\bin\Win64_Shipping_Client\CombatBrain.dll' -Algorithm SHA256
# 打完：把 %TEMP%\CombatBrain.log 交回（Captain 负责归档 + 计算 SHA256 + 判读）
Copy-Item "$env:TEMP\CombatBrain.log" 'E:\MBmod\CombatAI\logs\CombatBrain-2026-09-__-cps4-condNN.log'
```

- 每场结束后**先别关游戏**，确认 `[RouteA] run=` 汇总行已写出（每 5s 一行）。
- 若中途切换了 MCM 模式（离开 E），该场按「已结束」处理，另起一场。

---

## 1. 条件 6 —— 玩家手动接管：CB 1 逻辑拍内停止且不抢回

| 项 | 内容 |
|---|---|
| **为何必须实机** | 「玩家真实下令」无法离线模拟；当前无该形态的运行期样本（热关闭/释放已有 PASS 证据，本项**专指手动接管**） |
| **场景** | 自定义战斗：**200 vs 200，双方步兵为主**，普通野战，无快进 |
| **前置判断** | 开战后先让 Route A 接管一段时间，确认 `applied ≥ 1`（否则本场无被接管对象，不计证据） |
| **操作步骤** | ①让 Route A 正常工作 ≥30s；②**手动给被 CB 接管的编队下令**（选中该编队 + 右键指定移动/攻击）；③观察 10s；④重复 2–3 次（可在战斗中段、尾声各来一次） |
| **判定命令** | `Select-String $env:TEMP\CombatBrain.log -Pattern 'run=<RUN_ID>' \| Where-Object { $_.Line -match 'event=(Exit\|Restore\|OrderApplied\|ExternalAfterExit)\|reason=PlayerOwned' }` |
| **通过标准** | ①出现 `event=Exit … reason=PlayerOwned`（或 `LostAIControl`）**且紧随 `event=Restore`**；②该编队此后**不再出现** `source=CombatBrainRouteA` 的 `OrderApplied`（不抢回）；③`restoreFailures=0` |
| **失败判读** | 出现「玩家下令后仍有 `source=CombatBrainRouteA` 的 `OrderApplied`」= **不通过**（抢回），记 finding 并立卡 |
| **附加（可选）** | 若同时勾选 `PlayerTeamExemptionEnabled=false` 再打一场，可顺带取 `CB-REPAIR-0008`（豁免开关一致性）的**现状**样本——**但这是缺陷取证，不是修复验证** |

---

## 2. 条件 10 —— 攻城 fail-open：不支持任务不激活任何接管

| 项 | 内容 |
|---|---|
| **为何必须实机** | 攻城 `MissionType` / `IsFieldBattle=false` 的运行期路径无样本（离线断言已有） |
| **场景** | **攻城战**（进攻或防守皆可），规模不限，无快进 |
| **前置判断** | 开局仍应出现 `[SetOrderObserver] 观测已挂载`（观测只读、与任务类型无关）；**但 Route A 不应落地任何命令** |
| **操作步骤** | 正常打一场攻城（≥120s 即可），不需要额外操作 |
| **判定命令** | `Select-String $env:TEMP\CombatBrain.log -Pattern '\[RouteA\] run=\|event=OrderApplied\|ControlLease' \| Select-Object -Last 30` |
| **通过标准** | ①`[RouteA]` 汇总中 `applied=0` 且 `selected=0`（Route A 未获选中/未落地）；②无 `source=CombatBrainRouteA` 的 `OrderApplied`；③`[ControlLease] summary active=0` 全程（租约不 Acquire）；④无异常行（`Exception` 0） |
| **失败判读** | 任一 CB 源在攻城场落地命令 = **不通过**（fail-open 破口），立即记 finding 并立卡 |

---

## 3. 条件 12 —— 当前控制面 500 人性能复测

| 项 | 内容 |
|---|---|
| **为何必须实机** | 历史 500 人 `avgAi=0.07ms` 属**旧控制面**；当前控制面只有 200 人实测 |
| **场景** | 自定义战斗 **500 vs 500**（或 1000 总量），普通野战，无快进，持续 ≥180s |
| **前置判断** | 开局确认 `Cps3Mode = E_RouteA` + 观测挂载；确认出现 `[Bench]` 行 |
| **操作步骤** | 打满 3 分钟即可，不需要额外操作（可让两边自由交战） |
| **判定命令** | `Select-String $env:TEMP\CombatBrain.log -Pattern '\[Bench\]' \| Select-Object -Last 20`；另取 `[RouteA] run=` 汇总末行看 `weightFlap/restoreFailures` |
| **通过标准** | ①`[Bench]` 各段（`coordination` / `executor` / `lease` / 观测）有 500 人样本且总开销可解释；②`avgAi` **< 5ms**；③`Exception` 0；④无「同一秒内多次落地」迹象（`OrderApplied` 间隔合理） |
| **失败判读** | `avgAi ≥ 5ms` 或某段出现量级跳变 = **不通过**，必须给出开销归因（哪段、为何）后才能进 GO 报告 |
| **禁止** | 用诊断衍生构建（`a24e921`）的性能数据替代本场 |

---

## 4. 条件 7 / 9 / 11 —— 本轮不需要你操作（审计结论）

| 条件 | 结论（详见 `tools/cps4-gap-log-audit-2026-09-15.md`） |
|---|---|
| **7** | 既有日志**不能**证明「两评估周期内恢复」的**时限**，也没有租约释放/清零事件 ⇒ 需极小的只读诊断增强（见 §5）+ 一场普通野战（**可与条件 6 同场**） |
| **9** | 既有日志可证 `Restore` 配对与 MissionEnding，但对象替换 / Index 复用**不可观测** ⇒ 建议并入条件 6/10 之外的一场「增援/清空 + 拆分」，或由离线断言 + 诊断行闭合 |
| **11** | 既有日志**可以**做确定性统计（基于 `revision` 序列 + `OrderApplied` 时间戳，判定「≥2s 无 revision 变化窗口内是否出现语义相反落地」）⇒ **我离线写统计器，若样本足够即不进游戏** |

---

## 5. 需授权的一项代码改动（只读诊断，极小）

> 目的：让条件 7 的「时限 / 租约清零」可观测。**冻结令下必须先立卡、由你授权**，本清单只登记需求，不代表已批准。

| # | 建议新增（只读） | 用途 |
|---|---|---|
| 1 | 租约 Release/Clear 时输出 `[ControlLease] event=Release reason=<...> mission=<...> session=<...>` | 条件 7「租约清零」可判定 |
| 2 | Route A `Restore` 完成时输出 `event=RestoreCompleted t=<mission time>` | 条件 7「恢复时限」可判定 |

- 风险面：纯增量只读日志（不改变控制流），与 `CB-REPAIR-0005/0006` 同型做法；仍须走卡 + 离线断言 + 静态 setter 扫描（不得新增调用点）。
- 若你不想再动代码：条件 7 只能以「Release 后出现 `ExternalAfterExit` + `restoreFailures=0`」作**部分证据**记入 GO 报告的运行期债。

---

## 6. 打完后的交接要求

每场请回交：①日志文件（或 `%TEMP%\CombatBrain.log` 原文件）②场次概况（哪张图、是否攻城、兵力、有无快进、有无手动操作）。我负责：归档 + SHA256 + 按上表判定 + 写进 `docs/cps3r-routea-runtime-scenarios-*.md` 与硬门表，并更新 `CURRENT_STATE`。
