# CombatBrain ADR — Architecture Decision Records

> status: active
> authority: canonical-bootstrap
> 依据：CombatBrain Knowledge Architecture — Phase C4B（Evidence & ADR Canonicalization）。
> 职责：登记**持久决策**的 rationale/alternatives/consequences。ADR 解释「为何」，CONSTITUTION 陈述「现行规则」；
> 若未来 ADR 改变规范，**必须显式更新 CONSTITUTION**，ADR 不静默覆盖。

---

## 1. Role Contract

- **Authority axis**：NORMATIVE（决策存档域；规则本体仍在 CONSTITUTION）。
- **ADR 是**：持久、跨切面/架构级、未来维护者很可能追问「为什么」、且需要 alternatives 与 rationale 的决策。
- **ADR 不是**：任务日志 / bug 修复报告 / commit 日记 / 测试记录 / CURRENT_STATE 替代。
- **维护 owner**：Captain / Integration Owner；ADR 采纳需 rationale（架构级变更）。

## 2. Admission Criteria（每档必答）

1. 持久性（影响多迭代/多维护者的理解或选择）？
2. 有取舍（被拒替代方案未来可能被重提）？
3. 代码不可自明（读源码无法还原「为何」）？
4. 非纯施工（不是实现卡/bug 修复/断言调整）？
不满足 → 属于 evidence 或 backlog，不写 ADR。

## 3. Status

`proposed → accepted（或 rejected）→ superseded（注明被谁取代）`。

## 4. Format（每档字段）

Status / Date / Deciders / Context / Decision / Alternatives Considered / Rationale / Consequences /
Related Canonical Rules（`CONSTITUTION#...`）/ Related Evidence / Supersedes / Superseded by。

约束：**不嵌入动态项目状态**（当前 HEAD、当前断言数）；历史 commit 仅作 evidence 引用，不作现行状态。

## 5. Register

| ADR | 主题 | Status | 关联 Canonical | 关联 Evidence |
|---|---|---|---|---|
| ADR-0001 | 三层两横切分层架构 | accepted | CONSTITUTION#architecture-principles / #design-principles(P2) | MS §2/§4.1；CONTEXT §2 |
| ADR-0002 | 反作弊闸语义模型 | accepted | CONSTITUTION#anti-cheat-gate / #hard-invariants(IL-1) | X0/X7 记录；CONTEXT §4 澄清 |
| ADR-0003 | TerrainStub→TerrainSampler | accepted（已实施） | CONSTITUTION P1（信息公平） | 代码注释；review-findings #7 |
| ADR-0004 | 命令所有权与归因纪律 | accepted | CONSTITUTION#command-ownership；INDEX §4 | CommandProvenance.cs；CPS 报告（指针） |
| ADR-0005 | CPS 下一阶段执行架构与首个扩展域（Facing） | **proposed**（*启用以 CPS-4 GO 为前置*） | CONSTITUTION §7 命令所有权/写入纪律；INDEX §4 | `cps4-hardgate-checklist-2026-09-08.md`（条件 14）；`cps3r-routea-astra-rereview-brief-2026-09-14.md` §0e；CPS-4 推进评估报告（2026-09-15） |

## 6. Deferred Candidates（未到准入时机，不创建）

| 候选 | 原因 |
|---|---|
| CPS Route A vs Route B / 条件 5 替代路径 | CPS review/GO 链仍开放（C4B 明确禁止此时成档）——**2026-09-15 说明：其"架构选型"部分已由 ADR-0005 承接（proposed，启用以 CPS-4 GO 为前置）；本候选仅剩「条件 5 替代路径的具体证据取舍」待 GO 闭环后处理** |
| 租约/ForceLease 结构性限制结论（ATC-5R → CPS 转向） | 属 CPS 历史，等闭环后随 CPS 证据一并处理 |
| MCM `Dropdown<string>` 离线加载限制 / Harmony 参数名绑定等工具教训 | 多为 lessons 而非跨切面决策——符合准入判据者后续转 ADR，否则入 lessons-log |
| 编号体系（F/S/H/P/T/X/B/t/G/CPS）唯一化 | 等 B 系列撞名人工裁决后以 NOMENCLATURE 登记，无需 ADR |
