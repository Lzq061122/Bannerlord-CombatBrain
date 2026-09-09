# CombatBrain Evidence — Router / Index

> status: active
> authority: canonical-bootstrap (Phase C2 建立)
> 依据：CombatBrain Knowledge Architecture — Phase C2。
> 本文件是 **evidence router/index**，不是巨型 evidence 文档。

---

## 1. Role Contract

- **Authority axis**：DESCRIPTIVE（描述层）。
- **职责**：说明「什么算 evidence、evidence 放哪里、如何解释」；提供路由而非全文。
- **核心语义**：
  - Evidence 是 **scoped claims**，不是永恒真相；
  - **Evidence 不自动改写 Normative**（代码违反规范 = implementation/spec mismatch，不是规范自动变了；见 INDEX §3 冲突协议）；
  - 离线绿 ≠ 运行期绿（OfflineTests 与运行期语义不同权，历史有 provenance/生命周期语义失败先例）。
- **必须不含**：动态快照（HEAD/断言数/active task）；evidence 全文堆积。
- **维护 owner**：Captain / Integration Owner。

## 2. Evidence 分类与位置

| 类别 | 位置（现状） | 说明 |
|---|---|---|
| 离线断言证据 | repo `OfflineTests/` + 各实现提交 | 断言基线数字只作为历史 checkpoint 记录，不写进 CORE/SYSTEM |
| 运行期验证证据 | repo `docs/cps3*-*.md` 报告（如 cps3-runtime-ab-report / cps3r-routea-final-blocker-report / cps4-hardgate-checklist） | **C2 不搬移**；CPS GO/ADR 闭环后再按类别归档 |
| 缺陷/审查清单 | `tools/review-findings-2026-09-01.md` | 标题引用，不重编号 |
| 完结实现/任务记录 | legacy CONTEXT §3/§4 卡史（暂留原处） | C3 按系列迁入本域并指针化 |
| 交接/进度档案 | `tools/HANDOFF-*`、`tools/task-update-context-*` 等 | 默认不加载（NEVER_BY_DEFAULT） |

## 3. 解释规则

1. Evidence 按「何时、何对象、何范围」解读 —— 结论不越界泛化（如历史「原生占比 X%」口径已废弃，见 CURRENT_STATE Engineering Debt）。
2. 引 evidence 时必须带 checkpoint 上下文（commit/时间/范围），不得把过时 checkpoint 当现行事实。
3. 冲突时：证据只用于回答 Descriptive「实际怎样」；若与 Normative 冲突 → 报告 mismatch，不自动改规范。

## 4. C3 迁移占位

- CONTEXT §4 CPS 完结卡 / CPS-3R 阶段综述 → 本域（附报告文件指针，正文不再重复）；
- CONTEXT §3/§3.5–3.8、X/N/ATC 完结记录 → 本域分系列归档；
- 已有 repo `docs/cps3*.md` 报告保持为权威 evidence 文件，只登记索引不改内容。
