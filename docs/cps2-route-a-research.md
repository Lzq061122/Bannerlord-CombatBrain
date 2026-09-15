# CPS-2 路线 A 可行性 + 第三方 Mod 架构研究报告

> **历史快照（入库标注 2026-09-15）**：本件为 **2026-09-07** 时点的研究快照，**内容不随后续裁定更新**；其中的状态判断（如「未完成/未部署/待裁决」）**不得当作当前事实**。
> 现行验证态唯一权威 = `docs/knowledge/CURRENT_STATE.md`；现行归因与覆盖语义口径见 `docs/cps4-hardgate-checklist-2026-09-08.md` 的 2026-09-15 更新节与 `docs/cps3r-routea-astra-rereview-brief-2026-09-14.md` §0e。

> 状态：研究完成（纯研究，零代码改动）。
> 日期：2026-09-07。
> 代码仓：`E:\Agent\6a9268112aeaf49072bef052`。
> 任务代号：CPS-2（t20 cps2-route-a-research）。
> 证据源：v1.4.8 官方反编译源码 `E:\Agent\6a930c301898716386ed6c01\BannerlordSage\dist\games\bannerlord\assets\Source`（下文行号均为该目录下文件）。
> 第三方源：RBM `E:\MBmod\FIX\rbm_decompiled\RBMAI`；OF `E:\MBmod\IDEA\_mod_research\FrontlineMod_decompiled`；Cunning Lords `E:\MBmod\IDEA\CunningLords`（仅编译 DLL，无本地源码，历史参考）。

---

## 1. 路线 A 可行性（规格 §6.1，六问逐答）

### Q1：能否通过公开 AddTacticOption / 行为权重 / 自定义 TacticComponent / BehaviorComponent 表达 Hold/Advance/Rejoin/Retreat？

**结论：Movement 域可行；Facing/Arrangement 域不直接表达。**

公开 API（均公开/可覆盖，无需 Harmony 阻断）：

| 机制 | 位置 | 说明 |
|---|---|---|
| `Team.AddTacticOption(TacticComponent)` | `Team.cs:415` | 加自定义 Tactic |
| `Team.RemoveTacticOption(Type)` / `ClearTacticOptions()` | `Team.cs:423/431` | 移除/清空 Tactic（§6.1 禁止全量 Clear） |
| `TacticComponent.GetTacticWeight()` | `TacticComponent.cs:589` | `protected internal virtual`，自定义 Tactic 可覆盖 |
| `Formation.AI.AddAiBehavior(BehaviorComponent)` | `FormationAI.cs:133` | 加自定义 Behavior |
| `Formation.AI.SetBehaviorWeight<T>(float)` | `FormationAI.cs:120` | per-Formation 设行为权重 |
| `Formation.AI.ResetBehaviorWeights()` | `FormationAI.cs:358` | 重置 |
| `BehaviorComponent.GetAiWeight()` | `BehaviorComponent.cs:154` | `protected abstract`，自定义 Behavior 覆盖 |
| `BehaviorComponent.CalculateCurrentOrder()` | `BehaviorComponent.cs:172` | `protected virtual`，产出 MovementOrder |
| `BehaviorComponent.CurrentOrder`（MovementOrder） | `BehaviorComponent.cs:59-70` | Behavior 持续写入的当前命令 |

关键限制：`FormationAI.TickOccasionally` 只调用 `_formation.SetMovementOrder(behaviorComponent.CurrentOrder)`（`FormationAI.cs:288`），且仅当 `_formation.IsAIControlled`（`FormationAI.cs:286`）。Behavior 只表达 **Movement** 域；`Facing` 仅以字段 `CurrentFacingOrder` 存在于 BehaviorComponent（`BehaviorComponent.cs:25`），未见 FormationAI 应用；**Arrangement** 由 Tactic/FormationTactics 管理，不经 Behavior。因此：

- Hold/Advance/Rejoin/Retreat 均为 Movement 域，可用自定义 BehaviorComponent（override `GetAiWeight` + `CalculateCurrentOrder` 产出 `MovementOrderStop/Advance/Move/Retreat`）表达。
- **Facing/Arrangement 无法经 Behavior 表达**，需保留原版或走 CB 执行器（路线 B）。

### Q2：是否必须清除/降权特定冲突战术？

**结论：是，但只需降权，不必全量 Clear。**

证据：`FormationAI.FindBestBehavior` 选 `max(GetAIWeight() × WeightFactor)`（`FormationAI.cs:179`）；`TeamAIComponent.MakeDecision` 选 `max(GetTacticWeight() × (当前?1.5:1))`（`TeamAIComponent.cs:301`）。要保证 CB 意图胜出，需把冲突 Behavior 权重设 0（`SetBehaviorWeight<T>(0f)`）或移除冲突 Tactic（`RemoveTacticOption`）。RBM 正是如此（见 §2）。红线：§6.1 禁止 `ClearTacticOptions()` 重建整队、禁止替换整个 TeamAI。

### Q3：能否只影响一支 Formation 或一个明确行为域？

**结论：Formation 级可以（per-Formation 权重）；命令域仅 Movement。**

证据：`SetBehaviorWeight<T>` 是 per-Formation（`FormationAI.cs:120`），可只对目标编队设权。但 `TacticComponent` 是 Team 级，管理全队 `_mainInfantry/_archers/_leftCavalry/_rightCavalry/_rangedCavalry`（`TacticComponent.cs:26-34`）。命令域上 Behavior 只写 Movement（见 Q1）。

### Q4：Tactic 切换 / 增援 / Formation 拆分 / 玩家接管时是否自然恢复？

**结论：玩家接管天然恢复；拆分/增援会重置 CB 权重需重设；Tactic 切换不自动清 CB 权重。**

证据：
- 玩家接管：`IsAIControlled=false` → FormationAI 不 `SetMovementOrder`（`FormationAI.cs:286-288`）；`BehaviorComponent.OnLostAIControl`（`BehaviorComponent.cs:127`）。**天然安全**。
- Formation 拆分：`SplitFormationClassIntoGivenNumber` 对拆分编队 `ResetBehaviorWeights()` + `SetDefaultBehaviorWeights`（`TacticComponent.cs:455-456`）→ CB 权重丢失，需重设。
- Tactic 切换：`MakeDecision` 每 5s（`TeamAIComponent.cs:203-206`）按权重重选，当前 Tactic 1.5x 粘滞（`:301`）；CB 设的 per-Formation 权重不受 Tactic 切换自动清除。
- 增援/新编队：走默认行为集，CB 权重需在 `OnUnitAddedToFormationForTheFirstTime` 或每拍重设。

### Q5：与 RBM 类改写同一 Tactic/Behavior 的冲突面？

**结论：高冲突（同层对刷）。**

RBM 用 Harmony 在 Tactic/Behavior/SetMovementOrder 层改写（详见 §2），CB 路线 A 设置权重/自定义 Behavior/Tactic 会与 RBM 在同一 Formation/Team 上叠加，无稳定仲裁（后写者胜），即把"管线外对刷"搬进"管线内对刷"。

### Q6：是否需 Harmony 阻断？

**结论：表达不需 Harmony；保证排他需 Harmony 阻断（CB 红线禁止）。**

路线 A 表达走公开 API（无需 Harmony）。但保证排他需 Harmony 阻断命令（如 RBM 的 SetMovementOrder 修改 Patch），而 CB 按 CPS 红线只能用只读 Harmony（CPS-0），不得 Harmony 阻断。故路线 A 只能"降权 + 忠实执行"，无法硬排他；需要硬排他的域走路线 B 租约（`SetControlledByAI(false)` 公开 API）。

---

## 2. 第三方 Mod 架构对比表

| 维度 | RBM AI | Organized Frontline | Cunning Lords（历史参考，版本陈旧） |
|---|---|---|---|
| 本地源 | `E:\MBmod\FIX\rbm_decompiled\RBMAI` | `E:\MBmod\IDEA\_mod_research\FrontlineMod_decompiled` | `E:\MBmod\IDEA\CunningLords`（仅 DLL/PDB） |
| 介入层级 | Tactic/Behavior/`Formation.SetMovementOrder` | `HumanAIComponent`（Agent 级个体位置/朝向/速度） | 敌方战斗 AI + 阶段计划（重做，版本陈旧不可据此推断 v1.4.8 API） |
| 如何避免管线外平刷 | 管线内：`ClearTacticOptions` 重建 + 自定义 RBMTactic + 自定义 Behavior + `SetBehaviorWeight` | 不清理战术；只在 Agent 层近距离覆盖 | 历史 |
| 清除/降权原版 | `EarlyStartPatch` Postfix `ClearTacticOptions()` 后重建（只留 TacticDefensiveEngagement/DefensiveLine/FullScaleAttack/CoordinatedRetreat 等少数）（`Tactics.cs:208/262/278/344`）；`TacticComponent.SetDefaultBehaviorWeights` Prefix 返回 false 整体替换（PullBack/Stop/Reserve=0，Charge=0.2/1.0）（`Tactics.cs:659-677`）；`GetTacticWeight` Postfix 降权（×0.75/0.6）（`Tactics.cs:491-494/614-651`） | 无 | 历史 |
| Patch 方式 | Harmony：`Formation.SetMovementOrder` Prefix **修改 input**（Charge 重定向到最近大敌编队）（`OverrideSetMovementOrder.cs:12-27`）；`SetDefaultBehaviorWeights` Prefix（返回 false）；`GetTacticWeight` Postfix；`EarlyStart` Postfix | Harmony `PatchAll`：`HumanAIComponent` 前缀覆盖个体阵型帧（`FrontlineCoreAI.cs:16-18`），`OnMissionBehaviorInitialize` 注入 `FrontlineCleanupBehavior`（`SubModule.cs:43-47`） | 历史 |
| 持续写入 | 自定义 `BehaviorComponent.CurrentOrder`（管线内，经 FormationAI 落地） | Agent 级位置/输入覆盖（近战责任区内） | 历史 |
| 冲突点 | 与任何改 `SetMovementOrder`/Behavior 权重的 mod 冲突（含 CB 执行器、CB 路线 A 权重） | 与改 Agent AI/HumanAIComponent 的 mod 冲突 | 历史 |
| 可借鉴原则 | 自定义 Behavior 表达战术、`SetBehaviorWeight` 降权、`GetTacticWeight` 降权、per-Formation 权重 | **"明确责任区（接敌近距离）+ 范围外 fail-open"** 可映射到命令域（可见区满血 AI，其余交原版） | 不借鉴 |
| 不适合 CB 处 | `ClearTacticOptions` 重建（§6.1 禁）、修改 `SetMovementOrder`（红线，CB 只读）、整体替换 `SetDefaultBehaviorWeights` | Agent 级而非 Formation 级（CPS 三命令域是 Formation 级） | 版本陈旧 |

---

## 3. ADR 九维度对比输入（§6.3，路线 A vs 路线 B）

| 维度 | 路线 A（Tactic/Behavior 融合） | 路线 B（控制权租约） |
|---|---|---|
| 排他性 | 无硬排他；靠权重降权竞争，原版仍可能写 | `SetControlledByAI(false)` 硬排他（公开 API，已核实） |
| 玩家接管安全 | 天然安全（`IsAIControlled=false` 停写，FormationAI.cs:286） | 已实现（P1 玩家接管绝不抢回） |
| Formation 生命周期 | 拆分/增援重置权重需 CB 重设 | identity 对象哈希防复用（已实现） |
| 野战/攻城扩展性 | Behavior/Tactic 主覆盖野战；攻城需自定义 Behavior | 租约 fail-open 非野战；攻城同 fail-open |
| 第三方兼容 | 与 RBM 高冲突（同层对刷，无稳定仲裁） | 与 RBM 冲突较低；但 RBM 仍可能 SetMovementOrder 写入 |
| 版本维护成本 | 依赖 Tactic/Behavior 内部 API（`GetAiWeight/CalculateCurrentOrder` 为 protected，脆弱） | 依赖公开 `SetControlledByAI/IsAIControlled`（已核实稳定） |
| 性能 | 每 5s Tactic 决策 + per-formation behavior tick，开销低 | 租约 watchdog + 仲裁，开销低 |
| fail-open | 权重降权后原版可兜底；但无排他保证 | SetControlledByAI 有 watchdog/TTL/玩家/任务 fail-open |
| 需 Harmony 阻断 | 表达无需；保证排他需 Harmony 阻断（CB 红线禁止） | 无需（`SetControlledByAI` 公开 API） |

**ADR 输入结论（混合架构优先，§6.3）：** 路线 A 表达高层意图（Movement 域 Hold/Advance/Rejoin/Retreat 用自定义 Behavior + per-Formation 权重；Facing/Arrangement 保留原版）；路线 B 仅在 A 无法忠实表达或需硬排他时对具体域启用。不推荐全量租约或全量 TeamAI 替换作为默认答案。

---

## 4. 《命令来源基线报告》输入（§15.4）

- 路线 A 下 CB 自定义 Behavior 与原版 Behavior 都在管线内 `SetMovementOrder`，CPS-0 setter 观测（Harmony 只读 Prefix + `CommandSourceScope`）可区分 CB 来源 vs ExternalUnknown。
- RBM 等第三方 mod 会改写 `SetMovementOrder`/Behavior 权重，`ExternalUnknown` 可能包含第三方 mod（不可冒充 Native，§4.1）。
- 建议 CPS-3 A/B 场景同时采样"仅 CB"与"CB+常用 mod 组合"两组日志，二者结论不得混同（§4.4）。

---

## 5. 已知限制与后续

- 本卡纯研究，零代码改动；路线 A PoC 实现、路线 B 租约整合（`FormationControlLeaseGlue` 2 处 SetOrder 经唯一执行器）属 cps2-ownership-poc 后续卡。
- Cunning Lords 无本地反编译源码，仅编译 DLL，版本陈旧，未据此推断任何 v1.4.8 API 可用性。
- 未复制任何第三方反编译代码进生产仓（§9 末段）。
