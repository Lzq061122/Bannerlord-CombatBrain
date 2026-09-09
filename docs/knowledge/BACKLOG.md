# CombatBrain BACKLOG — Currently Unresolved Work / Debt

> status: active
> authority: canonical-bootstrap (Phase C2 建立)
> 依据：CombatBrain Knowledge Architecture — Phase C2。
> 角色契约见下。C2 只建契约与状态词汇；**条目正文在 C3 由 legacy CONTEXT §4 / review-findings 迁入**。

---

## 1. Role Contract

- **Authority axis**：OPERATIONAL（待办/债务域）。
- **职责**：承载**当前仍未解决且仍可行动**的工作与债务。
- **必须不含**：已完结工作的长期陈列；历史任务执行记录（属 evidence/ 域）；HEAD / 断言数 / 当前阶段快照。
- **状态词汇（必须区分）**：

| 状态 | 含义 |
|---|---|
| OPEN | 当前可领取/可执行 |
| DEFERRED | 已知但暂缓（有明确暂缓理由） |
| BLOCKED | 被依赖项卡住（注明阻塞源） |
| NEEDS_VERIFICATION | 是否仍成立未核实（先核实再领取） |

- **维护 owner**：Captain / Integration Owner；普通成员可提建议与 evidence，不直接改写。
- **迁入规则（C3）**：条目迁入时从源删除「现行待办」语义并保留历史证据指针；已完成条目不得无限期滞留为 current backlog。

## 2. 开环工作源映射（条目正文仍在各自权威源，C2 不搬移正文）

| 权威源 | 承载的开环内容 | 状态语义 |
|---|---|---|
| `CURRENT_STATE.md`（Known Blockers / Debt / Frozen） | 当前验证态相关的阻塞与债务（如外部队列、功能冻结清单） | 以 CURRENT_STATE 为现行口径；BACKLOG 只在跨任务路由时引用 |
| `tools/review-findings-2026-09-01.md`（P1×4 / P2×6 等） | 缺陷/整改开环项（标题引用，不重编号） | NEEDS_VERIFICATION（自清单口径后可能有 CPS 相关新处理） |
| legacy CONTEXT §4（CPS 巨块 / t71 / t61 / 待确认项 / nits / 验证债） | 研究组任务与验证债务开环项 | CPS 相关条目 = **BLOCKED（CPS GO / ADR 前冻结）**；其余 NEEDS_VERIFICATION |
| 归因链路重建（Observed/Inferred/Unknown 严格口径） | 待 CPS-4 重建基线 | BLOCKED（等 Astra 复审 / CPS-4） |

## 3. 当前已知的 BLOCKED / 冻结面（引用级，非重复正文）

- **CPS 功能冻结令**：CPS GO 前禁新文化战术 / 新 Formation Mission / 新兵种角色 / 新播报 / 新主动移动 Glue / 新状态机 / 新直接 SetOrder 调用点（规范源：CONSTITUTION 待落地 + CURRENT_STATE）。
- **CPS 历史段迁移冻结**：C1 判定 CONTEXT §4 CPS 条目与 CPS-3R 综述为 high-risk legacy，等适用 review / GO / ADR 闭环后才可迁入 evidence/ 与 ADR（见 evidence/INDEX.md 与 adr/README.md）。

## 4. C3 迁入范围（预留，不执行）

- CONTEXT §4 开环项 → 本文件（区分 OPEN/DEFERRED/BLOCKED/NEEDS_VERIFICATION）；
- CONTEXT §5 已知占位中未决项、X/N/ATC 验证债 → 本文件；
- 已完结系列记录 → evidence/ 域，**不**进本文件。
