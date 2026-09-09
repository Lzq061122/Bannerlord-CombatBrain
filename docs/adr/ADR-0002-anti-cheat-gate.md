# ADR-0002: 反作弊闸语义模型（Anti-Cheat / Realism Gate）

- **Status**: accepted
- **Date**: 2026-09-10（Phase C4B 登记；裁决本体 2026-09-04）
- **Deciders**: 项目架构决策链（2026-09-04 裁决 → CONSTITUTION §6）

## Context

「拟人而非全知」需要一条可执行的边界：AI 能「看/反应」到什么程度算拟人，什么程度算作弊。早期只是一句原则（CONTEXT §2），无运行时形态；同时用户要求**把信息约束做成可关的 MCM 开关**，且曾有 T40-01 三档（Strict/Tactical/Omniscient）方案在案。需要一个不会每次被重新解释、也不会让开关变成「作弊入口」的语义模型。

## Decision

闸 = **代码注释纪律 + 分层开关语义**（canonical 详见 CONSTITUTION#anti-cheat-gate）：

1. 本体 = 反作弊自检注释纪律（15 处，非运行时模块），不可关闭；自检门：「它拿到了它不该拿的信息吗？」
2. **可配置现实维度（两层，默认开）**：A 信息获取约束（信息迷雾）；B 反应/决策延迟（拟人迟疑）。
3. **开关硬规则**：只加减「约束」，不新增「信息」——全部关闭后 AI 信息集 = 现状（全知公开聚合），不大于现状。
4. **C 层（禁读指针 / 禁全图遍历个体 / 性能红线）永久不可开放**——不是聪明问题，是崩溃与帧率问题。
5. **玩家可见术语**：UI 用「真实度」组「决策延迟/战场信息迷雾」，禁用「反作弊」字样。
6. 早期三档设计（Strict/Tactical/Omniscient）**废弃**。

## Alternatives Considered

1. **Strict/Tactical/Omniscient 三档**（T40-01）—— 拒绝/废弃：存在「全知档」即等于给玩家一个作弊开关；语义被「只减约束不加信息」取代。
2. **做成独立运行时闸模块** —— 拒绝：本体是注释纪律，无玩家入口、无需运行时形态。
3. **只留原则不做开关** —— 拒绝：X0 决策延迟 / X7 信息迷雾需要玩家可调（机器性能与观感取舍），且用户明确要求可关。
4. **C 层对普通玩家开放（仅提示风险）** —— 拒绝：C 层违背后果是崩溃/帧率坍塌，不是「聪明与否」，不可由玩家承担。

## Rationale

- 单一解释消除了「Zero Harmony / 只读 instrumentation / 开关」三者每次重新解释的负担（INDEX §4 已知例外纪律）；
- 区分「约束开关」与「永不开的完整性层」使可配置性不与产品宪法冲突；
- 术语约束防玩家（与开发）把「拟人」误读成「作弊系统」。

## Consequences

- X0 落 MCM「通用」组、X7 落「真实度」组（命名不一致为描述性事实，已记录于 Master Spec §2.7，不影响语义）；
- 任何未来的「拟人度/真实度」类开关都必须先通过「只减约束不加信息」检查，且不得触碰 C 层；
- legacy CONTEXT §2 + L205 澄清与 Master Spec §2.7 在 legacy 退役前保留为临时重复；canonical 以 CONSTITUTION §6 为准。

## Related Canonical Rules

- CONSTITUTION#anti-cheat-gate（§6）、CONSTITUTION#hard-invariants（IL-1）、CONSTITUTION#design-principles（P1）

## Related Evidence

- X0/X7 实现记录（Master Spec §4.4）；legacy CONTEXT §4 反作弊闸澄清；代码 grep 15 处注释（历史核实）
- ADR-0001（信息公平为架构输入）

## Supersedes / Superseded by

- Supersedes：CONTEXT §2 单句闸表述的模糊解释空间；T40-01 三档方案（2026-09-04 裁决后）。
- Superseded by：无。
