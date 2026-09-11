# 个体控制面退役与产品边界收口 —— 收工报告（CB-PROMPT-0038）

> 派单卡：`prompts/active/CombatBrain-个体控制面退役与产品边界收口-正式派单-2026-09-11.md`（CB-PROMPT-0038，series_id=agent-control-retirement）
> 团队：combatbrain-开发组-20260911（开发12 / 质检5 / Captain）
> 分支：`feature/agent-control-retirement`（worktree `E:\Agent\cb-agent-retirement`，自 `f6634f7` 分出）；**未合入 main、未部署**
> 复核：`docs/qc-review-agent-control-retirement.md` —— verdict **pass-with-runtime-debt**（锚点 `1ed91e1`）

---

## 0. 交付物清单

| # | 派单 §十 要求 | 落点 |
|---|---|---|
| 1 | 个体控制面完整审计表 | `docs/agent-control-surface-audit.md`（§2 逐点表 + §2.9 每 Agent 状态字典 + §2.10 穷尽核对 + §5 保留清单 + §6 裁决台账） |
| 2 | 架构决策说明与 ADR | `docs/adr/ADR-0005-individual-control-plane-retirement.md`（accepted，已登记 `docs/adr/README.md` §5） |
| 3 | 代码修改 | 提交链 `6a33df4`/`7fc07bb`（t2）、`8c4228e`/`8aea592`（t3）、`90d2fbf`（t9）、`b0b1628`（Captain 构建同步）、`1ed91e1`（t10 门禁硬化） |
| 4 | 规范与设置说明修改 | `docs/knowledge/CONSTITUTION.md`（§3.1/§3.2/P2 改写 + 新增 **HC-2**）、`docs/knowledge/NOMENCLATURE.md`（§2 RETIRED）、`CombatBrain/Mcm/CombatBrainMcmSettings.cs`（「玩家队豁免（全控制面）」+ HintText） |
| 5 | 静态门禁与回归测试 | `OfflineTests/AgentWriteStaticChecks.ps1`（**68 条**）、`OfflineTests/Program.cs`（退役断言增删按账目链）、`OfflineTests/RouteAStaticChecks.ps1`（20 条，未改） |
| 6 | 构建及测试结果 | 构建 0 error / 0 warning；离线 **1147 PASS / 0 FAIL**；门禁 68 条全 PASS；RouteA 20 条全 PASS（开发者与 QC 双方自跑） |
| 7 | 游戏内 A/B 验收步骤 | `docs/agent-control-retirement-ab-checklist.md`（A/B 步骤、六类只读计数 + FrameRelease 单列判定口径、玩家队/阵框专项、记录模板） |
| 8 | 风险清单与可回滚点 | 本报告 §6 |
| 9 | 仍保留的 Agent API 写入及理由 | 本报告 §3（= 审计表 §5 K1/L1-L3） |

---

## 1. 收工报告七问（派单 §十末段，逐条回答）

**Q1 哪些旧模块被彻底退役？**
- **X2 手活属性模型**：`CombatBrainAgentStatCalculateModel`（格挡/招架/攻击决定概率/射击误差/随机防守方向五项覆盖）、`HandSkillProfile`、`SubModule.cs` 的 `AddModel` 注册（无等价替代注册）；`CombatBrainSettings.HandSkillStrength` 死开关删除。
- **F4 预测式格挡**：`CombatAiMissionLogic.TickBlock` 整方法、`EnforceShieldUsage` 调用点、`_attackStats`/`GetAttackStats`/`MapAttackDir`/`ToUseDefend`、`AttackStats.cs`/`PredictiveBlock.cs` 整类。
- **F6 个体走位**：`MovementGlue.cs` 与 F6 逐兵内核 `Movement/MovementController.cs`（Advance/StepBack/Strafe/RetreatToAllies/Hold/走形扰动）。
- **F9 个体远程**：逐兵 `SetTargetAgent`、`TryToWieldWeaponInSlot`、后撤 `SetMovementDirection`、`RangedBrain` 逐兵选敌打分 API（`SelectTarget/Score/RangedTarget/ArmorClass/AmmoKind`）与 `_fallingBack` 状态。
- **F12 个体抖动**：`DecisionJitterGlue`、`Compliance/DecisionJitter`（个体语义）、`DecisionDelay.ScaleProfile`、每 Agent 犹豫/延迟/疲劳状态字典。
- **F7 单兵求生腿**：`SurvivalInstinct` 的 `SurvivalAction/SurvivalContext/Decide/HpThreshold`（文件重命名为 `Survival/CasualtyReformSignal.cs`）；`SurvivalGlue` 逐兵撤退腿。
- **X5 逐兵补位执行腿**：`FormationSlotGlue` 逐兵 `SetMovementDirection`、`X5SlotCooldown`、`ShouldRunIndividualLeg`、`SlotAssigner` 的 `FrameDisabledAgents` 跳过集消费。
- **AttackSuppression 链**：`AttackSuppression.cs`/`AttackSuppressionState.cs`（运行期零消费者死状态）与跨 Mission 静态残留 `_suppressedAttack`。
- **每 Agent 状态与死类型**：`FighterProfile` 四字段表 + `FighterProfileFactory`；`_profiles` 字典与 `GetProfile`；`SoldierTier` 无消费者枚举（承接任务执行，见 §5 未闭环项）。

**Q2 哪些逻辑被上收成编队级信号？**
- F7：`CasualtyReformSignal`（伤亡事件累计 + 窗口阈值）→ `IntentType.Reform` 编队级重整信号保留；逐兵 `_wasCritical` 状态不再承担行为决策。
- F9：编队级远程任务判断（是否继续射击 / 是否整体后撤 / 是否转换任务 / 编队级目标优先级）保留为只读编队快照，不再驱动单兵 API。
- F12：迟滞上收至 **指挥层意图切换**（`DecisionDelay.HoldTime`/`SwitchMargin`、`IntentEngine` hold）与 **编队任务切换/命令防抖**（`FormationCommandArbiter`/`Executor`）。
- F6/X5：编队级表达通道统一为 `Formation.SetMovementOrder/Facing/Arrangement`（CPS 唯一执行器）、Formation Mission、Route A 行为权重；X5 保留编队构成分类/职责/Arrangement/纪律/观察性 Slot 数据。
- 阵框释放：上收为「编队级统一接战政策」（编队级触发 + 同政策），非逐兵决策。

**Q3 是否还存在 CombatBrain 与原版 HumanAI 同时控制单兵的路径？**
**仅剩 1 条，且为已裁决保留项**：阵框释放 `Agent.SetFormationFrameDisabled()`（`CombatAiMissionLogic.cs:2097`）。它是**一次性状态开关**（非周期性方向输入），语义为「解除该兵对编队帧框的跟随、交还原版 AI 自由接敌」，因此与原版并不争抢同一个控制量；且带一次性 latch + 5s 冷却 + 玩家队零写入 + 只读计数单列。除此之外，`SetMovementDirection`/`EnforceShieldUsage`/`SetTargetAgent`/`TryToWieldWeaponInSlot`/`AgentDrivenProperties` 写入在**生产代码中不存在**（门禁 68 条含零容忍硬断言）。运行期是否真的不产生争抢/振荡，属 **D-2 运行期债**，未闭环前不得宣称「已证明无双重控制」。

**Q4 玩家队豁免是否已成为全控制面豁免？**
**是（静态与离线口径）**：Formation 命令 / 编队行为权重 / 士兵移动方向 / 格挡换防选敌换武器 / AI 士兵战斗能力属性 / 阵框释放 六类对玩家队均零主动写入，且 MCM 文案改为「玩家队豁免（全控制面）」并补全 HintText（消除「玩家本人 ≠ 玩家队」歧义）。豁免关闭时也不复活已退役逐兵系统（写入点已不存在）。运行期两态计数证据属 **D-1/D-3**。

**Q5 是否改变了 CPS 当前架构或验证口径？**
**架构未变**：CPS 唯一执行器 / Arbiter / Route A / 归因链语义未动，`RouteAStaticChecks` 20 条全 PASS。**验证口径变化**：离线断言基线 1241 →**1147**（−31 t2 / −48 t3 / −15 t9，逐条名单见 QC 报告 §4），门禁 55 →**68** 条；这些变化**只源于退役模块与门禁硬化**，无与退役无关的断言消失（QC 独立复算）。运行期验证口径新增一条硬约束：**未完成 A/B 前任何结论只能是 `pass-with-runtime-debt`**。

**Q6 是否污染了既有 CPS 测试 DLL、日志或证据？**
**未污染**：部署 DLL 与 t6 基线逐字节一致（SHA256 `A39AC964…FB22`，全程未部署）；主检出 `f6634f7` 的 5 个未跟踪 docs 未被清理或覆盖；`%TEMP%\CombatBrain.log` 仅**尾部追加**且时间戳早于团队首次写入、为旧构建会话自然收尾；`telemetry.json` 为 Mission 结束覆盖写语义；`.agent-teams` 归档未变。证据：`tools/cps-evidence-baseline-2026-09-11.md` + QC 报告 §8。

**Q7 哪些规范文本仍需 Integration Owner 裁决？**
仅 **1 项**：**合并时点与冻结令记录**（M8）——分支未合并、未部署；本任务为「减写者」与冻结令同向，但属架构级变更，需 Integration Owner 确认合并时点并记录 waiver/architecture decision。另有 3 项**非阻断的后续卫生/治理项**（见 §5）。CONSTITUTION/NOMENCLATURE/ADR 已由 Captain / Integration Owner 角色位按派单 §七 完成修订。

---

## 2. 证据分类（不得以「测试通过」代替行为解释）

| 类别 | 能证明什么 | 证据 | 不能证明什么 |
|---|---|---|---|
| 源码 | 写入点存在/不存在、语义与并行性 | 审计表 §2 全表（`文件:行`）、领域文件头 P2 反编译坐标 | 运行期是否真的更稳、观感是否改善 |
| 静态扫描 | 生产代码零写入、允许名单极窄 | `AgentWriteStaticChecks.ps1` 68 条（退役文件不存在 + 13 类型零引用 + 防过度退役） | 引擎原生 AI 与 CB 的运行期交互 |
| 离线测试 | 纯逻辑契约与编队级信号仍在 | `OfflineTests` 1147 PASS / 0 FAIL；`RouteAStaticChecks` 20 条 | 帧级时序、动画翻转、动作振荡 |
| 游戏内观感 | 抽搐/小碎步/盾向翻转是否消失、敌方战术是否保留、500 人性能 | **未执行（D-1/D-2/D-3）** | —— |

---

## 3. 仍保留的 Agent / 编队 API 写入（派单 §十.9）

**Agent 级主动写入：1 项**
- `Agent.SetFormationFrameDisabled()`（`CombatBrain/Ai/CombatAiMissionLogic.cs:2097`，判定源 `FrameReleaseGlue.cs`）
  —— 编队级统一接战政策（产品裁决保留）；理由与恢复方法见审计表 §5 K1；运行期债 D-2。

**编队级写入（边界透明，非 Agent 级）**
- `Formation.SetMovementOrder/SetFacingOrder/SetArrangementOrder`（`FormationCommandExecutor.cs` 唯一执行器 + `RouteAOrderAdapter.cs` Route A 单编队）；
- `Formation.SetControlledByAI(bool)`（ATC-5R 租约 PoC，默认关）；
- `FormationAI.AddAiBehavior` / 行为权重（Route A 表达 Movement 域）。

**只读诊断（不改变行为）**：`CommandProvenance`/`SetOrderObserver`、`DecisionObservatory`、`DiagnosticCounters`（六类写入计数 + FrameRelease 单列）、`BattleBenchmark`、`BattleTelemetry`、`AnomalyDetector`、`InGameFeedback`、`CombatLog`。

---

## 4. 范围修正与账目（SCOPE-AMEND）

| 修正 | 内容 | 归属 |
|---|---|---|
| SCOPE-AMEND-1 | t2 inScope 过窄：连带路径 `CombatBrain/Ranged/`、`CombatBrain/Combat/`、`CombatBrainSettings.cs` 与 4 个 `Ai/` 注释文件 | t7（Ranged+Combat）、t8（CombatBrainSettings）承接；4 个 Ai/ 文件由 t3 的 `CombatBrain/Ai/` 承接 |
| SCOPE-AMEND-2 | t3 inScope 未含三处退役内核：`Movement/MovementController.cs`、`Survival/SurvivalInstinct.cs`、`Combat/FighterProfile.cs` | t9 承接执行；t10 承接 `OfflineTests/` 同步 |
| SCOPE-AMEND-3 | Captain 直接提交：`b0b1628`（OfflineTests 构建性同步）、`9a584cb`（审计表回填）、本收口提交（规范/ADR/本报告） | Captain（t5 声明） |

**账目链**：离线断言 1241 →(t2 −31) 1210 →(t3 −48) 1162 →(t9 −15) 1147；门禁 55 →(t10) 68；
范围账目 37 个变更路径逐文件唯一归属、无未声明路径（QC 报告 §3）。**口径更正留痕**：Captain 提交 `b0b1628` 的
commit message 写「14 条断言」为笔误，diff 实测 **15 条**（`FighterProfileFactory.Classify` 12 + `Create` 3），
注释与账目已按 15 修正；commit message 不追溯改写（不得伪造历史）。

---

## 5. 未闭环项（交付时点状态）

| # | 项 | 类别 | 闭环路径 |
|---|---|---|---|
| D-1 | §九 游戏内 A/B（A1–A8） | 运行期债 | `ab-checklist` §3/§7；游戏进程退出后由 Integration Owner 授权独立构建部署 |
| D-2 | 阵框释放无重复抢写/无动作振荡 + 恢复后重新整齐 | 运行期债 | `ab-checklist` §5 + `[Diag] FrameRelease` 单列；异常 → 回报 Captain |
| D-3 | §八.7/§八.8 豁免开/关两态的运行期写入计数 | 运行期债 | 并入 D-1 采集 |
| M8 | 合并时点与冻结令记录 | 待 Integration Owner 裁决 | 本报告 §6 + ADR-0005「实施与合并」 |
| M12/F-7 | `CombatBrain/Enums.cs:36` `SoldierTier` 死枚举删除 + `Enums.cs:44` 注释更正 | **已执行**（t11 `31823b6`；t12 增量复核 pass，锚点 `31823b6`） | 完成；全仓零代码引用，注释指向 `Formation/TroopClassifier` |
| MS-1 | `CombatBrain_Master_Spec.md`（manager 侧 long-form 产品规格）中的 F4/F6/F7/F9/F12/X2/X5 个体层表述同步 | 跨仓文档层（不在本仓 `docs/` 范围，本轮未改） | 由 Integration Owner / manager 侧按 ADR-0005 与 NOMENCLATURE RETIRED 语义同步（不得由本团队直接改 manager 资产） |
| F-3/F-4/F-6/F-7 | `BracePlan.survivalRetreating` 既有死参数注释（**登记不动**）；`DiagnosticCounters` 死字段清理 + 哨兵标注（**已执行** t11）；门禁补 Agent 级 `SetControlledByAI`/`AddAiBehavior` 禁止模式（**已执行** t11，68→73 条）；`SoldierTier` 死枚举退役（**已执行** t11） | 已闭环（F-3 为登记保留） | t11 `31823b6` + t12 增量复核 pass（`c89276a`）；残余观察：新禁止模式的 Agent/编队判定依赖收信者变量命名启发式（回归哨兵，非安全边界，不立项） |

**纪律**：D-1~D-3 未闭环前，本任务链任何对外结论只能是 **`pass-with-runtime-debt`**。

---

## 6. 风险清单与可回滚点

**风险**
1. **观感取舍**：失去 F4/F6/F7/F9/F12/X2 带来的个体级观感增强（早期「观感补强」成果）；若 A/B 显示敌方战术辨识度下降，需评估是否用编队级表达补回（不得复活逐兵写入）。
2. **阵框释放（K1）**：唯一保留的 Agent 级写入；风险=运行期出现重复抢写/动作振荡（D-2）。缓解=一次性 latch + 5s 冷却 + 只读计数单列；触发即退役该调用。
3. **冻结令交互**：架构级变更在 CPS GO 前合并需 Integration Owner 记录（M8）。
4. **断言基线下降**：1147 < 1241 是退役的必然结果，已逐条可追溯；后续若出现「为凑绿而删断言」属违规（门禁含防过度退役断言）。
5. **残留登记**：`BracePlan.survivalRetreating` 死参数注释为**既有残留**（明确不动，见 §5 F-3 行）；`DiagnosticCounters` 哨兵计数标注与 `SoldierTier` 死枚举均已随 t11 `31823b6` **处理完毕**（见 §5 M12/F-4/F-6/F-7 行），非未决残留。均不影响行为。

**可回滚点**
- 分支级：`feature/agent-control-retirement` 整体丢弃（或 `git checkout` 回分叉点 `f6634f7`）——本分支**未合入 main、未部署**。
- 逐项级：阵框释放单点回滚 = 删除 `CombatAiMissionLogic.cs:2097` 调用 + 清 `SlotAssigner` 跳过集消费；
  F7 编队级信号回滚 = 恢复 `SurvivalGlue` publish（注意不得复活逐兵腿）。
- 规范级：CONSTITUTION HC-2 与 §3.1/§3.2 修订、NOMENCLATURE RETIRED 标记、ADR-0005 均随本提交可整体回退（`git revert`）。
- **禁止**以「恢复旧功能」名义复活逐兵写入：按 ADR-0005「Superseded by」条款，需新 ADR + 显式 Waiver。
