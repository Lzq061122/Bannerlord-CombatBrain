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

∴ Route A 只能武装「非玩家队 + 无领主所有者 + 无玩家兵员 + AI 控制」的编队（实测为 `team=1`）；而玩家在 UI 上无法对非玩家队编队下令 → 该动作不可达。**不得把场景 3 记为 FAIL，也不得记为 PASS；正确状态是 N/A（口径不可执行）**。

**替代的可达路径（覆盖原意图「外部接管时 Route A 不得抢回」）**：

| 替代项 | 状态 |
|---|---|
| 外部 setter 覆盖当前命令 | 已有运行期样本（场景 1：`ExternalReplaced → EdgeRetry → OrderApplied`） |
| 编队失效 / 清空 | 已观察（`Exit(EmptyOrInvalid)`，场景 1、2） |
| 模式 / 开关关闭 | 已通过（`Exit(Disabled)` + `Restore`，场景 2） |
| **玩家队豁免生效（新增验收点）** | 待跑：整场日志中**玩家队编队的 `[RouteA]` 行恒为 0**，且 Route A 武装对象的 `identity` **永不属于玩家队** |

**附带记录（口径分歧，冻结令下不改代码）**：其它 7 个模块（Protect / Brace / Cavalry / Skirmish / Morale / FormationSlot / FormationControlLease）的玩家队豁免均受 MCM 开关 `CombatBrainSettings.PlayerTeamExemptionEnabled` 约束（可关闭），**Route A 完全不看该开关**（上列两处均无条件）。属 Descriptive 观察，是否收敛口径待 CPS-4 / 后续裁决，本文件不作规范结论。

## 3b. 场景 4（Rejoin）：**N/A —— 实战两条件不共存**（用户裁决 2026-09-13）

**代码条件**（Rejoin 只有一条产生路径）：
1. `FormationCommandPolicy.cs:156-157`：仅当 `formationMission == SupportMainEffort` **且** `phase == Deployment` 才映射为 Rejoin（其余映射 Advance/Hold）
2. `RouteAOrderAdapter.cs:224-241`：MainBody 有效（`strength>0 && confidence>0`）+ **非贴身接敌**（已知敌编队 >12m，`RouteARejoinContactMeters`）+ MainBody 点在边界内 + 起终点 NavMesh 有效且**路径距离可算**
3. `RouteAControl.cs:100`：已选 Rejoin 但校验失败 → `Exit(NavigationFailed)`，不落地任何命令

**实测（不可达判定依据）**：
- 三场 E 模式（`4c6212a1` / `ba84a11b` / `a408fc92`）：`Rejoin` 意图 **0 次**、落地 **0 次**、`NavigationFailed` **0 次**
- 全日志 ~35 场：`phase=Deployment` 均由游戏自动结束，时长 **5–20s**（绝大多数 5–6s，最长 21s）——**玩家停等无法有效延长该窗口**（实测两处 20–21s 与 5–6s 并存，与是否等待无稳定相关）
- 被武装编队在部署期内恒为 `mission=Reserve`（`ReserveClassifier`：未接敌 + 非主动任务 + 距 MainBody ≤30m → Reserve → 意图 Hold）：实测 `13:53:13.777`、`01:11:08.778` 两个部署窗口内均为 `Reserve`
- ∴ `SupportMainEffort` 样本（全部出现在 Engage）与 Deployment 窗口**时间上不重叠**

**结论**：Rejoin 在当前代码与游戏参数下**实战不可达**；不得记为 FAIL，也不得记为 PASS，正确状态是 **N/A（口径不可执行）**。原清单该条（Astra 提交物 §6「Rejoin 触发场景：目标=MainBody、导航失败路径不执行」）无运行期证据支撑，属未验证项。

**替代可达路径（覆盖"目标/MainBody/导航校验"的相邻语义）**：

| 替代项 | 状态 |
|---|---|
| Route A 意图→命令落地链（Hold/Advance）与 MainBody 态势 | 已有样本（场景 1、2） |
| 编队清空/失效退出 | 已观察（`Exit(EmptyOrInvalid)`） |
| 开关/模式关闭退出 | 已通过（`Exit(Disabled)`，场景 2） |
| 增援 / 对象身份隔离 | 待跑（场景 5） |
| Rejoin 分支本身 | **无运行期样本**；若日后需验证，须先具备可观测性或调整触发条件（代码变更，冻结令下需先裁决） |

**工具**：`MBmod/CombatAI/tools/routea-rejoin-check.ps1`（只读日志，一条命令复现上述判定：部署阶段区间、mission 时间线、条件重叠判定、Rejoin/NavigationFailed 计数与结论）。

## 4. 本轮未证明（运行期债，不得据本文件判 GO）

1. 场景 5（增援·编队清空·拆分）**未跑**；场景 3（玩家接管）、场景 4（Rejoin）已分别经代码与实测核实**不可达**，改判 **N/A**（见 §3a、§3b）。
2. **Rejoin 分支无任何运行期样本**（当前不可达），其导航校验失败路径（`NavigationFailed`）同样无样本。
3. F-2 语义分支「重试已用后再次 `replaced=true` → 立即 `Release(ExternalOverridePersisted)`」**无运行期样本**（两场 `replaced` 分别为 1 / 0，未出现第二次 `replaced=true`）。
4. `replaced=false` 路径（不释放、不消耗重试、仅计数）只被观测为计数（`externalWhileActive=3` / `12`），其对所有权与抖动的影响无运行期证据。
5. F-5（Release 位于 Harmony postfix 栈内改权重）无运行期证据；H-2（E 模式帧成本须按「RouteA + 观测」复合口径评定）未评定。
6.「玩家队豁免生效」验收点（§3a 表格末行）尚未采样。
7. CPS §14.1 **条件 5 仍未满足**；Astra 复审未回签。

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

- 已登记场次：场景 1（重跑，PASS）、场景 2（热关闭，PASS）。
- 已裁决不可执行：场景 3（玩家接管）→ **N/A**（§3a）；场景 4（Rejoin）→ **N/A**（§3b）。
- 待跑场次：**场景 5（增援·编队清空·拆分）**，以及 §3a 的「玩家队豁免生效」验收点；跑完后本文件续写并按 `docs/evidence/INDEX.md` §5 更新指针。
- 辅助工具：`MBmod/CombatAI/tools/routea-rejoin-check.ps1`（Rejoin/场景条件只读体检）。
