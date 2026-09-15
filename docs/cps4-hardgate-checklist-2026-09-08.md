# CPS-4 硬门核验表（§14.1 十四条，2026-09-08 初稿）

> 目的：把规格 §14.1 十四条 GO 硬门与现有证据逐条对照，暴露缺口 → 决定补测清单 → 支撑 ADR/GO-NO-GO。
> 证据分级：✅满足（实测/离线断言充分）/ ⚠️部分（有证据但运行期专项缺失或强度不足）/ ❌缺（无证据）/ ⏳待办（文档类）。
> 当前 HEAD=`5868a02`，OfflineTests 1174 PASS/0 FAIL，CPS 状态=Pending（不判 GO），功能冻结令生效。

## 核验总表

| # | 硬门（§14.1） | 现有证据 | 状态 |
|---|---|---|---|
| 1 | 三 SetOrder 域真实来源观测，不再把 Unknown 写成 Native | t3/t40：Harmony 只读 Prefix 三 setter + source scope（CB/ExternalUnknown）+ owner 核验（t40 HasOwnPatch）；OriginalUnknown 语义修正（t3：FacingCorrection/FormationTactics 改真实来源）；运行期实测三域 ExternalUnknown 计数非零（A/B/C/D/E 各组 2000~11000+） | ✅ |
| 2 | 每 Formation/Domain/逻辑拍唯一最终胜者 | t5/t6：FormationCommandArbiter 每域唯一胜者 + 稳定 tie-break（优先级→矩阵→Source→Kind→ProposalId）+ §10.2 14 项离线测试（含 producer 顺序无关断言） | ✅ 离线充分 |
| 3 | 不依赖 Glue 调用顺序表达优先级 | t5/t6：P100-P0 优先级契约集中定义测试锁定；t9：防御迁移 Retreat>Brace>Protect 矩阵显式化；t13：P50>P40 顺序无关断言 | ✅ 离线充分 |
| 4 | 除唯一执行器/批准适配器外无编队级直接 SetOrder | t15/t18 静态扫描收口；t19/t21 租约整合后**全 Ai 目录仅执行器 ApplySetOrder 3 处**（LeaseGlue 清零）；静态扫描复核每次迁移后执行 | ✅ |
| 5 | 单域排他战斗中段 ExternalUnknown=0 **或** 路线 A 忠实执行 | 路线 B：D 组尾声 Acquire 后 ExternalReplaced 冻结 0 新增（辅助证据，样本形态不足）；**路线 A（替代路径）：三场 E_RouteA 战斗中段 externalReplaced=0**（场1 完美 956 applied 0 覆盖；场2/3 边界事件可解释——开局意图切换 flap/尾声 AI 队将灭 extRep，无中段覆盖） | ⚠️ 方向成立，正式验收待 P3 统计或外部确认 |
| 6 | 玩家接管 1 逻辑拍内生效，CB 不抢回 | t7/t8：执行器玩家接管无迟滞停止（离线测试 F）；t50：路线 A ShouldRestoreRouteA 玩家接管恢复（离线）；t1/t2：租约玩家豁免三条件（离线） | ⚠️ 离线有，运行期专项未单测 |
| 7 | Release 后原版 AI 两引擎评估周期内恢复，不残留租约 | t19/t21：租约 Release 三条件 + watchdog TTL/MissionEnding/identity/开关关闭 fail-open（离线）；t50：路线 A 热关闭恢复原权重（离线）；D 组尾声 Acquire 后战斗结束（运行期 Release 恢复未专项观测） | ⚠️ 离线充分，运行期专项未单测 |
| 8 | Retreat/Protect/Brace/合法 Flank/Intercept 无仲裁回归 | §10.5 离线全覆盖（Retreat 不被覆盖/Limited 不锁死/Flank 不召回，Program.cs:2882/3572/3585/4213/3037）；C 组运行期 FC=35/o0（仲裁共存无覆盖）佐证 | ✅ |
| 9 | Formation 清空/增援/Index 复用/Mission 结束无状态泄漏 | t11/t12：Mission 结束 ReleaseAll 幂等 + FormationId 复用不继承（P4 identity 引用）+ ClearMissionStaticState（离线）；t40：setter 事件流 identity 对象哈希防 Index 复用 | ⚠️ 离线充分，运行期专项未单测 |
| 10 | 不支持任务全部 fail-open | t24/t25：IsFieldBattle 门控（攻城/守城/突围 fail-open）+ 组合矩阵（离线）；租约/路线 A 均限普通野战 | ⚠️ 离线有，运行期攻城专项未实测 |
| 11 | 无每帧高频对刷，无 2s 无状态变化的相反命令振荡 | t7/t8：执行器同语义去重 + 5m/0.2rad 迟滞（离线）；路线 A 三场 weightFlap=0/1/1（边界可解释）、intentChange=1/7/7；[Bench] 各段 0.000~0.002ms（无高频迹象） | ⚠️ 离线+三场实测，正式验收待定（flap 边界可否接受） |
| 12 | 500 人 avgAi<5ms，新增控制面开销可解释 | 历史 500 人 avgAi=0.07ms（2026-09-06 集成轮，余量 70 倍）；新控制面实测 [Bench] coordination=0.001ms/executor=0.000ms/lease=0.001ms/frame（E_RouteA 场，200 人）；Arbiter O(Formation×Proposal) 低频 | ⚠️ 新控制面 200 人实测有，500 人复测未做 |
| 13 | A/B 报告能区分控制权问题与战术逻辑问题 | A/B/C/E 四模式数据齐（ExternalUnknown/覆盖/稳定性/事件链）；报告附录 A 已区分路线 A 忠实执行 vs 路线 B 租约限制 | ✅ |
| 14 | ADR 明确选定下一阶段执行架构及首个扩展域 | t20 研究报告（路线 A/B 九维度对比输入）；外部验证建议混合架构（A 默认主路径+执行器仲裁+B 窄域保险丝） | ⏳ ADR 文档未写 |

## 证据缺口汇总（决定补测清单）

### 必须补（影响 GO 判定的运行期专项）
- **条件 5 正式验收**：P3 统计（5 场固定场景 A/B/C + 每百次 Applied 同域替换率）或外部确认三场已够
- **条件 6/7 运行期专项**：战斗中玩家接管（F1/F5 手动下令）→ 验证 CB 1 拍内停止且不抢回；RouteA 热关闭 → 验证原版恢复
- **条件 12 500 人复测**：新控制面 500 人 avgAi 各段（Arbiter/Executor/Lease/观测）

### 建议补（低成本高价值）
- **条件 10 攻城 fail-open 运行期**：进一场攻城确认租约/RouteA 不激活、行为与基线一致（规格 R7）
- **条件 9 运行期生命周期**：一场增援/编队清空局观察无状态泄漏（[ControlLease] 无残留）

### 可接受现有证据（离线充分 + 运行期佐证）
- 条件 1/2/3/4/8/13：离线断言全绿 + 运行期计数/实测佐证

### 文档待办
- 条件 14：ADR（路线 A 默认主路径 + 执行器仲裁 + 路线 B 窄域保险丝；首个扩展域建议）→ 最后写

## 补测顺序建议（外部验证 P3 顺序）
1. 条件 5：5 场固定场景 A/B/C + RouteA 每百次 Applied 替换率（需用户进游戏）
2. 条件 6/7：战斗中玩家接管 + 热关闭恢复（需用户进游戏）
3. 条件 12：500 人 Bench（需用户进游戏）
4. 条件 10：攻城 fail-open（需用户进游戏）
5. 条件 9：增援/清空局（需用户进游戏）
6. 条件 14：ADR → GO/NO-GO 报告

## 判定规则（已锁定，外部验证 2026-09-08）
- 路线 A 独立通过 → 条件 5 满足 → D 组租约样本不再是 GO 必要条件
- 路线 A 未通过且路线 B 证据仍缺 → 最终裁决 NO-GO（不无限期 Pending）
- 条件 5 通过仍须满足其余硬门才判 GO

---

## 2026-09-14 更新：路线 A「本场退役」口径（J-2，用户裁决接受）

> 上文核验总表与缺口汇总为 2026-09-08 快照（当时 HEAD=`5868a02`、1174 PASS），保留不改；本节为增量口径，判定时以本节 + `docs/knowledge/CURRENT_STATE.md` 为准。

- **背景**：CB-REPAIR-0004 r3（`395fe13`）把超时后的重新武装收窄为「具名/离散/有界」——须同时满足①真实战术意图变化②`ReArmCooldownSeconds=10f`③本暂停期授予次数 `< MaxSuspendedReattempts=2`；预算重置只发生在「持所有权时真实选中／非超时真实释放／`BeginSession`」。
- **口径**：预算（最多 2 次重取）耗尽后**本场战斗内不自愈** → 语义即「**一场内出现一次坏窗口 = 本场 Route A 退役**」。CHURN 最坏压力（意图每拍抖动 + 连续态势变化）实测 acquires 封顶 **3**、Retry 封顶 **2**，此后持续让位、执行器每拍可写。
- **口径（Astra 2026-09-15 复核补充）**：本节保留「本场退役」语义（用户 2026-09-14 裁决、Astra 判实现忠实、无更高规范硬冲突），并把「**坏窗口**」明确为「**本暂停期两次重取耗尽且无真实选中**」——**不是**"首次超时即退役"。
- **裁决**：用户 2026-09-14 **接受**该语义并要求写进 CPS-4（本节即落点）；同时保留在交 Astra 的问题清单中供外部复核（`docs/cps3r-routea-astra-rereview-brief-2026-09-14.md` §5 问题 4），**该问已于 2026-09-15 裁定（见该件 §0e 第 4 项）**。
- **判定影响**：
  1. 条件 5/6/7 与 GO 判定必须按此语义书写：Route A 在某场失效后**不得再被期待自愈**；
  2. 采样判定**不得把"该场后续无 Route A 活动"当作新缺陷**（属既定退役语义）；
  3. 若未来要求自愈，须另立卡设计具名/离散/有界的自愈触发（当前不做，防再退化为周期占位）。
- **同期状态（判定时须一并核对；2026-09-15 回填更正）**：r3 `395fe13` 已于 2026-09-14 部署，并于 2026-09-15 完成诊断衍生构建 `a24e921` 采样后**回滚回 r3** —— **现网 DLL = r3 `7E5FC25A…`（228,352 B，2026-09-15 核对 dst SHA = 备份 SHA；`Get-Process Bannerlord` 为空），r1 `723b457`／r2 `bc8449a` 仍未部署**；离线 r3 = **1297 PASS / 0 FAIL**（诊断构建 `a24e921` = 1327 PASS / 0 FAIL、`RouteAStaticChecks` 20 项、生产 setter 4=4）；质检 t12 以真 r2/r3 生产构建双向独立复现（r2 复现 Astra P1：duty 98.75%~99.88%；r3 判否：acquires=1、40/100/150s 零重取、执行器 75%~93.3% 连续窗口）；台账 CB-REPAIR-0004 **已于 2026-09-15 关闭（`COMPLETED`，commit `238e31a5…`，证据 4 条 PASS，`repair-verify=ALL CONSISTENT`；Astra 裁定 7 认定具备关闭条件、用户授权关闭）**；**条件 5 经 Astra 2026-09-15 评审判 PASS，但 CPS-4 保持 Pending、GO 不通过**（该 PASS 不推出 CPS-4/GO）。
- **保留项**：F-5（Release 位于 Harmony postfix 栈内改权重，需运行期证）、H-1（E 模式可观测性）、H-2（RouteA+观测复合帧成本）；**F-3/F-4 已随 r2/r3 闭合**；J-3（证据表满时计数不一致）、J-4（超时暂停依赖先清后设次序）低危未修。

---

## 2026-09-15 更新：归因口径 A（Route A 写入不进 provenance 聚合）与 CPS-4 重建基线前置

> 与上文核验总表、`2026-09-14 更新` 节并列的增量口径；判定时以本节 + `docs/knowledge/CURRENT_STATE.md` 为准。

- **结构性事实（Descriptive，4 场复现）**：`RouteAOrderAdapter` 只 `CommandSourceScope.Enter` + `SetMovementOrder`，**不**调用 `CommandProvenance.Instance.Record`（全仓唯一调用点 = `FormationCommandExecutor.cs:147`）⇒ Route A 的落地写入**不进** 5s `[OrderTrace]` 与终局 `[OrderTraceAgg]`（`CombatBrainRouteA` 恒为 `0/o0`）；Route A 的归因只存在于 **`[RouteAEvent] source=CombatBrainRouteA`** 渠道（自洽：36 条 = 12 次落地 × 3 个事件）。
- **Astra 2026-09-15 裁定（归因口径收敛方向）**：采用 **方案 A —— 只改文档口径、不改码**（冻结令生效中，不在 Route A 路径新增 `Record` 接线、不新增 `SetOrder` 调用点、不需 Waiver）。
- **判定口径（本节即落点）**：
  1. **Route A 的落地/归因判定只认 `[RouteAEvent]`** 的 `OrderApplied` / `SetterBegin` / `SetterCompleted` 三方**自洽**，**不得**用 `[OrderTraceAgg]` 的 `CombatBrainRouteA` 计 Route A（该值恒 0，会漏记）；
  2. `[OrderTraceAgg]` / `[OrderTrace]` **只**用于验普通执行器（`FormationCommandExecutor`）与**释放后窗口**（`ExternalAfterExit`）；
  3. `ExternalUnknown=0/o0` 属正确设计，不是缺口。
- **CPS-4 重建基线前置**：重建归因基线时**必须显式写明「Route A 不进渠道①（provenance 聚合）」**，否则会把 CombatBrain 自己的 Route A 写入记为 0（即成"非 CB"），与 `CONSTITUTION.md` §7.1 的归因纪律方向相反。同理，CPS-4 条件 13（区分控制权问题与战术逻辑问题）不得把 `[OrderTraceAgg]` 当 Route A 的控制权指标。
- **P2（不阻断，Astra 2026-09-15 列项）**：重试事件 revision 配对；t12 源码不可重建；`NavigationFailed` 失败路径无样本；J-3/J-4 低危未修；引擎侧「为何停止选中」的原生权重候选解释仍为 Inferred（未取证）。
- **未决裁决项（GO 前必裁，产品口径）**：Route A 的玩家队排除**不受** MCM 开关 `PlayerTeamExemptionEnabled` 约束，与其它 7 个模块口径不一致。
