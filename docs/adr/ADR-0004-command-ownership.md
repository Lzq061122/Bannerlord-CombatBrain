# ADR-0004: 命令所有权与归因纪律（Command Ownership / Attribution）

- **Status**: accepted
- **Date**: 2026-09-10（Phase C4B 登记；纪律随 CPS-0 起建立，Astra 复审进行中）
- **Deciders**: 项目架构决策链（CPS 命令控制面工作确立）
- **Scope note**：本 ADR 只覆盖**已独立确立的稳定规范**（归因纪律/冻结/waiver）。
  不含 Route A vs Route B 选择、条件 5 替代路径、租约设计等 **CPS 进行中决策**——那些等 review/GO/ADR 闭环后另行成档。

## Context

原版 AI 与 CombatBrain 都会对编队下发移动/排列命令。若不知道「一条命令到底是谁写的、有没有被覆盖」，就无法验证 CB 是否生效、也无法回答「CB 是否与原版互刷/打架」。早期曾把 `ExternalUnknown` 直接归因为「原生 TeamAI 写」（OriginalUnknown=NativeTeamAI，并据此得出「原生占比 35–80%」），事后证明该推断无依据：Unknown 只是「观测不到写者」，不等于「原生写的」。

## Decision

**命令所有权 = 单写者归因纪律**（canonical 详见 CONSTITUTION#command-ownership）：

1. `CommandProvenance` 只记录 **CB 自己的调用点**；不得把非 CB 写入归因为 CB，反之亦然。
2. 运行期数据严格区分 **Observed / Inferred / Unknown**；Unknown ≠ Native。
3. 旧结论「原生占比 X%」「OriginalUnknown=NativeTeamAI」**废弃**，不得继续当实锤引用。
4. **功能冻结令（CPS GO 前）**：禁新文化战术 / 新 Formation Mission / 新兵种角色 / 新播报 / 新主动移动 Glue / 新状态机 / 新直接 SetOrder 调用点——收敛写者面，为归因可信度创造条件。
5. 任何对上述规范的例外必须走**显式 Waiver**（INDEX §4），禁止任务卡悄悄绕过。

## Alternatives Considered

1. **Unknown 归因为原生** —— 拒绝/废弃：无证据推断，污染归因结论（历史教训）。
2. **全面拦截原版 AI 写入（深度接管）** —— 拒绝：违反产品边界（不深度接管原生）与兼容（NFR N3），且崩溃风险高。
3. **CB 与原版各自写入、事后仲裁** —— 作为演化中的观察态曾产生大量 ExternalUnknown；后收敛为「谁写谁记录 + 仲裁器收敛」。
4. **只做影子记录不落地** —— 仅作过渡测量模式（Shadow），不作为终态：终态需要单写者归属 + 真实覆盖判定。

## Rationale

- 归因诚实（fail-fast）优先于「看起来有效的结论」：不知道就不猜；
- 冻结写者面使「Unknown」逐步趋近于可解释边界（CPS 的收口目标）；
- Waiver 通道保留合法例外（如 Zero Harmony、只读 instrumentation）而不破坏纪律。

## Consequences

- 新命令写者必须注册 `CommandSource` 并通过归因链；未注册写者 = 潜在盲区，需在接入时显式处理；
- 历史归因结论不得复用（需 CPS-4 以 Observed/Inferred/Unknown 严格口径重建基线——BACKLOG BL-004 关联，非本 ADR 范围）；
- 冻结解除状态与现行验证态以 CURRENT_STATE 为准（本 ADR 不维护动态值）。

## Related Canonical Rules

- CONSTITUTION#command-ownership（§7）、INDEX §4 Waiver Contract、repo AGENTS.md 不变量 #5/#6

## Related Evidence

- `CombatBrain/Coordination/CommandProvenance.cs`（实现）
- legacy CONTEXT §4 CPS 卡史 + `docs/cps3*-*.md`（运行期证据，CPS 域，仍在闭环中；只作指针不作结论源）

## Supersedes / Superseded by

- Supersedes：`OriginalUnknown=NativeTeamAI` 与「原生占比 X%」结论。
- Superseded by：无（若 CPS-4 产生更严格的归因口径定义，另行修订本 ADR）。
