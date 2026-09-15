# CombatBrain — CPS-3R Route A 复审汇报件（2026-09-14）

> 用途：交给 Astra 做**第二轮复审 / 条件 5 判定**。本件自包含：变更、离线证据、运行期证据、未证明项与待裁决问题。
> 性质：DESCRIPTIVE（scoped claims）。现行验证态唯一权威 = `docs/knowledge/CURRENT_STATE.md`；本件不判 GO。
> 上一轮：`docs/cps3r-routea-astra-review-submission-2026-09-08.md`（当时结论 READY FOR ASTRA RE-REVIEW，未部署 DLL）。

---

## 0a. Astra 回签（2026-09-14）

**Astra 正式结论：离线 PASS、允许进入受控部署采样。** 据此锁定当前状态（后续判定以本块为准）：

| 项 | 状态 |
|---|---|
| CB-REPAIR-0004 r3（`395fe13`） | **离线复审通过** |
| `395fe13` | **允许部署测试；尚未取得运行期验收** |
| 台账 CB-REPAIR-0004 | 保持 **`IN_PROGRESS`**；`review_state = PASSED`（离线通过）／运行期验收待办 |
| 条件 5 | **仍未满足** |
| CPS-4 / GO | **仍不批准** |
| 阻断代码 | **当前没有必须继续修改的阻断项** |
| 阻断外待整理（P2，不阻断本轮采样） | ①重试事件 revision 配对；②t12 源码不可重建 |
| 最终 GO 前仍须裁决 | **玩家豁免口径**（产品/规范裁决项） |

部署与采样绑定项（进游戏前须登记完整）：commit `395fe13`、部署 DLL SHA、run/session、MCM 与战斗配置，以及 §4 末条的最小采样验收点（连续变号不重取／合格意图变化仅一次 `SuspensionRetry`／零选中后按期限 `SelectionTimeout` + `Restore`／普通 Movement 持续恢复／健康 `EdgeRetry`、归因与 `RestoreFailures=0`）。

## 0b. 本件更新：r3 受控部署 + 运行期采样完成（2026-09-14 晚）

> **2026-09-15 更新**：本块之后的 **§0c（归因缺环 + P2 债务）** 与 **§0d（诊断构建运行期采样，机制已确立）** 为更新块；本块中关于"现网 = r3 DLL""S5 正向形态 3/3 全负""原生胜出待探针确认"等表述，**以 §0d 为准**（现网已换为诊断构建 `a24e921`；r3 类构建覆盖事件累计 6 次、正向 0 次；机制已由探针确立）。

**Astra 2026-09-14 回签后，已按回签执行 r3 受控部署与运行期采样。** 本块取代正文中所有"未部署 / 零运行期证据"的表述（§2 部署行、§3 无 r3 采样、§3b④、§4.1/4.2/4.8/4.9）。

| 项 | 现状 |
|---|---|
| 部署 | **r3 `395fe13` 已部署**（2026-09-14 09:34，DLL SHA256 `7E5FC25A…`，src=dst MATCH，硬门：部署前 `Bannerlord` 进程为空）；此后**未再重部署** |
| 运行期采样 | **累计 8 场**（`docs/cps3r-routea-r3-sampling-record-2026-09-14.md` §2b–§2h；运行场景档 §3e–§3k），全部同部署绑定、无快进、异常 0 |
| S1 连续变号不重取 | **PASS**（多场：494s / 895.9s / 213.7s / 117s+ 零再武装；连续变号零重取） |
| S2 合格意图变化仅一次 `SuspensionRetry` | **PASS**（2 个独立样本：§3h 三连重取、§3k 两次重取各对应一次具名意图变化；冷却内与达上限后一律不授予） |
| S3 超时 `SelectionTimeout` + `Restore` | **PASS**（多次，Δ=9.507–10.006s，全部配对 `Restore`，`restoreFailures=0`） |
| S4 释放后执行器持续可用 | **PASS（直接形态；口径更正）**：释放后 `ExternalAfterExit` 连续窗口——§3k 场 190 次 / 总跨度 117.2s（**内含 CB 两次重新持有所有权，实得三段 ≈10.0s/30.7s/56.5s，合计 ≈97.2s**）；§3j 场 188 次 / 213.7s |
| **S5 正向形态（`ExternalReplaced → EdgeRetry → OrderApplied`）** | **未取得：3 次覆盖样本 3/3 全负**（§3i/§3j/§3k），`edgeRetries` 均为 0 |
| 健康路径 / 生命周期 | **PASS（强）**：多场 `applied=15~11 / stableSoFar` 同步、`weightFlap=0`、`unverified=0`、`dropped=0`；所有 `Exit` 全部配对 `Restore` |
| 场景 4 Rejoin | **成功路径已取得 1 次运行期样本**（§3j：部署期 `SupportMainEffort` → `Intent(Rejoin)` → `OrderApplied(ot=1)`，`NavigationFailed=0`）；**失败路径 `NavigationFailed` 仍无样本** |
| CB-REPAIR-0004 台账 | 仍 **`IN_PROGRESS`**、`review_state=PASSED`（离线通过）；**唯一未满足的运行期验收点 = S5 正向形态** |
| CB-REPAIR-0005（新，诊断） | 只读断流探针已**实现 + 离线验收完成**（`a6a4ec5`；OfflineTests **1320 PASS/0 FAIL**，setter 站点 4=4），**按卡要求未部署**；派单 `CB-PROMPT-0043`、交付报告 `CB-PROMPT-0044` |
| 条件 5 / CPS-4 / GO | **仍未满足 / 仍不批准**（本件不判 GO） |

**S5 三次覆盖样本的共同形态（本件新增的关键待裁决事实）**

| 样本 | 覆盖发生时的状态 | 覆盖后 | 结果 |
|---|---|---|---|
| §3i `0496f7b6`（1426 人 / 3809s） | 覆盖前最后选中 1114.587 | 断流 **2694s 至战斗结束** | 负 |
| §3j `4156cc25` s2（2010 人 / 549s） | 覆盖前 320.0–325.0 连续选中 | 断流 **223s 至战斗结束** | 负 |
| §3k `4e885a4e`（1251 人 / 347s，**最强**） | 覆盖前 **379 次连续选中 / 15 次落地**（健康局正中间） | 断流 **127.3s 至战斗结束**；期间 CB **两次重取**（各持 10s 交付窗口）引擎仍未回调 | 负 |

§3k 附带的两个可判定事实（Descriptive；**口径已按 Astra P1 复审更正**）：

1. `ExternalWhileActive`：**覆盖前 3 次（全在部署拍 5.295）/ 覆盖后 58 次**（221.0295→290.5281；**分布非均匀：含 11.0s 与 31.0s 两处间隔，最大间隔 31.0s**）⇒ 只能报告"覆盖后外部 Movement 写入显著增加 + 该切片事实"；**来源仍是 `ExternalUnknown`，不得写成"已证明原生持续胜出"**；CB 行为同期零选中。
2. 全场 `BehaviorSelected` 只有两处 >3s 空档，且都在 `LostAIControl` 窗口内（19.9s / 5.8s），其余严格 0.5s 连续 ⇒ **断流不是引擎节拍问题**。

**由此产生的新判断（Inferred，未证）**：`ExternalReplaced`（CB 落地后 ≤1s 内出现外部写）标记的正是"外部侧重新接管该编队 Movement"的时刻；此后外部写入持续存在而 CB 行为再未被选中，**armed 单次边沿重试失去被兑现的前提** ⇒ 在当前实现下，S5 正向形态（要求 10s 内引擎重新选中 CB 行为）可能**在该配置下不可达**；r3 前构建上出现过的那一次正样本（§3 `ba84a11b`，Δ=5.00s）反而成为需要解释的例外。**"原生行为是否持续胜出"仍属待探针确认的推断。**

**这正是 CB-REPAIR-0005 探针要判定的事**：`[RouteASilence]` 行会给出覆盖后 `activeIdx`（胜出者是否仍是 Route A 行为）、`activeType`（谁在胜出）、`weights`（CB 侧权重是否仍为 `10/0/0/0`）、`aiSame/aiCtl`（AI 对象与资格是否仍成立）。**探针已就绪但未部署**——部署会替换现网 r3 DLL，故需裁决（见 §5 问题 6/7/8）。

## 0c. 本轮新增证据：归因缺环与 P2 债务（2026-09-15，均已过独立复核）

> 两项只读调查由开发组完成（团队 `combatbrain-开发组-20260915`：Captain + 开发 + 质检；**零产品代码改动、未部署、未启动游戏**）。以下为**已过独立复核**的结论；引用请以 SHA 锚点为准。

| 主题 | 交付物（锚点） | 复核链（均为独立实跑） | 终态 |
|---|---|---|---|
| ① Route A 归因缺环：落地写入不进 `CommandProvenance`/`[OrderTraceAgg]` | `MBmod/CombatAI/tools/cps3r-routea-attribution-gap-report-2026-09-15.md` = **38,425 B / SHA256 `F8F800D3EF8CA58F7D1666B289CEC2A3A7E102BE7F925BAE2E1CFCA95AD7294D`** | review r1 → needs_revision（3 medium）→ 修 → **verification r2 = PASS**；其后 2 条 low 随合并返修落修并 **verification r3 = PASS** | 结论成立、文档自洽 |
| ② P2 债务整理：重试事件 revision 配对 + 历史提交源码可复现性 | `MBmod/CombatAI/tools/cps3r-routea-p2-debt-consolidation-2026-09-15.md` = **42,868 B / SHA256 `889269FB37AB8EE9B31C25B55226F6E46498C0D56A6862520B74F70A2029B39D`** | review r1 → needs_revision（1 high + 2 medium）→ 修 → verification r2 → needs_revision（1 medium：两计数器口径混用）→ 修 → **verification r3 = PASS** | 结论成立、文档自洽 |

**① 关键结论（Observed，静态可证）**：`CommandProvenance.Instance.Record` 的**生产唯一调用点** = `CombatBrain/Ai/FormationCommandExecutor.cs:147`；Route A 走旁路 `RouteAOrderAdapter.cs:33-36`（`CommandSourceScope.Enter(CombatBrainRouteA)` + `SetMovementOrder`，调用来源 `:302`），**从不调用 `Record`** ⇒ `[OrderTraceAgg] CombatBrainRouteA` 恒为 `0/o0`（含该字段的行 17/46、19/48、20/49 全为 0；全库 `CombatBrainRouteA=[1-9]` 命中 **0**）；Route A 的归因只存在于 `[RouteAEvent] source=CombatBrainRouteA` 渠道（同 run 自洽：`OrderApplied` = `SetterBegin(source=CombatBrainRouteA)` = 12（`run=4156cc25`）/ 15（`run=4e885a4e`），同期 `[OrderTrace]` 593 条中 0 条含 Route A）。
**影响（Inferred）**：S5 验收项中"`OrderTraceAgg` 归因与执行器一致"在当前实现下**不可满足**；CPS-4 归因基线须据此改写。**收敛方案代价清单（未改码）**：A 文档口径（可立即执行）／C 记录为待裁（不改码）／B 适配器接线 `Record`（改码；须先裁定冻结令 §7.2 与 CB-REPAIR-0004 未关台账）／D 两渠道聚合合并（改码，风险最高，不建议）——请在 §5-9 裁决方向。

**② 关键结论**
- **P2-1（配对口径）**：`SuspensionRetry`/`EdgeRetry` 的 `revision` = **解锁/被消费时该身份已生效的 revision**（回指），配对方向按事件类型分立：`SuspensionRetry` 的配对 `Intent` 在**其后**，`EdgeRetry` 的配对 `Intent` 在**其前**。全库去重后 `SuspensionRetry` **7 条 / 3 run**（原始命中 21 行系三份**累积快照**重复所致），配对 **7/7**；`EdgeRetry` 唯一 **1** 条（`ba84a11b` seq=551 t=250.5362）。
- **上限口径（重要，已固化文档硬约束）**：日志汇总字段 `suspensionRetries=` 是 **session 累计**（`RouteAEvidence.cs:218`，仅 `BeginSession`/`:89` `Array.Clear(_stats)` 清零）；**期内预算** `_suspendedReattempts`（`RouteAControl.cs:105/:136/:179/:185/:287/:381`，全 private、**不出现在任何日志**）才是与 `MaxSuspendedReattempts`(2) 比较的对象。反例：`run=d43c302e` 汇总打印 **3**，而按暂停期分段期内最大 **2**（P1={252}、P2={1871,1997}）⇒ **两计数器不可互相替代**；文档已写入"禁止用汇总值与 2 比较、期内计数须按事件分段重建"的硬约束。
- **P2-2（可复现性）**：**源码级可复现成立**——`git archive 395fe13 -- CombatBrain OfflineTests` 多次执行 zip **逐字节相同**（SHA256 `8194E8A791089334B6DE96348641B88C284C031A006E9C0A736AE5A3AE0AC17A` / 431,203 B / 113 文件），离仓构建 **0 警 0 错**；**二进制级不保证一致**——同源但 obj/out 目录不同的三次构建得三个不同 SHA（`F62F6642…`/`3D86DADB…`/`066A1F9C…`，均 ≠ 现网 `ADCC641E…`），"同目录重复构建是否稳定"未做正交对照 ⇒ 记为 **Unknown**。

**遗留（不阻断）**：① verification r3 记 1 条 low——② 报告 `:42`「二者可相差任意大」措辞过强（实测 3 vs 2），建议后续顺手改为「可显著不等，**不得互相替代**」；② **运行期债**：诊断构建 `a24e921` 已部署并**已完成运行期采样（见 §0d：3 次覆盖、14 条探针拍、机制确立）**；但该样本来自诊断构建，**其能否计入 r3 运行期验收仍待 Astra 认定（§5-7）**，本 pass 本身只覆盖"文档证据与磁盘一致"，不构成 S5／条件 5／CPS-4 的通过证据。

## 0d. 诊断构建运行期采样（2026-09-15）：**机制问题已由观测确立**（替换此前的 Inferred）

**绑定**：诊断构建 `a24e921`（= r3 `395fe13` + 只读探针 + 单点异常边界隔离），现网 DLL `ADCC641E04231DD628BB80F1FAFF887062D81C76894467D83D394A682AAF397C`（231,424 B，src=dst MATCH，09-15 部署；r3 可回滚备份 `7E5FC25A…` 在 `E:\MBmod\_backups\`）；每场均 `观测已挂载 prefixes=3`；`Cps3Mode=E_RouteA`；**未用快进**（mission/wall ≈1.0）；窗口 44,497 行 **`Exception` 命中 0**。采样按 CB-REPAIR-0006 派单的上限执行（1 个覆盖窗口；≤2 场或 30 分钟；先到即停）。

**结果（4 场 mission / 2 run，1949–2032 agents）**：3 次 `ExternalReplaced` 覆盖，`[RouteASilence]` 共 **14 条**采样拍（其中 M4 场覆盖后 10s 窗口内 9 拍 + `final=1` 一拍）。

| 观测项 | 实测（14/14 一致） |
|---|---|
| `activeIdx` | **0**（引擎的 `ActiveBehavior` 不是四个 Route A 行为中的任何一个） |
| `activeType` | **`BehaviorCharge`**（原生 charge 行为） |
| `aiSame` / `aiCtl` | **1 / 1**（FormationAI 对象未替换、编制仍受 AI 控制） |
| Route A 侧 `weights` | 始终**恰一个 10.00、其余 0.00**（CB 权重完好；M4 中期由 Hold 切到 Advance） |
| 覆盖前 | `BehaviorSelected` 连续到覆盖前 0.5s（M4 `lastSelectedAt=280.033s`，覆盖 280.533s） |
| 覆盖后 | 整个交付窗口内**零回调**（`edgeRetries=0`）⇒ M4 `Exit(SelectionTimeout)`：Δ=9.502s 自覆盖 / 10.002s 自末次选中；M3 由 `Exit(Disabled)`、M2 由 `Exit(MissionEnding)` 提前终止 |

⇒ **结论（Observed）**：`ExternalReplaced` 之后，**原生 `BehaviorCharge` 成为并保持胜出者**，Route A 行为在 10s 交付窗口内一次都未被回调 ⇒ armed 单次边沿重试**无兑现机会**。同时由探针排除三类替代解释：行为表/对象被换（`aiSame=1`）、资格丢失（`aiCtl=1` 且无 `EmptyOrInvalid`/`LostAIControl` 释放）、CB 权重被清零（权重完好）。
**候选解释（Inferred，未取证）**：Route A 行为有效权重上限为固定 `10 × GetAiWeight()`，而原生 charge 行为在"应冲锋"局面下权重可能更高 ⇒ 原生接管后 CB 无法赢回选择权。现有探针**不报原生权重**，故该解释未验、本轮不据此改码。

**对 §5 问题的影响**：**问题 7（是否授权部署诊断构建）已不适用**——探针已部署并完成采样；请转而裁定「**诊断构建样本能否计入 r3 运行期验收**」（即"纯增量只读探针 + 单点异常边界隔离"是否满足零语义差/等价性前提）。**问题 6（S5 验收口径）与问题 8（是否重新设计覆盖恢复语义）现在有了实测依据**：**r3 类构建上覆盖事件累计 6 次、正向形态 0 次**（r3 前构建曾出现 1 次，`ba84a11b` Δ=5.00s）。

**明细与归档**：`docs/cps3r-routea-diagnostic-sampling-2026-09-15.md`；全量快照 `MBmod/CombatAI/logs/CombatBrain-2026-09-15-full-1604.log`（57,436,833 B / `0E1EC859…`）、切片 `run-14ff27b0-20260915-diagnostic.log`（656,089 B / `605AD775…`）与 `run-6e2a87d2-20260915-diagnostic.log`（394,157 B / `52F9772E…`）。

**不得扩大结论**：样本来自诊断构建（≠ r3 DLL）；"窗口内未被选中"不等于"永久不可达"；本件不判条件 5 / CPS-4 / GO。

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
| 部署 | `deploy.ps1` 于游戏进程退出后执行；源/部署 DLL **SHA 一致**；r1/r2/r3 在提交当时均未部署。**2026-09-14 09:34 起现网 DLL = r3 `395fe13`（`7E5FC25A…`，src=dst MATCH）——见 §0b**；此后至本件更新时**未再重部署** |
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

### 场景 3 / 场景 5 子项「拆分」：本次采样不适用；场景 4 Rejoin：**成功路径已取样、失败路径未验证**

- **场景 3 玩家接管**：Route A 对玩家队**无条件豁免**（`CombatAiMissionLogic.cs:2387`、`RouteAOrderAdapter.cs:141-142`），只能武装"非玩家队 + 无领主所有者 + 无玩家兵员 + AI 控制"编队；玩家在 UI 上无法对非玩家队编队下令 → 该动作在本次采样口径下不适用（**玩家豁免口径属产品/规范裁决项，未裁决**）。
- **场景 4 Rejoin**：仅当 `mission==SupportMainEffort` **且** `phase==Deployment` 才产生（`FormationCommandPolicy.cs:156-157`）；早期三场 E 模式（200v200 步兵）实测部署阶段由游戏自动结束、时长 5–20s，且被武装编队在部署期内恒为 `Reserve`（→Hold）→ 当时 `Rejoin` 触发 **0 次**、`NavigationFailed` **0 次**。**2026-09-14 更新：混编大场面已取得 1 次成功路径样本**（`run=4156cc25` session=1 部署期 `SupportMainEffort` → `Intent(Rejoin rev1)` → `OrderApplied(ot=1)`，`NavigationFailed=0`；运行场景档 §3j）⇒ **该分支不是结构不可达**；**仍缺 `NavigationFailed` 失败路径样本**。
- **拆分**：中途拆分编队是玩家侧操作，AI 不会自行拆分 → 对 Route A 武装对象不可达。

替代证据已覆盖相邻语义（外部覆盖、`EmptyOrInvalid`、`Disabled`、增援重挂载）；详见 `docs/cps3r-routea-runtime-scenarios-2026-09-13.md` §3a/§3b/§3c。

## 3b. CB-REPAIR-0004 轮次与独立复核（离线层面已闭合；r3 已部署并完成 8 场运行期采样——见 §0b）

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

**④ 结论**：Astra 的 P1 被独立证实，r3 判否该反例；**r3 已于 2026-09-14 部署并完成 8 场运行期采样（见 §0b）**；台账仍 `IN_PROGRESS`，唯一未满足的运行期验收点 = **S5 正向形态（3/3 全负）**。

## 4. 未证明项与开放项（如实披露）

1. **CB-REPAIR-0004**：离线已闭（r3 + t12 独立复核）；**r3 已部署，运行期采样 8 场完成（见 §0b）**。五个验收点中 S1/S2/S3/S4 **已 PASS 且多数有多场样本**；**唯一未满足 = S5 正向形态（`ExternalReplaced → EdgeRetry → OrderApplied`），3 次覆盖样本 3/3 全负**。
2. **未取得样本的分支**：**S5 正向形态**（§0b）；Rejoin 的 `NavigationFailed` **失败路径**（成功路径已有 1 次样本）；新 Formation 对象身份隔离；F-2「重试已用后再次 `replaced=true` → 立即 Release」。
3. **已闭合**：R4-1（空 Release 解锁暂停）随 r2 闭合；**F-3（`0f` 哨兵）/ F-4（`edgeRetries` 落账口径）随 r2/r3 闭合**。**仍未闭**：F-5（Release 位于 Harmony postfix 栈内改权重，需运行期证）、H-1（E 模式选中无提示，可观测性）、H-2（E 模式帧成本须按 RouteA+观测复合口径评定）。
4. **r3 新增口径（J-2，需裁决）**：预算重置只在「持所有权时真实选中／非超时真实释放／`BeginSession`」发生 ⇒ **预算耗尽后本场不可自愈**（CHURN 实测 acquires 封顶 3；运行期 §3k 实测 `suspensionRetries` 封顶 2 后不再重取）。安全方向正确，但 CPS-4 须写明「**一场内一次坏窗口 = 本场 Route A 退役**」，是否接受请裁决。
5. **低危未修（J-3/J-4）**：J-3 证据表满时 `_suspendedReattempts` 与 `SuspensionRetry` 事件可能不一致；J-4 超时暂停依赖"先清后设"次序（已留陷阱注释）。
6. **J-1（记账口径）**：开发侧反向验证产物（`REVERSE-CHECK PASS=1280 FAIL=17` + 具名失败断言）未按任务契约字段登记 → 质检按**未验证**记账，风险已由 t12 更强的等价证据（真 r2 生产构建复现 P1）闭合；已在运行场景档 §3d 补登。
7. **口径分歧（Descriptive，未改代码）**：Route A 的玩家队排除**不受** MCM 开关 `PlayerTeamExemptionEnabled` 约束，与其它 7 个模块口径不一致。
8. **状态陈旧纪律（2026-09-15 回填更正，以 §0e 为准）**：Astra 2026-09-15 裁定把验证基线推进至**生产语义 `395fe13`**，`a24e921` 另记**等价诊断证据**（限定见 §0e 第 7 项）；**现网 DLL 已回滚为 r3 `395fe13`（`7E5FC25A…`）**，诊断构建已卸载；`main` HEAD 已在其后（含 r1/r2/r3 与证据文档提交），不得把新 HEAD 当作已验证状态。
9. **下轮最小采样项（按 §0b 更新）**：a) 连续变号期间不得重取 → **已达成（多场）**；b) 合格意图变化后允许且仅允许一次重取 → **已达成（2 样本，上限/冷却实机确认）**；c) `ExternalReplaced` 后 ≤10s `Exit(reason=SelectionTimeout)` 且执行器恢复 Movement 写 → **已达成（Δ=9.507–10.006s；执行器实得连续窗口：§3j ≈213.7s、§3k 三段合计 ≈97.2s）**；d) 无重入异常、`RestoreFailures==0` → **已达成（8 场全 0，异常 0）**。**唯一未达成 = 健康 `EdgeRetry` 被兑现（S5 正向形态）→ 已由 Astra 2026-09-15 裁定选①收口（§0e 第 6 项）。**
10. **归因口径问题（Descriptive；Astra 2026-09-15 裁定采用方案 A：只改文档口径、不改码）**：Route A 的写入**不进** `CommandProvenance` 聚合——`RouteAOrderAdapter` 只 `CommandSourceScope.Enter`+`SetMovementOrder`，不调用 `CommandProvenance.Instance.Record`（全仓唯一调用点 = `FormationCommandExecutor.cs:147`）⇒ `[OrderTraceAgg]` 恒为 `CombatBrainRouteA=0/o0`（4 场复现），Route A 的归因只存在于 `[RouteAEvent] source=CombatBrainRouteA` 渠道（内部自洽：36 条 = 12 次落地×3）。**落修口径（§0e 第 9 项）**：S5 归因改为以 `[RouteAEvent]` 的 `OrderApplied/SetterBegin/SetterCompleted` **自洽**验 Route A；`[OrderTraceAgg]` 只验普通执行器与释放后窗口——即上文 §4.9(d) 中「`OrderTraceAgg` 归因正常」**不再作为** Route A 的归因判据。

## 5. 请 Astra 裁决的问题

1. **条件 5**：在 §0b 的 8 场 + §0d 的 4 场运行期证据下（场景 1、2、5 PASS；S1/S2/S3/S4 均 PASS 且有多场样本；Rejoin 成功路径已有 1 次样本；**仅 S5 正向形态始终未取得——r3 类构建累计 6 次覆盖、0 次正向，机制见 §0d**），条件 5 是否**满足**？若否，还缺哪一类证据（§6 为可复现命令）？
2. **本次采样不适用是否可接受**：把"该采样口径下不适用、分支仍未验证"记作非 FAIL 是否符合验收口径？（Rejoin **成功路径**已于 §0b 取得样本；仍缺 `NavigationFailed` 失败路径与更多次成功样本——如需专门配置补验，请指出方式。）
3. **CB-REPAIR-0004（r3 + t12 复核通过；r3 已部署、8 场采样完成、S5 正向形态 3/3 全负）**：是否构成 GO 阻断项，还是可列为 GO 之后的运行期债？
4. **J-2 口径**：接受「一场内一次坏窗口 = 本场 Route A 退役（预算耗尽后本场不自愈）」这一语义吗？若需自愈机制，请指出期望的触发条件与上限。
5. **玩家队豁免口径分歧**：是否需要收敛（使 Route A 也受 MCM 开关约束）？该项当前仅记录，未改代码（功能冻结令内）。
6. **S5 正向形态的验收口径**：请裁决——**①接受离线证据替代**（三次覆盖样本均证明"覆盖后引擎不再回调 CB 行为"这一失败形态，armed 重试的兑现前提不成立），据此关闭 CB-REPAIR-0004 的运行期验收；还是**②要求先做机制确认**（见问题 7）；或**③要求继续采样**（请给出可接受的等待上限/场次）。
7. **诊断构建样本的等价性（原"是否授权部署"已被执行取代，见 §0d）**：探针构建 `a24e921` 已部署并完成采样（`ADCC641E…`，src=dst MATCH）⇒ 请裁定：**该构建上的运行期样本能否计入 CB-REPAIR-0004 的 r3 运行期验收**？其与 r3 的差异仅为「纯增量只读探针 `[RouteASilence]` + 单点异常边界隔离（7 条离线断言 + 反向验证证明诊断异常不改变控制流）」——是否满足"零语义差 + 离线等价"前提；若不满足，请指出需补何种证据。
8. **若 S5 正向形态在当前实现下不可达，是否需要重新设计"覆盖恢复"语义**：现有机制把恢复的希望寄托于"引擎在 10s 内重新选中 CB 行为"（armed 单次边沿重试）；§0b 的 3/3 全负 + "覆盖后外部 Movement 写入由 3 次增至 58 次（分布非均匀，最大间隔 31.0s；来源仍为 `ExternalUnknown`，'原生胜出'待探针确认）"提示该前提可能长期不成立。是否另立卡（冻结令下需先裁决）改为 CB 侧主动、具名、有界的重取？
9. **归因口径收敛方向（新增，见 §0c①）**：Route A 落地写入不进 `CommandProvenance`/`[OrderTraceAgg]` 是**结构性事实**（生产唯一 `Record` 调用点在普通执行器；Route A 走旁路）。请裁决采用哪一方案：**A** 只改文档口径（不改码，可立即执行）／**C** 记录为待裁事项（不改码）／**B** 适配器接线调用 `Record`（改码；须先裁定是否触碰冻结令 §7.2 与 CB-REPAIR-0004 未关台账）／**D** 两渠道聚合合并（改码，风险最高，不建议）。并请确认 S5 验收项中"`OrderTraceAgg` 归因与执行器一致"应改写为何种口径。

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
- **r3 采样绑定表（8 场样本的部署绑定、S1–S5 判定、无效场次、归档日志指针）**：`docs/cps3r-routea-r3-sampling-record-2026-09-14.md`
- **CB-REPAIR-0005 派单 / 交付报告**：`prompts/active/CombatBrain-CB-REPAIR-0005-RouteA覆盖后断流只读诊断派单-2026-09-14.md`（registry `CB-PROMPT-0043`）、`tools/CombatBrain-CB-REPAIR-0005-交付报告-2026-09-14.md`（`CB-PROMPT-0044`）
- **归档日志（研究用途）**：`MBmod/CombatAI/logs/CombatBrain-2026-09-14-full-1600.log`（`D6B80D36…`）与 `-full-2056.log`（`7223BB15…`）；切片 `run-4156cc25-…-twobattles.log`（`54374180…`）、`run-4e885a4e-…-mixed-battle.log`（`E3E60C97…`）、`run-0496f7b6-…-longbattle.log`（`585DCEAD…`）
- 上一轮复审提交物：`docs/cps3r-routea-astra-review-submission-2026-09-08.md`
- **本轮新增调查与复核件（§0c，均在 `MBmod/CombatAI/tools/`）**：
  - ① 归因缺环报告 `cps3r-routea-attribution-gap-report-2026-09-15.md`（`F8F800D3…`）；其复核 `cps3r-routea-attribution-review-2026-09-15.md`（needs_revision）与 `cps3r-routea-attribution-verification-r2-2026-09-15.md`（**PASS**）
  - ② P2 债务整理报告 `cps3r-routea-p2-debt-consolidation-2026-09-15.md`（`889269FB…`）；其复核 `cps3r-routea-p2-debt-review-2026-09-15.md`、`cps3r-routea-p2-debt-verification-r2-2026-09-15.md`（needs_revision）与 `cps3r-routea-consolidated-verification-r3-2026-09-15.md`（**PASS**）
- Route A 收口报告：`docs/cps3r-routea-final-blocker-report.md`
- 验证命令链与采样前置：`测试指南.md`（§0a = E 模式前置）
- 返修台账：`MBmod/CombatAI/prompts/repairs/registry.json`（CB-REPAIR-0001…0006）
- 现行验证态：`docs/knowledge/CURRENT_STATE.md`

---

## 0e. Astra 复审裁定回填（2026-09-15）——**本轮九项裁定 + 3 条 P2**

> 性质：本块是 **Astra 2026-09-15 复审结论的回填登记**（原文见 Astra 复审指令件与裁定文本；registry `CB-PROMPT-0040`，状态 `COMPLETED`——本轮无需再激活）。
> 效力：本块对 §5 的九问逐条给出裁定；与正文其他块冲突时**以本块为准**（正文各块保留为历史证据，不改写其正文）。现行验证态唯一权威仍是 `docs/knowledge/CURRENT_STATE.md`。

| # | 问题（§5） | Astra 2026-09-15 裁定 |
|---|---|---|
| 1 | 条件 5 是否满足 | **判 PASS**（**但**账面在文档落修前保持未通过；**不推出** CPS-4/GO） |
| 2 | 采样不适用是否可接受 | 接受；`NavigationFailed` 失败路径与更多 Rejoin 样本列 **P2 运行期债**（不阻断 0004；不得声称结构不可达） |
| 3 | CB-REPAIR-0004 是否阻断 GO | **不阻断**；**已具备关闭条件**（裁定 7） |
| 4 | J-2 口径 | **实现忠实、无更高规范硬冲突**；「坏窗口」明确定义为「**本暂停期两次重取耗尽且无真实选中**」（**不是**"首次超时即退役"） |
| 5 | 玩家队豁免口径 | 列 **GO 前产品口径裁决项**（不阻断本轮收口） |
| 6 | S5 验收口径 | **选①**：接受「离线证据 + 本轮机制观测」替代，**停止继续等正样本**，无需先取原生权重 |
| 7 | 诊断构建样本等价性 | **可计入** r3 运行期验收，**限定为「r3 的诊断衍生构建」**；绑定须含 `395fe13→a24e921` 差分 / DLL SHA / run·session / 同配置 / 1327·20·4=4 / 异常 0；**不得称同一 DLL、不得外推原生权重** |
| 8 | 是否重新设计覆盖恢复语义 | **另立设计决策卡**，但列 **DEFERRED**（冻结期不授权改码，不阻断本轮收口） |
| 9 | 归因口径收敛方向 | **选 A**：只改文档口径——S5 归因改为以 `[RouteAEvent]` 的 `OrderApplied/SetterBegin/SetterCompleted` **自洽**验 Route A；`[OrderTraceAgg]` 只验普通执行器与释放后窗口 |

**证据链结论**：**PASS**（探针 P1 已闭、无代码阻断）。

**3 条 P2（均不阻断 CB-REPAIR-0004，列运行期/工程债）**：①重试事件 revision 配对；②t12 源码不可重建；③`NavigationFailed` 失败路径与 J-3（证据表满时计数不一致）、J-4（超时暂停依赖先清后设次序）。

**裁定后的验证基线**：推进至**生产语义 `395fe13`**；`a24e921`（诊断构建）**另记为等价诊断证据**（限定见上表第 7 项）；**CPS-4 保持 Pending、GO 不通过**。

**Astra 裁定后的现网与归档现状（回填）**：诊断构建 `a24e921`（`ADCC641E…`）完成采样后**已回滚回 r3 `395fe13`**（现网 DLL SHA256 = `7E5FC25A0EF0559D718B0D5E42994141846291501E3342F94E6C0B1F713FD8D6`，228,352 B，与 `E:\MBmod\_backups\CombatBrain-r3-395fe13-7E5FC25A.dll` 一致）；部署 DLL 内 `[RouteASilence]` 字面量 = **False**（探针已随回滚移除）；采样记录见 `docs/cps3r-routea-diagnostic-sampling-2026-09-15.md`（含裁定 2 要求的等价性绑定 §9）。

**对 §5 的净影响**：九问**全部已裁**，§5 不再是"待裁决"状态；本件的 open 项自本块起仅剩 **P2 三条 + 玩家豁免（GO 前产品口径）**。
