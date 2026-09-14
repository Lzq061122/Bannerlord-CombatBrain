# CombatBrain — CPS-3R Route A 复审汇报件（2026-09-14）

> 用途：交给 Astra 做**第二轮复审 / 条件 5 判定**。本件自包含：变更、离线证据、运行期证据、未证明项与待裁决问题。
> 性质：DESCRIPTIVE（scoped claims）。现行验证态唯一权威 = `docs/knowledge/CURRENT_STATE.md`；本件不判 GO。
> 上一轮：`docs/cps3r-routea-astra-review-submission-2026-09-08.md`（当时结论 READY FOR ASTRA RE-REVIEW，未部署 DLL）。

---

## 0. 一句话状态

上一轮提交后，共落地 **3 张已闭环返修卡 + 1 项观测硬约束**，**已部署**（现网 = `9a2afe4`），并完成**可执行的运行期场景 1 重跑 + 场景 2 + 场景 5**（均 PASS）；
场景 3（玩家接管）、场景 4（Rejoin）、场景 5 子项（拆分）为**本次采样不适用**（**Rejoin 分支仍未验证**，不得视为结构上永久不可达）；
后续巡检出「占位不开火」→ **CB-REPAIR-0004**：r1 → 质检拦下（R4-1）→ r2 → **Astra 判 FAIL/P1（周期重占位）** → **r3 `395fe13` 已修复并经质检 t12 双向独立复现判否**（离线层面闭合；**该提交未部署**，卡未关）。
**条件 5 是否满足，交由 Astra 判定；本机不判 GO。**

代码基线：**现网/已部署 = `9a2afe4`**；**HEAD = `395fe13`（含 CB-REPAIR-0004 r1/r2/r3 三个未部署提交）**；分支 `main`；功能冻结令仍在生效。**SHA 对应（J-5，防对不上号）**：r1 = `723b457`，r2 = `bc8449a`（早期同标题提交 `6f632ae` 已被 amend 取代、不在祖先链），r3 = `395fe13`。

## 1. 变更清单（均已过独立质检门）

| 卡 / 条目 | commit | 内容 |
|---|---|---|
| CB-REPAIR-0001 | `70c9a11` | 外部 setter 覆盖 Route A 当前命令时通知 `RouteAControl`；首次覆盖只允许**一次**边沿重试；外部继续覆盖 → 立即 `Release(ExternalOverridePersisted)` + 恢复原权重；汇总新增 `externalWhileActive / edgeRetries / externalDuration` |
| CB-REPAIR-0002 | `a219c5b` | F-1：`externalDuration` 口径统一为**最后一次外部覆盖时间结算**（不再用退出时刻）；F-2：覆盖释放语义**显式三分支**（`replaced=true` 未用重试→arm；重试已用后再 `replaced=true`→立即 Release；`replaced=false`→不释放、不消耗重试、仅计数） |
| CB-REPAIR-0003 | `9a2afe4` | G-1：`RouteAExecutionEnabled() &&= ObserverEnabled` —— E 模式**无观测不执行**（fail-safe，防"看似执行、覆盖恢复未验证"的假样本） |
| 测试指南 §0a | `f6634f7` | E 模式采样前置写入文档：ObserverEnabled 硬约束 + 帧成本须按「RouteA+观测」复合口径评定 |
| CB-REPAIR-0004 r1 | `723b457` | 交付上限 `DeliveryDeadlineSeconds=10f` → 长期无选中时 `Release(SelectionTimeout)` + 暂停重取（治「占位不开火」） |
| CB-REPAIR-0004 r2 | `bc8449a` | 交付暂停仅由**真实释放**终结（no-op Release 不再作废暂停）；具名 `HasExternal` 取代 `0f` 哨兵；`lastSelectedAt`；`edgeRetries=授权次数` |
| CB-REPAIR-0004 r3 | `395fe13` | **消除 Astra P1 周期重占位**：删除「`situationSignal` 变化即解锁」；重武装须同时满足**意图真变化 + 10s 冷却 + 本暂停期授予 <2**；可观测 `SuspensionRetry` / `suspensionRetries=` |
| 运行期证据登记 | `97fbcd9`→`b2827b5` | `docs/cps3r-routea-runtime-scenarios-2026-09-13.md`（场景 1/2/5 证据、场景 3/4 口径、§3c 根因、**§3d r3 与 t12 双向复现**） |

台账：`prompts/repairs/registry.json` 中 CB-REPAIR-0001/0002/0003 均 `COMPLETED` + `review_state=PASSED`；**CB-REPAIR-0004 仍 `IN_PROGRESS`**（按 r3 派单：须 Astra 复审通过 + 部署/运行期证据齐备才可关）。

## 2. 离线与部署证据

| 项 | 结果 |
|---|---|
| Release 构建 | 每次改动 `--no-incremental` **0 错 0 警** |
| OfflineTests | 现网基线 `9a2afe4` = **1241 PASS / 0 FAIL**；**未部署的 r3 `395fe13` = 1297 PASS / 0 FAIL**（Check 站点 1100→1120；`RouteAStaticChecks.ps1` 实跑 20 条 PASS） |
| 静态 setter 扫描 | 去 trivia 后 **4 = 4 无新增**（`FormationCommandExecutor`=3 + `RouteAOrderAdapter`=1） |
| 部署 | `deploy.ps1` 于游戏进程退出后执行；源/部署 DLL **SHA 一致**（`A39AC964…`）；**r1/r2/r3 均未部署**（新构建 `7E5FC25A…` 仅在 repo bin） |
| 红线 | 各卡全程未部署、未跑运行期采样、未触碰 `CommandProvenance` / 执行器 / 适配器 / Mission 逻辑 / Settings |

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

### 场景 3 / 场景 4 / 场景 5 子项「拆分」：本次采样不适用（分支仍未验证）

- **场景 3 玩家接管**：Route A 对玩家队**无条件豁免**（`CombatAiMissionLogic.cs:2387`、`RouteAOrderAdapter.cs:141-142`），只能武装"非玩家队 + 无领主所有者 + 无玩家兵员 + AI 控制"编队；玩家在 UI 上无法对非玩家队编队下令 → 该动作在本次采样口径下不适用（**玩家豁免口径属产品/规范裁决项，未裁决**）。
- **场景 4 Rejoin**：仅当 `mission==SupportMainEffort` **且** `phase==Deployment` 才产生（`FormationCommandPolicy.cs:156-157`）；实测 ~35 场部署阶段由游戏自动结束、时长 5–20s，且被武装编队在部署期内恒为 `Reserve`（→Hold）→ 三场 E 模式 `Rejoin` 触发 **0 次**、`NavigationFailed` **0 次**。**该分支仍未验证**，不得视为结构上永久不可达。
- **拆分**：中途拆分编队是玩家侧操作，AI 不会自行拆分 → 对 Route A 武装对象不可达。

替代证据已覆盖相邻语义（外部覆盖、`EmptyOrInvalid`、`Disabled`、增援重挂载）；详见 `docs/cps3r-routea-runtime-scenarios-2026-09-13.md` §3a/§3b/§3c。

## 3b. CB-REPAIR-0004 轮次与独立复核（在线下层面已闭合，未部署）

**① 缺陷**（`run=b0e2e0a4…` session 4）：`ExternalReplaced`（00:15:39.418）之后 **101 秒内 `BehaviorSelected` = 0 次**，同期原版 AI 写入 `ExternalWhileActive` 1535 条、`EdgeRetry` 0 次，期间 Route A 仍持有 Movement 所有权（阻塞 CB 自身执行器）= **占位不开火**。

**② r1/r2/r3**：见 §1 表；r2 被 Astra 判 **FAIL/P1**：「连续态势数值变化 → 超时释放后下一拍重武装」，占空比 ≈99.83%。

**③ 质检 t12 双向独立复现**（`git archive` 抽 r2/r3 生产源各自构建 DLL，驱动生产 `RouteAControl` + `RouteASituationStamp.Update` + `FormationCommandExecutor.ExecuteWinner`；仅 weights 端用 `IRouteAWeights` 替身）：

| 场景（60Hz） | r2 = `bc8449a` | r3 = `395fe13` |
|---|---|---|
| P1 位置每拍微变、零选中 | 释放@10.0167 → 重取@**10.0333**；100s duty **99.85%**，执行器仅 0.15% | acquires=1；40/100/150s **零重取**；duty 25/10/6.67%；执行器 **75/90/93.33%**（连续窗口） |
| 兵数变化 / 12 维向量微变 | duty **98.75% / 99.88%**（P1 的洞不限于位置） | 不重武装 |
| 离散意图变化 | — | acquires=2 + `SuspensionRetry`=1（只授一次、具名可观测） |
| CHURN（意图每拍抖动+连续变号） | 每 10s 重取 | acquires 封顶 **3**、Retry 封顶 **2** 后持续让位 → 无周期重占位 |
| HEALTH（稳定选中） | `selected=80 applied=2 replaced=1 edgeRetries=1 lastSelectedAt=39.517s` | 与 r2 **逐字段一致** → 无回归 |

**④ 结论**：Astra 的 P1 被独立证实，r3 判否该反例；**卡仍 `IN_PROGRESS`**（等 Astra 复审 + 部署/运行期证据）；**r1/r2/r3 均未部署**。

## 4. 未证明项与开放项（如实披露）

1. **CB-REPAIR-0004**：离线已闭（r3 + t12 独立复核），但**未部署、零运行期证据**；下游最小采样项见 §4 末条。
2. **未取得样本的分支**：新 Formation 对象身份隔离；Rejoin 分支及其 `NavigationFailed` 路径；F-2「重试已用后再次 `replaced=true` → 立即 Release」。
3. **已闭合**：R4-1（空 Release 解锁暂停）随 r2 闭合；**F-3（`0f` 哨兵）/ F-4（`edgeRetries` 落账口径）随 r2/r3 闭合**。**仍未闭**：F-5（Release 位于 Harmony postfix 栈内改权重，需运行期证）、H-1（E 模式选中无提示，可观测性）、H-2（E 模式帧成本须按 RouteA+观测复合口径评定）。
4. **r3 新增口径（J-2，需裁决）**：预算重置只在「持所有权时真实选中／非超时真实释放／`BeginSession`」发生 ⇒ **预算耗尽后本场不可自愈**（CHURN 实测 acquires 封顶 3）。安全方向正确，但 CPS-4 须写明「**一场内一次坏窗口 = 本场 Route A 退役**」，是否接受请裁决。
5. **低危未修（J-3/J-4）**：J-3 证据表满时 `_suspendedReattempts` 与 `SuspensionRetry` 事件可能不一致；J-4 超时暂停依赖"先清后设"次序（已留陷阱注释）。
6. **J-1（记账口径）**：开发侧反向验证产物（`REVERSE-CHECK PASS=1280 FAIL=17` + 具名失败断言）未按任务契约字段登记 → 质检按**未验证**记账，风险已由 t12 更强的等价证据（真 r2 生产构建复现 P1）闭合；已在运行场景档 §3d 补登。
7. **口径分歧（Descriptive，未改代码）**：Route A 的玩家队排除**不受** MCM 开关 `PlayerTeamExemptionEnabled` 约束，与其它 7 个模块口径不一致。
8. **状态陈旧纪律**：`CURRENT_STATE.last_verified_commit = 9a2afe4`（现网/已部署）；**HEAD = `395fe13` 含三个未部署代码提交**（r1/r2/r3），不得当作已验证状态。
9. **下轮最小采样项（部署后）**：a) 连续变号期间不得重取、`suspensionRetries` 应为 0；b) 合格意图变化后允许且仅允许一次重取（`SuspensionRetry` 一条）；c) `ExternalReplaced` 后 ≤10s 出现 `Exit(reason=SelectionTimeout)` 且该编队执行器恢复 Movement 写；d) 无重入异常、`RestoreFailures==0`、`OrderTraceAgg`/执行器归因正常。

## 5. 请 Astra 裁决的问题

1. **条件 5**：现有运行期证据（场景 1、2、5 PASS；场景 3、4 及拆分本次采样不适用）是否足以判定条件 5**满足**？若否，还缺哪一类证据？（§6 为可复现命令）
2. **本次采样不适用是否可接受**：把"该采样口径下不适用、分支仍未验证"记作非 FAIL 是否符合验收口径？若 Astra 认为需要，请指出替代验证方式（尤其 Rejoin 分支）。
3. **CB-REPAIR-0004（r3 + t12 复核通过，未部署）**：是否构成 GO 阻断项，还是可列为 GO 之后的运行期债？
4. **J-2 口径**：接受「一场内一次坏窗口 = 本场 Route A 退役（预算耗尽后本场不自愈）」这一语义吗？若需自愈机制，请指出期望的触发条件与上限。
5. **玩家队豁免口径分歧**：是否需要收敛（使 Route A 也受 MCM 开关约束）？该项当前仅记录，未改代码（功能冻结令内）。

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
