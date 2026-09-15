# ADR-0005：CPS 下一阶段执行架构与首个扩展域

> **状态**：`PROPOSED`（架构决策已具备被接受条件，但**启用仍以 CPS-4 GO 为前置**；本 ADR 不得被用来替代 GO 判定）
> **日期**：2026-09-15　**决策者**：用户（Project Owner）　**记录者**：Project Captain
> **依据**：`docs/cps4-hardgate-checklist-2026-09-08.md`（14 条硬门）、`docs/knowledge/CONSTITUTION.md` §7 命令所有权与写入纪律、`docs/cps3r-routea-astra-rereview-brief-2026-09-14.md` §0e（外部九项裁定）、`MBmod/CombatAI/CombatBrain CPS-4 推进评估报告-致外部独立审阅-2026-09-15.md`（外部独立审阅，GPT）
> **对应条件**：CPS-4 硬门 **条件 14**

---

## 1. 决策（待用户批准）

1. 下一阶段执行架构采用**混合架构**：**路线 A 为默认主路径**（CB 自定义 Behavior + 唯一批准适配器 `RouteAOrderAdapter` 写入 Movement 域），**执行器仲裁**承接 Facing / Arrangement 域，**路线 B（控制租约）退居窄域保险丝**，仅在明确不可用场景启用。
2. **首个扩展域 = `Facing`**（顺序：**Facing → Arrangement → 非野战场景**）。
3. 本 ADR **仅固定架构与门禁**；**启用动作以 CPS-4 判 GO 为前提**，冻结令解除亦以 GO 为前提。

## 2. 背景与约束

- 主线 CPS 的目标是「消除 CB 与原版 AI 的命令写入互刷」，其技术路线先在 CPS-2/CPS-3 做过 A/B 对照，CPS-3R 收口时把 Route A 做成**默认主路径**的候选。
- 当前已验证基线：`395fe13`（r3，生产语义），现网 DLL = 该构建（`7E5FC25A…`）；离线 1297 PASS / 0 FAIL。
- 硬约束（不得被本 ADR 绕过）：功能冻结令（`CONSTITUTION.md:159`）禁止新增直接 `SetOrder` 调用点；命令写入纪律（`:163-:166`）要求玩家队豁免「按各功能既有 MCM 开关语义、语义统一、不逐功能发明」；任何对规范/系统契约的合法例外必须走 **Waiver**（`docs/knowledge/INDEX.md` §4）。

## 3. 命令域所有权表

| 命令域 | 唯一所有者（默认路径） | 保险路径 | 现状态 |
|---|---|---|---|
| **Movement** | Route A：CB Behavior 竞争胜出 → `RouteAOrderAdapter.TryApply` 唯一 setter | 控制租约（路线 B，窄域） | 已实现；`a879688`/r3 收口 |
| **Facing** | 普通执行器 `FormationCommandExecutor`（仲裁域） | — | 已实现（未扩展） |
| **Arrangement** | 普通执行器 `FormationCommandExecutor`（仲裁域） | — | 已实现（未扩展） |
| **Morale / 其它外围 Glue** | 各 Glue 自身（受 MCM 开关约束） | — | 已实现 |

**唯一写入纪律**：除 `FormationCommandExecutor.ApplySetOrder`（3 处 setter）与 `RouteAOrderAdapter`（1 处）外，**不得**新增编队级直接 `SetOrder` 调用点；四类 CB Behavior 只调适配器（静态扫描 4=4 无新增，见 `OfflineTests/RouteAStaticChecks.ps1`）。

## 4. 进入 / 退出条件

| 路径 | 进入 | 退出 | 依据 |
|---|---|---|---|
| **Route A** | 普通单机野战 + `Cps3Mode=E_RouteA` + 观测挂载 + CB Behavior 被引擎选中 + `CanExecuteSelected` 全量复核通过 | `ExternalReplaced` 交付超时（10s，`SelectionTimeout`）、`LostAIControl`、`EmptyOrInvalid`、热关闭（`Disabled`）、Mission 结束 | r3 语义 + J-2（§5） |
| **控制租约（路线 B）** | AI 编队被 Denied 且远离 MainBody（>30m）等租约条件满足 | Release 三条件 / watchdog TTL / MissionEnding / identity 变更 / 开关关闭（fail-open） | CPS-1 规格 |
| **普通执行器** | 非 Route A 域或 Route A 未持有该域所有权 | 每拍按仲裁结果 | CPS-1 规格 |

**J-2「本场退役」语义（已裁决）**：预算（≤2 次重取）耗尽后本场**不自愈**；「坏窗口」= **本暂停期两次重取耗尽且无真实选中**（不是「首次超时即退役」）。自愈机制改造另立卡 `CB-REPAIR-0007`（DEFERRED）。

## 5. fail-open 矩阵（必须保持）

| 触发 | 行为 | 恢复时限要求 | 现证据 |
|---|---|---|---|
| 玩家实际接管（`PlayerOwner` / `HasPlayerControlledTroop` / `IsPlayerTroopInFormation`） | **无条件**停止 CB 写入 | ≤1 逻辑拍 | 离线断言有；**实机样本缺（条件 6）** |
| 玩家队（`Team.IsPlayerTeam`） | **受 `PlayerTeamExemptionEnabled` 控制**（默认豁免） | 不适用 | **当前实现未接开关 → 见 §10 开放项 `CB-REPAIR-0008`** |
| 热关闭（MCM 关 / 切出 E 模式） | `Exit(Disabled)` + `Restore` 原权重 | ≤1 拍 | 场景 2 PASS |
| Mission 结束 / 清空 / 增援 | `RestoreAll` 幂等、无残留 | 不适用 | 场景 5 PASS（含 3 次 `LostAIControl`、1 次 `EmptyOrInvalid`） |
| 异常路径（导航失败 / 未申请到所有权） | 不写入 + 撤销权重 + `Restore` | 不适用 | 离线有；Rejoin 失败路径**无样本** |

## 6. 回滚与防双写

- **回滚开关**：MCM `Cps3Mode` 退出 E 模式即恢复原版权重（`Restore` 已实现）；部署回滚以 **DLL 备份**为唯一字节等价来源。
- **恢复时限**：玩家接管 / 热关闭均要求 ≤1 逻辑拍（条件 6/7 待实机确认）。
- **禁止双写**：Route A 持有 Movement 域期间，普通执行器对该域跳过（`CbRouteAPoC.IsMovementOwnedByRouteA`）；反向亦然。

## 7. 归因渠道矩阵（并声明各渠道「不代表什么」）

| 渠道 | 记录什么 | **不代表什么** |
|---|---|---|
| `[RouteAEvent]`（`Intent` / `BehaviorSelected` / `SetterBegin` / `SetterCompleted` / `OrderApplied` / `ExternalReplaced` / `ExternalWhileActive` / `Exit` / `Restore` / `ExternalAfterExit`） | Route A 的行为选择、写入落地、被覆盖、释放与释放后外部写入；含 `revision` / `setterSeq` / `observerSeq` / `identity` | 不代表引擎**为何**选择该 Behavior；不代表原生侧权重 |
| `[OrderTraceAgg]` / `[OrderTrace]`（provenance 聚合） | 普通执行器（`FormationCommandExecutor`）5s 与终局聚合；普通执行器与释放后窗口 | **不包含 Route A**（`CombatBrainRouteA` 恒为 0）——**不得**用作 Route A 落地或控制权指标 |
| `[RouteASilence]`（只读诊断，采样期构建） | `ExternalReplaced` 后每 ≥1s 一拍：`activeIdx` / `activeType` / `aiSame` / `aiCtl` / CB 侧权重 | 不代表原生权重数值；不代表「永久不可达」；该行**仅存在于诊断衍生构建** |
| `[ControlLease] summary` | 租约周期快照（`active` / `ttl` / `minhold`） | 不含取得/释放事件、不含权重恢复 |
| `[RouteA] run=` 汇总 | 每 5s：`selected/applied/replaced/externalWhileActive/edgeRetries/weightFlap/restoreFailures/suspensionRetries/lastSelectedAt` | `restoreFailures=0` 只是 Route A 侧计数，**不代表**原版 AI 恢复时限已验 |

**硬性声明（CPS-4 基线重建时必须写出）**：**归因渠道①（provenance 聚合）不含 Route A**；Route A 的落地计数**只认** `[RouteAEvent]` 的三事件自洽（`OrderApplied` / `SetterBegin` / `SetterCompleted`）。

## 8. 性能预算

| 段 | 预算 | 实测（200 人，E_RouteA 场） | 待办 |
|---|---|---|---|
| `coordination`（仲裁） | 可解释、非高频 | 0.001 ms/帧 | — |
| `executor` | 同上 | 0.000 ms/帧 | — |
| `lease` | 同上 | 0.001 ms/帧 | — |
| 整体 `avgAi` | **500 人 < 5 ms** | 历史 500 人 0.07 ms（余量 70×）；**当前控制面未在 500 人复测（条件 12）** | 需实机 Bench |

**限制**：诊断衍生构建（`a24e921`）的性能数据**不得**用于 500 人结论。

## 9. 扩展域准入门与验证门

- **准入门（新域扩展必须全满足）**：①该域有明确唯一所有者且不与既有路径重叠；②有可观测证据渠道（不得依赖不可观测状态）；③有离线断言覆盖；④有单场实机验收判据；⑤有回滚触发器（异常即退回原版权威）。
- **逐阶段验证门**：域扩展 → 离线断言（只增不减）→ 静态 setter 扫描（不得新增调用点）→ 单场实机 → 归因三渠道一致性核对。
- **回滚触发器**：`restoreFailures>0`、释放后无 `ExternalAfterExit`、同一域出现双写、`avgAi` 超预算。
- **选定顺序（本轮决策）**：**Facing → Arrangement → 非野战场景**。理由：Facing 状态空间小于 Arrangement、可观测性更好（`CurrentFacingOrder` 已有字段）、回滚容易；非野战扩展会同时触碰任务类型边界（攻城/守城/突围 fail-open），应最后做。

## 10. 被拒方案与代价

| 方案 | 位置 | 代价 / 理由 |
|---|---|---|
| 全量 `ClearTacticOptions()` 换取路线 A 独占 | CPS-2 研究报告 | 破坏原版战术决策，副作用面大 |
| 适配器补一次 `CommandProvenance.Record` 以求「单一计数器」 | 归因口径讨论（Astra 裁定 5） | 会丢失 `RouteAEvidence.cs:143` 对「直接调用 vs 重入调用」的严谨区分；且属新增写入接线，触碰冻结令。**长期应改为统一报告/导出模型**，不是补一行 `Record` |
| 冻结期改码统一玩家豁免 | 本轮 | 冻结令禁止；已改为立卡 `CB-REPAIR-0008` 登记 |
| 让 Route A 覆盖 Facing/Arrangement 域 | 早期设想 | Facing/Arrangement 不经 Behavior 通道，硬做会引入第二个写入面 |

## 11. 与其它约束的关系

- **冻结令**：本 ADR 不解除冻结；任何启用动作须在 GO 之后且按卡执行。
- **玩家豁免规范（`CONSTITUTION.md` §7.3）**：架构上保留「手动接管信号无条件保护」；`IsPlayerTeam` 一类**策略性豁免**必须与 MCM 开关一致（当前不一致 = `CB-REPAIR-0008`）。
- **自愈设计债（`CB-REPAIR-0007`）**：本 ADR 固定「J-2 本场退役」为现状语义；若未来引入自愈，须新增准入门条目并给出有界触发条件。

---

## 附：状态与生效条件

- **本 ADR 状态**：`PROPOSED`。用户批准后置 `ACCEPTED`。
- **无论状态如何**：**Route A 的启用、扩展域开工、冻结令解除，均以 CPS-4 判 GO 为前置**；CPS-4 现为 **Pending（GO 不通过）**。
- 生命周期权威：本件属架构决策记录（ADR）；Prompt 生命周期以 `MBmod/CombatAI/prompts/registry.json` 为准，本件不改变任何 Prompt 状态。
