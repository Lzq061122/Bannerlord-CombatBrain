# CombatBrain Evidence — Router / Index

> status: archived
> authority: non-authoritative (historical record)
> superseded-by: E:\Agent\6a9268112aeaf49072bef052  (current 1.x canonical authority)
> 依据：CombatBrain Knowledge Architecture — Phase C4B（Evidence & ADR Canonicalization）。
> 本文件是 **evidence router/index**：让 Agent 在**不加载全部历史**的前提下定位证据。不是巨型 evidence 文档。
> 现有权威报告/交接/按域记录以**指针登记**为主，不复制正文。

---

## 1. Role Contract

- **Authority axis**：DESCRIPTIVE（描述层）。
- **职责**：说明「什么算 evidence、evidence 放哪里、如何解释」并登记**已闭合**的非 CPS 证据组。
- **核心语义**：
  - Evidence 是 **scoped claims**（何时/何对象/何范围），不是永恒真相；
  - **Evidence 不自动改写 Normative**（Descriptive ≠ Normative；见 INDEX §3 冲突协议）；
  - 离线绿 ≠ 运行期绿；**历史 PASS ≠ 当前 HEAD 已验证** —— 现行验证态唯一权威 = `docs/knowledge/CURRENT_STATE.md`。
- **必须不含**：动态快照；evidence 正文堆积（只登记 + 指针）。
- **维护 owner**：Captain / Integration Owner。

## 2. Evidence Lifecycle 字段（每组记录）

| 字段 | 含义 |
|---|---|
| checkpoint | 该证据对应的时间/commit/断言快照（历史值，不回刷） |
| current relevance | 对当前任务仍相关的程度（现役约束 / 仅背景） |
| superseded | 是否被更新证据取代（若被取代，注明被谁） |
| authoritative source | 最权威出处（指针） |
| relation to current state | 与 CURRENT_STATE 的关系（≠ 现行验证态） |

## 3. First-wave Evidence Registration（C4B；non-CPS，已闭合）

| Evidence Group | Authoritative Source（指针） | Checkpoint | 语义 / 说明 |
|---|---|---|---|
| F1–F12 完成 | Master Spec §4.1（逐项 commit/断言）；legacy CONTEXT §3（进度表）；代码 + OfflineTests | 各 F 模块接入 commit（v1.x 史） | 历史里程碑；F1 曾停用、X6 重启用——以 CURRENT_STATE 为现行态 |
| S1–S4 流水线交付 | `MBmod/CombatAI/tools/HANDOFF_S1-S4.md`（交接主档）；Master Spec §4.2 | 2026-09-01 交接 | S1–S4 已实现；B0 相关复核见 BACKLOG BL-001 |
| P0 六项评审整改关闭 | `MBmod/CombatAI/tools/review-findings-2026-09-01.md`（清单源）；legacy CONTEXT §3.8；Master Spec §4.6 P0 | 11f66c7..9d818ec（2026-09-01） | 六项全修复（P0 修复表为历史） |
| X0–X7 完成 | Master Spec §4.4（逐项 commit/断言基）；legacy CONTEXT §4 X 记录；X 系列派单为历史 prompt | 逐项 commit（09-04..09-05） | 八项全落地；X 开关语义 canonical 见 CONSTITUTION#anti-cheat-gate |
| N1–N3 完成 | Master Spec §4.4b（逐项 commit/断言基） | 逐项 commit（09-05） | 全链实现；运行期验证债见 BACKLOG BL-003 |
| ATC-0..4 闭合 | legacy CONTEXT §4 ATC 记录（222f580..e2a4226，814 PASS）；派单/复审规格 doc；代码 `CombatBrain/Coordination/` | e2a4226（2026-09-05） | 仅离线验证（记录已标注）；ATC-5R→CPS 路线史属 CPS，见 §5 |
| TerrainStub→TerrainSampler | 代码注释（`CombatBrain/Ai/CombatAiMissionLogic.cs:237`、`Perception/TerrainSampler.cs`、`Perception/VisibilityOracle.cs`）；review-findings #7「假数据比没数据更糟」 | 2026-09（删除 commit 前后） | RESOLVED；决策背景见 ADR-0003 |
| ATC low findings 收口 | legacy CONTEXT §4 L189/L200 对照；代码 BattlePhaseFSM.cs:37–43,97–103 | b362559 / f021fb0 | stalemate-dead & advance-to-engage 均 RESOLVED（代码实锤）；见 BACKLOG §4 |
| harness 缺陷（指针） | `MBmod/CombatAI/tools/harness-defect-report-2026-09-05.md` | t7/t9/t11（09-05） | harness 域；产品 backlog 不滞留 |

## 4. 解释规则（沿用）

1. 引 evidence 必须带 checkpoint 上下文；不得把过时 checkpoint 当现行事实。
2. 证据冲突时只用于回答 Descriptive「实际怎样」；与 Normative 冲突 → 报告 mismatch，不改规范。
3. 找现行实现/验证态 → CURRENT_STATE；找「为何/当初」→ ADR（见 `docs/adr/README.md`）；找明细证据 → 本表指针。

## 5. CPS Evidence（冻结，仅登记指针）

以下**不迁移、不归档、不改写**；已由 canonical 路由（INDEX R2）引用，仅列指针供定位：

- `docs/cps3-runtime-ab-report-2026-09-07.md`（CPS-3 运行期 A/B）
- `docs/cps3r-routea-final-blocker-report.md`（Route A 收口）
- `docs/cps3r-routea-astra-review-submission-2026-09-08.md`（Astra 复审提交物）
- `docs/cps4-hardgate-checklist-2026-09-08.md`（硬门清单）
- legacy CONTEXT §4 CPS 卡史 / CPS-3R 综述（CPS GO/ADR 闭环后迁入本域）
- CPS 相关 ADR（route 选择/GO 链）未到准入时机，不创建（见 adr/README §5）。

## 6. C4B+ 占位

- CPS 历史段：等 review / GO / ADR 闭环后按系列登记（INDEX §11 迁移目标）。
- 运行期证据（[Bench]、A/B 场次）在新验证完成后按组登记，不进 CURRENT_STATE 正文。
