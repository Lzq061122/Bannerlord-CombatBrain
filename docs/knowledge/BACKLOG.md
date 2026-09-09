# CombatBrain BACKLOG — Currently Unresolved Work / Debt

> status: active
> authority: canonical-bootstrap (Phase C2 建立；C4A 首次入住)
> 依据：CombatBrain Knowledge Architecture — Phase C4A（Operational & System Canonicalization）。
> 角色契约见 §1；准入规则见 §2；条目正文 C4A 已按当前证据分类入住。

---

## 1. Role Contract

- **Authority axis**：OPERATIONAL（待办/债务域）。
- **职责**：承载**当前仍未解决且仍可行动**的工作与债务（仅条目级；不带执行日记）。
- **必须不含**：已完结工作陈列；历史任务执行记录（属 evidence/）；HEAD / 断言数 / 当前阶段快照（指向 CURRENT_STATE）。
- **维护 owner**：Captain / Integration Owner；普通成员可提建议与 evidence，不直接改写。

## 2. Admission Rule（C4A 起强制）

- 每条先以**当前证据/源码**分类，六态之一：`OPEN / DEFERRED / BLOCKED / NEEDS_VERIFICATION / RESOLVED / SUPERSEDED`。
- **只有可行动的未决项进入本文件**；RESOLVED / SUPERSEDED 不得作为 active backlog 滞留（在迁移表中记录，不入本表）。
- legacy 不确定项一律标 `NEEDS_VERIFICATION`，禁止猜 `OPEN`。
- 每条含：stable ID / 状态 / 一句话描述 / 源与证据指针 / 阻塞或触发条件（如适用）。

## 3. 条目

| ID | 状态 | 内容 | 源 / 证据指针 | 阻塞 / 触发 |
|---|---|---|---|---|
| BL-001 | BLOCKED | Luna-B0：架枪「木头人」回归复核（15 场实测，验证 t71 修复 c4c732e） | Master Spec §4.5 B0；CURRENT_STATE deploy 闸 | 部署/运行期闸：Astra re-review 裁决后按命令链部署 |
| BL-002 | NEEDS_VERIFICATION | t61 夹击触发质量门：needs_revision 状态与 +5 断言入库证据缺失，队列归档后存续性未核 | MS §4.6 旧队列遗留；CONTEXT §4 t61 | 先核实代码现状再定级 |
| BL-003 | BLOCKED | 运行期集成验证债统一清债：500 人 [Bench]（X5 全链三段 / X6 perception O(N) / X7 关迷雾降开销）+ N 系列 200 人 A/B、3 分钟 E1-E5、3.5s 窗意图切换 | MS §4.6 当前遗留；CONTEXT §4 X/N 债 | 需进游戏：同上部署/运行期闸 |
| BL-004 | BLOCKED | CPS 主线收口（CPS-3R Route A 复审 → CPS-4 硬门逐条核验）——仅登记高层现行态，不开新 CPS 实施工作 | CURRENT_STATE（WAITING_EXTERNAL）；INDEX R2 | 外部：Astra re-review；状态不重释 |
| BL-005 | BLOCKED | CommandProvenance.Enable() 运行期启动调用点接入（cps0-runtime-baseline 卡） | CURRENT_STATE Engineering Debt | CPS 域，随 CPS-4 / 复审安排 |
| BL-006 | DEFERRED | P1：队中心口径统一（Smoothed vs CachedMedian 等 4 套实现） | review-findings-2026-09-01.md（P1，标题引用）；CURRENT_STATE Frozen/Deferred | CPS GO / 等待窗口解除后处理 |
| BL-007 | DEFERRED | P1：多队场景敌队识别（两队假设全乱） | review-findings（P1） | 同上 |
| BL-008 | DEFERRED | P1：opponent_priors.json 未随 deploy 拷贝（已 fallback 常量表兜底，deploy 路径补拷） | CURRENT_STATE Frozen/Deferred | 随下一次部署窗口一并处理 |
| BL-009 | DEFERRED | P2 代码债批：OnAgentRemoved 漏清 _attackStats / TickBlock 每 0.1s new List+O(n²) / CombatLog 无缓冲 / BeliefIntentEngine 死参数 / OnAgentHit AttackDirection 时机可疑 | review-findings（P2，标题引用） | 同上 |
| BL-010 | NEEDS_VERIFICATION | review-findings「待确认 3 项」复核：①编队命令与原版 TeamAI 竞争（CPS 归因/单写者机制已实质覆盖，需一次游戏内日志确认后关闭；卡 T20-01）②routRatio 负值未 clamp ③BraceGlue _armed 不重估 | review-findings；CONTEXT §4；CPS 证据链（docs/cps3*-*.md） | ①需运行期日志样本（部署闸后） |
| BL-011 | DEFERRED | 知识库迁移债：63 处 CONTEXT 引用改写为语义锚 / `_kb` sync 修复 / team.json 历史瘦身 | CURRENT_STATE Engineering Debt；C1 迁移映射 | 属后续 C 阶段，非产品 backlog |
| BL-012 | DEFERRED | X5/X4 遗留低优：x4-lod-gite-nit（IsReleaseLod >=3 → ==L3）；x5-thrown-design-doc-gap（Thrown→Ranged 回填设计稿）；ATC-5R 租约 nits（hasactiveforteam-unused / 玩家豁免漏接 MCM 开关）随租约路线 B 恢复时处理 | MS §4.6；CONTEXT §4（X5 nits / ATC-5R nits） | 顺手收 / 租约路线恢复时 |
| BL-013 | NEEDS_VERIFICATION | atc1-advance-to-engage-unconditional：BattlePhaseFSM Advance→Engage 接触门控——疑似已被 ATC-4 P1-2（f021fb0，EnemyContact 迷雾门控）覆盖，代码现状待核 | CONTEXT §4 L200（low findings 列表为后续追加，可能过时） | 核实 BattlePhaseFSM.cs 后定级 |

## 4. 明确不入本表的 legacy 材料（迁移分类记录）

| Legacy 材料 | 分类 | 依据 |
|---|---|---|
| ATC low findings：atc1-stalemate-input-dead / atc3-armylog-decrement-per-team | RESOLVED | CONTEXT L189（b362559：Stalemate 注释收口=修订5 契约占位保留；_armyLogTimer 改 OnMissionTick 按 dt 递减） |
| ATC low：atc1-advance-to-engage-unconditional | NEEDS_VERIFICATION → 见 BL-013 | CONTEXT L200 可能过时（疑似 f021fb0 已覆盖） |
| TerrainStub 假地形待办 | RESOLVED | TerrainSampler.cs 替代（代码注释实锤） |
| P0 六项 / X0–X7 / N1–N3 / ATC 0..4 | RESOLVED（历史） | 完结记录在 evidence/历史，不滞留 backlog |
| 团队编制 / provider 迁移（tk9902） | SUPERSEDED | 权威在 team.json / harness 域 |
| harness 状态分裂缺陷 | SUPERSEDED（产品域） | 已有 `tools/harness-defect-report-2026-09-05.md`，属 harness 域 |
| 编队命令竞争「待确认①」 | NEEDS_VERIFICATION（未关） | 同上 BL-010 |
| CONTEXT §4 全部 CPS 卡史 / CPS-3R 综述 | 不入表（frozen） | CPS GO/ADR 闭环后迁 evidence/（INDEX §11） |
