# CPS-3 运行期 A/B 阶段报告（2026-09-07）

> **历史快照（入库标注 2026-09-15）**：本件为 **2026-09-07** 时点的 A/B 采样记录，**内容不随后续裁定更新**；其中「条件 5 未满足 / CPS Pending / 挂起」等判断为该时点口径，**不得当作当前事实**。
> 现行状态：条件 5 已由 Astra 2026-09-15 评审判 PASS，但 **CPS-4 仍 Pending、GO 不通过**；权威见 `docs/knowledge/CURRENT_STATE.md`，裁定见 `docs/cps3r-routea-astra-rereview-brief-2026-09-14.md` §0e。

> 状态：**CPS-3 运行期验证挂起（D 组样本不足）**，CPS 整体 **Pending（运行期证据不足，不判 GO）**。
> 依据：CPS 规格 §14.1 条件 5 未满足——「单域排他 PoC 在战斗中段证明 ExternalUnknown 覆盖数为 0，或路线 A 证明原版行为能忠实执行」。
> 功能冻结令按规格 §14.2 继续生效（NO-GO 时保持所有新控制功能默认关闭、保留可复用观测与纯逻辑层、继续冻结新玩法功能）。

## 1. 动工与收工 HEAD

- 动工：97bcff7（2026-09-06 交接基线）
- 收工：585b1d3（cps3-observer 参数名修复，1074 PASS/0 FAIL，本报告时点）

## 2. 四组采样数据（用户实测，普通野战 200 vs 200 双方步兵，势均力敌）

### A 组 Native baseline（19:00 场，观测挂载后首场）
- CB 三域命令全关：OrderTraceAgg FormationTactics=0/FacingCorrection=0/Morale=0（全部 0 落地）
- ExternalUnknownTotal ≈ **2000**（原版 AI 基线命令流，观测链路首次真实工作）
- 证据：18:56:34.602 「观测已挂载（Harmony patch OK, prefixes=3）」→ ExternalUnknownTotal 持续增长

### B 组 Shadow only（19:38-19:43 场，324s）
- `[Cps3Shadow] WouldApply` **310 条**（决策运行不落地）：FormationTactics=271 / FacingCorrection=36 / Morale=2 / Brace=1
- OrderTraceAgg：FormationTactics=221/o0、FacingCorrection=32/**o3**（CB Facing 决策被外部覆盖 3 次）、Morale=2/o2
- ExternalUnknownTotal = **11242**（原版命令大量存在）
- 结论：CB 决策流与原版命令流分离可量化；o3 证明原版覆盖 CB 的机制在 B 模式可观测

### C 组 Arbiter coexist（19:46-19:52 场，351s）
- `[Cps3Shadow]` = **0 条**（正确非 B 模式）
- OrderTraceAgg：FormationTactics=1/o0、FacingCorrection=35/o0、Morale=1/o0（**注意：此 o 值为 CB 内部 Record 链的覆盖标注，与外部 setter 观测无关，不能证明原版覆盖与否**——2026-09-07 外部验证纠正）
- ExternalUnknownTotal = **3436**（原版命令流存在）
- 修正结论：C 组只能说明「唯一仲裁共存时 CB 决策流正常、原版命令流持续」；**不能**作为「统一仲裁解决 CB 与原版互刷」的直接证据（原版是否覆盖 CB 需 setter 级顺序判定，见 CPS-3R 证据链修复）

### D 组 Exclusive PoC（20:53-21:12 场，1148s）
- **未触发租约 Acquire**：ControlLease summary 全程 active=0（31 条），无 Acquire 边沿
- OrderTraceAgg：FormationControlLease=0/o0、FormationTactics=1/o0、FacingCorrection=10/o0
- Cps2Observation Acquire=0 全程
- 原因（结构性，同 ATC-5R R1-R8）：租约 Acquire 需要「AI 编队 Denied + 远离 MainBody>30m」；势均力敌局 AI 编队贴主力（Reserve 安全带内→NoSafeFallback）或被玩家队豁免（Team 0），**战斗中段无远出 Denied 编队样本**
- 注意：租约 PoC 开关确认已开（summary 持续输出=FormationControlLeasePocEnabled=true），但无触发点
- **指标设计缺陷（2026-09-07 外部验证纠正）**：即使租约 Acquire 成功，`ExternalUnknownTotal` 聚合全部编队×三域×整场，全局总数不可能归零；正确指标 = 租约期间 + 指定 Formation identity + 指定 Movement 域 + ExternalUnknown 次数

## 3. 观测链路修复记录（本阶段关键工程）

ExternalUnknown 恒 0 → 根因链（每步有用户实测日志实锤）：
1. t30：Enable() 仅启动一次性读开关，游戏内 MCM 开不生效 → Tick 首部每帧 ShouldEnable/ShouldDisable 热切
2. t34：加挂载成功/异常日志与 GetPatchInfo 核验 → 实测「观测挂载异常：仅 0/3 target 有 prefix」
3. t36：PatchAll 程序集扫描静默失败 → 改显式 `new Harmony().Patch(...)` 逐个打三 setter + HarmonyLib 版本/Location 诊断
4. t38：实测「Parameter "order" not found in SetMovementOrder(MovementOrder input)」→ MovementPrefix 参数名 order→input（Harmony 显式 Patch 按参数名绑定）+ 防复发映射表

终态：18:56:34.602「观测已挂载（Harmony patch OK, prefixes=3）」→ 三域 ExternalUnknown 数据真实可观测。

## 4. 运行期结论（分域回答，规格 §7.4）

1. **原版/外部写入覆盖了哪些域**：C 组 FC=35/o0（仲裁共存下 0 覆盖）、B 组 FC=32/o3（Shadow 决策被覆盖 3 次）——Facing 是主要受覆盖域；Arrangement/Movement 覆盖样本需更多场次
2. **CB 内部冲突**：本阶段未观测到 CB 内部互刷新样本（C 组 FC o0）；历史 o11 的改善待更多场次确认
3. **统一仲裁后改善**：C 组 FC o0 vs 历史 o11 —— 迟滞去重+唯一仲裁生效的直接证据
4. **排他控制改善**：**未取得排他控制样本**（D 组无 Acquire）——条件 5 不满足，无法证明
5. **仍存在的不良表现**：D 组场景下租约无触发点（结构性），非控制权问题；AnomalyHighGroundAbandon=38（19:30 场）等战术层问题不属于控制权范畴
6. **战术逻辑保留/调整/停用**：本阶段不裁断（功能冻结中），留 CPS-4 或后续

## 5. GO/NO-GO 判定

**CPS 整体：Pending（运行期证据不足，不判 GO）**

- §14.1 已满足：条件 1（真实来源观测 ✓ 前缀=3 三域工作）、条件 2（唯一胜者 ✓ 离线）、条件 3（优先级显式 ✓）、条件 4（除执行器外零直接 SetOrder ✓ 静态收口）
- §14.1 未满足：**条件 5（单域排他战斗中段 ExternalUnknown 覆盖=0 或路线 A 忠实执行）——D 组无 Acquire 样本**
- 其余条件（6 玩家接管/7 Release 恢复/8 回归/9 生命周期/10 不支持任务/11 无振荡/12 性能/13 A/B 区分/14 ADR）多数有离线或部分运行期证据，但条件 5 为硬门，不满足即不 GO

**下一步（D 组样本途径）**：
- 需要「战斗中段 AI 编队 Denied+远离主力>30m」样本：观战局（玩家不参战、无豁免）为最干净途径，但 v1.4.8 自定义战斗无旁观选项（用户已确认）；或构造 AI 强侧远出场景
- 或转向路线 A 运行期验证（Behavior 权重表达）作为条件 5 的替代满足路径

## 6. 已知限制

- 本阶段四组样本均由用户实测、日志核对，无主观观感替代
- D 组无 Acquire 不代表租约失败，是样本缺失（同 ATC-5R 结构性缺样本）
- 观测链路修复链（t30/t34/t36/t38）本身是重要工程资产，离线 1074 PASS 全绿

---

# 附录 A：路线 A 三场实测（2026-09-08，CPS-3R 阶段 4-5）

> 条件 5 替代路径证据（外部验证 2026-09-08 判定：正式转向路线 A；运行期验证不用 ForceLease）。模式 E_RouteA（Cps3Mode=4，RouteA 开+租约关，单编队 Movement 域）。

## A.1 三场数据（用户实测，普通野战 200 vs 200 双方步兵，`[RouteA]` 5s 汇总末值）

| 场次 | 时长 | selected/applied | stable | extRep | same/cross | intentChange | weightFlap |
|---|---|---|---|---|---|---|---|
| 场 1（09:31-09:40） | 536s | 956/956 | 956 | **0** | 0/0 | 1 | 0 |
| 场 2（09:48-09:52） | 239s | 469/469 | 469 | **0** | 0/0 | 7 | 1 |
| 场 3（09:53-09:56） | 224s | 785/785 | 783 | **2** | 1/1 | 7 | 1 |

## A.2 判定

- **三场均达成**：单编队隔离（formations=1）、Applied 全落地（applied==selected）、绝大多 stable
- **场 1 完美**：0 覆盖、0 flap、意图 1 次变化
- **场 2/3 非零事件全部落在边界（可解释）**：
  - `weightFlap=1` 发生在**开局意图切换瞬间**（场 3 09:53:04，chg 1→7 集中，Behavior 竞争窗口回摆，迟滞防抖后残留）
  - `extRep=2` 发生在**尾声**（场 3 09:55:29，距落盘 35s，AI 队 str→0 将灭，士兵散后 Behavior 停写、原版接管）
  - **无战斗中段外部覆盖**
- **结论（供 CPS-4）**：路线 A 忠实执行方向成立——三场战斗中段 externalReplaced=0，边界事件可解释。按锁定规则：路线 A 独立通过 → 条件 5 满足（替代路径）→ D 组租约战斗中段样本不再是 GO 必要条件。

## A.3 边界事件说明

- 开局 flap：意图切换时 Behavior 权重竞争窗口内回摆（迟滞防抖 2s 窗口设计目标，少数残留）；可选后续收紧迟滞窗口
- 尾声 extRep：AI 队将灭/散后该编队 Behavior 不再执行（无兵），原版/其它路径接管——属战斗结束自然行为，非中段覆盖
- 叙事阵营名缺陷（帝国 vs 帝国显示瓦兰迪亚）已独立修复（t58/t59，CultureId 去 GeneralAgent 兜底）
