# CombatBrain — CPS-3R Route A 复审汇报件（2026-09-14）

> 用途：交给 Astra 做**第二轮复审 / 条件 5 判定**。本件自包含：变更、离线证据、运行期证据、未证明项与待裁决问题。
> 性质：DESCRIPTIVE（scoped claims）。现行验证态唯一权威 = `docs/knowledge/CURRENT_STATE.md`；本件不判 GO。
> 上一轮：`docs/cps3r-routea-astra-review-submission-2026-09-08.md`（当时结论 READY FOR ASTRA RE-REVIEW，未部署 DLL）。

---

## 0. 一句话状态

上一轮提交后，共落地 **3 张返修卡 + 1 项观测硬约束**，已完成**部署**，并完成**可执行的运行期场景 1 场重跑 + 场景 2 + 场景 5**（均 PASS）；
另有两场场景经代码与实测核实**不可达**（玩家接管、Rejoin）已改判 **N/A**；
新发现 1 处同域 discrepancy（「占位不开火」）**已开卡 CB-REPAIR-0004，正在处理**。
**条件 5 是否满足，交由 Astra 判定；本机不判 GO。**

代码基线：`9a2afe4`（= `CURRENT_STATE.last_verified_commit`，其后 HEAD 仅文档提交）；分支 `main`；功能冻结令仍在生效。

## 1. 变更清单（均已过独立质检门）

| 卡 / 条目 | commit | 内容 |
|---|---|---|
| CB-REPAIR-0001 | `70c9a11` | 外部 setter 覆盖 Route A 当前命令时通知 `RouteAControl`；首次覆盖只允许**一次**边沿重试；外部继续覆盖 → 立即 `Release(ExternalOverridePersisted)` + 恢复原权重；汇总新增 `externalWhileActive / edgeRetries / externalDuration` |
| CB-REPAIR-0002 | `a219c5b` | F-1：`externalDuration` 口径统一为**最后一次外部覆盖时间结算**（不再用退出时刻）；F-2：覆盖释放语义**显式三分支**（`replaced=true` 未用重试→arm；重试已用后再 `replaced=true`→立即 Release；`replaced=false`→不释放、不消耗重试、仅计数） |
| CB-REPAIR-0003 | `9a2afe4` | G-1：`RouteAExecutionEnabled() &&= ObserverEnabled` —— E 模式**无观测不执行**（fail-safe，防"看似执行、覆盖恢复未验证"的假样本） |
| 测试指南 §0a | `f6634f7` | E 模式采样前置写入文档：ObserverEnabled 硬约束 + 帧成本须按「RouteA+观测」复合口径评定 |
| 运行期证据登记 | `97fbcd9`→`3d75070` | `docs/cps3r-routea-runtime-scenarios-2026-09-13.md`（场景 1/2/5 证据、场景 3/4 N/A 裁决、待查 discrepancy） |

台账：`prompts/repairs/registry.json` 中 CB-REPAIR-0001/0002/0003 均 `COMPLETED` + `review_state=PASSED`（各带 commit 与测试证据）；CB-REPAIR-0004 `IN_PROGRESS`。

## 2. 离线与部署证据

| 项 | 结果 |
|---|---|
| Release 构建 | 每次改动 `--no-incremental` **0 错 0 警** |
| OfflineTests | **1241 PASS / 0 FAIL**（基线演进 1174→1227→1233→1239→1241，**只增不减**；静态 Check 站点 1072） |
| 静态 setter 扫描 | 三域 setter 调用点 **4 = 4 无新增**（`FormationCommandExecutor`=3 + `RouteAOrderAdapter`=1） |
| 部署 | `deploy.ps1` 于游戏进程退出后执行；源/部署 DLL **SHA 一致**（`A39AC964…`），ModuleData 同步 |
| 红线 | 三张卡全程未部署（部署在卡闭环后单独执行）、未跑运行期采样、未触碰 `CommandProvenance` / 执行器 / 适配器 / Mission 逻辑 / Settings |

## 3. 运行期证据（采样前置：`Cps3Mode=E_RouteA` + `Cps3ObserverEnabled=true`）

### 场景 1 — E_RouteA 采样（重跑）：PASS

| | 修复前 `run=4c6212a1…`（09-10 13:13） | 修复后 `run=ba84a11b…`（09-10 13:53） |
|---|---|---|
| 中途外部覆盖 | t=410.53 `ExternalReplaced`（外部 orderType=5） | t=245.53 `ExternalReplaced`（外部 orderType=5） |
| 覆盖后行为 | **直到 t=452.89 战斗结束未再落地**（缺陷） | **t=250.54 `EdgeRetry` → `OrderApplied`（5.00s 内重落地一次）** |
| 汇总 | `applied=3 replaced=1 stableSoFar=2`（**无**新字段 = 修复前构建） | `applied=7 replaced=1 externalWhileActive=12 edgeRetries=1 externalDuration=247.913s stableSoFar=6 restoreFailures=0` |

### 场景 2 — 战斗中热关闭：PASS

`run=a408fc92…`（09-13 01:11–01:17，战斗 362.85s）：t=208.9818（全场 58% 处）`Exit reason=Disabled` + `Restore reason=Disabled`；关闭后 344 次 `ExternalAfterExit`（原版接管）、**无任何 `OrderApplied`**（未重新武装/未抢回）；`restoreFailures=0`。

### 场景 5 — 增援 / 编队清空：PASS（带限定）

`run=b0e2e0a4…`（09-14 00:01–00:17，跨 3 个 mission）：**6 次 `Exit` 全部配对 `Restore`**（`LostAIControl`×3、`MissionEnding`×2、`EmptyOrInvalid`×1），每次退出后重新 `OrderApplied`；`identity` 恒为 1（同一 `Formation` 对象幂等重挂载）；`restoreFailures=0`、`weightFlap=0`；增援波次确实到场（`[Bench] agents` 反复跳变）。
**限定**：① 未覆盖"新 Formation 对象身份隔离"分支；② 采样使用**游戏快进**（1268.79s 游戏时间 / 约 6.5 分钟墙钟）→ 响应延迟不构成证据；③ `LostAIControl` 的触发机制（增援/编队更新瞬间 `IsAIControlled` 翻转）为观察推断。

### 场景 3 / 场景 4 / 场景 5 子项「拆分」：N/A（不可达，非失败）

- **场景 3 玩家接管**：Route A 对玩家队**无条件豁免**（`CombatAiMissionLogic.cs:2387`、`RouteAOrderAdapter.cs:141-142`），只能武装"非玩家队 + 无领主所有者 + 无玩家兵员 + AI 控制"编队；玩家在 UI 上无法对非玩家队编队下令 → 该动作不可达。
- **场景 4 Rejoin**：仅当 `mission==SupportMainEffort` **且** `phase==Deployment` 才产生（`FormationCommandPolicy.cs:156-157`）；实测 ~35 场部署阶段由游戏自动结束、时长 5–20s，且被武装编队在部署期内恒为 `Reserve`（→Hold）→ 三场 E 模式 `Rejoin` 触发 **0 次**、`NavigationFailed` **0 次**。
- **拆分**：中途拆分编队是玩家侧操作，AI 不会自行拆分 → 对 Route A 武装对象不可达。

替代证据已覆盖相邻语义（外部覆盖、`EmptyOrInvalid`、`Disabled`、增援重挂载）；详见 `docs/cps3r-routea-runtime-scenarios-2026-09-13.md` §3a/§3b/§3c。

## 4. 未证明项与开放项（如实披露）

1. **CB-REPAIR-0004（IN_PROGRESS，本轮新增）**：`run=b0e2e0a4…` session 4 出现「**占位不开火**」——`ExternalReplaced`（00:15:39.418）之后 **101 秒内 `BehaviorSelected` = 0 次**（Behavior 未被选中），同期原版 AI 写入 `ExternalWhileActive` **1535** 条，`EdgeRetry` **0 次**（armed 的重试永无执行机会），期间 Route A 仍持有 Movement 所有权（阻塞 CB 自身执行器），最后以 `Exit(LostAIControl)` 退出。该 session 汇总 `edgeRetries=0 / externalDuration=1101.900s`。与 CB-REPAIR-0001 要解决的「既不重落地也不释放」同域。**已开卡，尚无修复与运行期复验。**
2. **未取得样本的分支**：新 Formation 对象身份隔离；Rejoin 分支及其 `NavigationFailed` 路径；F-2「重试已用后再次 `replaced=true` → 立即 Release」。
3. **延后项（未开卡）**：F-3（`0f` 哨兵二义）、F-4（`edgeRetries` 授权侧落账口径）、F-5（Release 位于 Harmony postfix 栈内改权重，需运行期证）、H-1（E 模式选中无提示，可观测性）、H-2（E 模式帧成本须按 RouteA+观测复合口径评定）。
4. **口径分歧（Descriptive，未改代码）**：Route A 的玩家队排除**不受** MCM 开关 `PlayerTeamExemptionEnabled` 约束，与其它 7 个模块口径不一致。
5. **状态陈旧纪律**：`CURRENT_STATE.last_verified_commit = 9a2afe4`；其后 HEAD 均为文档提交（代码内容一致）。

## 5. 请 Astra 裁决的问题

1. **条件 5**：现有运行期证据（场景 1、2、5 PASS；场景 3、4 及拆分判 N/A）是否足以判定条件 5**满足**？若否，还缺哪一类证据？（附件 §6 为可复现命令）
2. **N/A 是否可接受**：把"结构上不可达"的场景记作 N/A（而非"未验证/FAIL"）是否符合验收口径？若 Astra 认为需要，请指出替代验证方式。
3. **占位不开火（CB-REPAIR-0004）**：是否构成 GO 阻断项，还是可列为 GO 之后的运行期债？
4. **玩家队豁免口径分歧**：是否需要收敛（使 Route A 也受 MCM 开关约束）？该项当前仅记录，未改代码（功能冻结令内）。

## 6. 复现方式（只读，不改仓库/不启动游戏）

```powershell
# 1) 场次关键事件（替换 RUN_ID）
Select-String $env:TEMP\CombatBrain.log -Pattern 'run=<RUN_ID>' |
  Where-Object { $_.Line -match 'event=(Intent|OrderApplied|ExternalReplaced|ExternalWhileActive|EdgeRetry|Exit|Restore)\b' }

# 2) Route A 汇总（字段名为 applied=、intent= 是事件计数，不是当前意图）
Select-String $env:TEMP\CombatBrain.log -Pattern '\[RouteA\] run=' | Select-Object -Last 3

# 3) Rejoin/场景条件体检（含部署阶段区间、mission 时间线、重叠判定）
powershell -NoProfile -ExecutionPolicy Bypass -File "E:\MBmod\CombatAI\tools\routea-rejoin-check.ps1"

# 4) 离线质量门
dotnet build "CombatBrain\CombatBrain.csproj" -c Release --nologo
dotnet run --project "OfflineTests\OfflineTests.csproj" -c Debug
```

## 7. 相关文件

- 运行期场景记录（含全部 run id、判定命令、N/A 理由、待查 discrepancy）：`docs/cps3r-routea-runtime-scenarios-2026-09-13.md`
- 上一轮复审提交物：`docs/cps3r-routea-astra-review-submission-2026-09-08.md`
- Route A 收口报告：`docs/cps3r-routea-final-blocker-report.md`
- 验证命令链与采样前置：`测试指南.md`（§0a = E 模式前置）
- 返修台账：`MBmod/CombatAI/prompts/repairs/registry.json`（CB-REPAIR-0001…0004）
- 现行验证态：`docs/knowledge/CURRENT_STATE.md`
