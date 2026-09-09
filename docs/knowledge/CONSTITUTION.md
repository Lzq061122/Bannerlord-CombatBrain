# CombatBrain CONSTITUTION — Canonical Normative Core

> status: active
> authority: canonical-bootstrap (Phase C2 建立；Phase C 内容迁移完成后生效)
> 依据：CombatBrain Knowledge Architecture — Phase C2（Canonical Skeleton & Authority Contracts）。
> 角色契约见下。本文件由 Phase C2 建立为**壳**；正文规范文本的迁移/裁决在 C3。

---

## 1. Role Contract

- **Authority axis**：NORMATIVE（规范层）。
- **职责**：承载项目**稳定**原则、不变量、架构约束 —— 即「应该怎样运行」的最高常驻文档。
- **必须不含**：当前 HEAD / commit 数 / 断言总数 / active task / 当前 blocker / 当前阶段 / 团队构成 / 执行日记。
- **覆盖规则**：Operational（任务/指令）不得在无显式 valid waiver / architecture decision 时覆盖本文件。
- **冲突语义**：Descriptive（代码/证据/测试）不自动改写本文件；合法例外必须走 Waiver Contract（见 INDEX §4）。
- **维护 owner**：Captain / Integration Owner。内容级修订 = 架构级变更，需显式 rationale / review（与 INDEX §6 路由权限变更同级）。

## 2. Planned Content Domains（正文在 C3 落地）

下列稳定规则是 CONSTITUTION 的预定内容；**落地前**仍需以来源裁决/冲突消解为前提（见 §4），不在 C2 搬运正文：

1. 三层两横切架构原则与「拟人而非全知」边界；
2. 三条铁律（拟人/性能预算/剧场效应）的精确定义；
3. 反作弊闸（realism-gate）语义：层级边界、可关范围、永不开放层；
4. CPS 命令所有权 / 冻结令中**真正规范**的部分（归因纪律、冻结范围）；
5. 稳定性能原则（聚合优于遍历、事件驱动优先、运行期红线）。

## 3. 内容来源与裁决路径（C3）

| 预定内容 | 候选来源 | 关系 |
|---|---|---|
| 铁律/架构 | CONTEXT §2 核心设计（legacy，权威口吻但非 canonical） | 需与下两行消解后并入 |
| 产品宪法 | manager `CombatBrain_Master_Spec.md` §1.3「四大支柱」 | **措辞结构不同**（支柱 vs 铁律），须统一 |
| 不变量摘要 | repo `AGENTS.md` §2 Immutable Invariants | 摘要；其权威指向行需改指本文件 |
| 反作弊闸语义 | Master Spec §2.7（2026-09-04 裁决）vs CONTEXT L205 澄清 | 两处并存，正文落地前先消解 |
| 归因/冻结 | CONTEXT §4 CPS 条目 vs repo AGENTS §2.5/§2.6 | 仅抽取规范部分 |

## 4. 已观测冲突（C3 裁决清单，不在 C2 处理）

1. **不变量权威指向**：repo `AGENTS.md` §2 声明「完整权威见 Master Spec」；C2 决定 CONSTITUTION 为 canonical normative core → AGENTS 权威行需在内容落地时改指 CONSTITUTION。
2. **「铁律」vs「四大支柱」**：CONTEXT §2 三条铁律 vs Master Spec §1.3 四大支柱 —— 命名与切分不一致，C3 须裁决统一表述。
3. **反作弊闸边界**：Master Spec §2.7 与 CONTEXT L205 澄清（两层可关/第三层永不开放/禁「反作弊」字样）并存且措辞不同，C3 裁决。
4. **Master Spec 自身动态值**：Master Spec §0 含 HEAD/commit 数、§4 含实现状态快照 —— 其角色定位为 long-form 产品/设计规格后，动态快照部分应由 CURRENT_STATE 接管（C3 决策，不自动执行）。

## 5. 本文件与 Master Spec 的边界（C2 锁定）

```text
CONSTITUTION.md（repo docs/knowledge/）
  = canonical 稳定规范核心（最高常驻 NORMATIVE）

CombatBrain_Master_Spec.md（manager 侧）
  = long-form 产品/设计规格 + 历史设计源
    除非 C3 后显式重新分类，其产品规格正文地位保留，
    但其「唯一真相 / 不变量权威」表述停止作为 canonical 依据。
```

- 冲突不得静默共存：任何规范文本进入 CONSTITUTION 前，先在 §4 裁决；无法调和 → `STOP → record → escalate`（INDEX §3 冲突协议）。
