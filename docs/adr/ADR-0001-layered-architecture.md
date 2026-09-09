# ADR-0001: 三层两横切分层架构（Layered Architecture）

- **Status**: accepted
- **Date**: 2026-09-10（Phase C4B 登记；决策自项目早期即存在）
- **Deciders**: 项目架构决策链（早期设计 → Master Spec → CONSTITUTION）

## Context

CombatBrain 是给 Bannerlord 大规模会战注入「拟人指挥官」行为的增强 Mod。早期若把全部决策写成一个单体大脑/直接在原生 AI 决策点散点插入，会同时违反**产品边界**（不深度接管原生 AI）与**性能预算**（每帧/逐 agent 全量不可行），也无法表达「指挥官定意图 → 编队执行 → 个体发挥」的战术层次与观感。

## Decision

采用 **三层 + 两横切** 分层模型：

- 指挥层（战术意图）→ 编队层（编队行为/目标）→ 个体层（走位/格斗/求生）；
- 横切 ① 感知管线（信息迷雾，定义「看得到什么」）；② 行为 LOD 调度器（定义「花多大开销想」）；
- 节奏律：上层低频、下层相对高频；各层受性能预算约束；
- 层边界稳定：功能在层内增量叠加。

## Alternatives Considered

1. **单体大脑 / 决策点散插原生** —— 拒绝：无法表达层次意图，性能不可控，逼近「深度接管原生」产品红线。
2. **RTS 式中央微操规划器** —— 拒绝：会接管/取代原生行为体系，违反兼容与观感边界；个体级中央控制不可扩展。
3. **纯参数化调优原生 AI** —— 拒绝：无法表达战术意图、文化差异与「将领」决策叙事（产品主价值）。
4. **只做编队层（无指挥/个体）** —— 早期演进曾考虑，拒绝：缺意图来源与个体差异化，「看起来蠢」问题无法系统性解决。

## Rationale

- 层次结构直接对应「拟人指挥官」叙事与 X 系列的可读战术动作；
- 决策与游戏解耦 → 纯逻辑可离线测试（NFR N5）；
- 低频上层 + 预算约束满足 500 人性能红线（NFR N1）。

## Consequences

- F 系列模块按层归位；X5 表现层作为编队层下方的「战术表现执行」补充层落地（其分层位置见 Master Spec §2.8）；
- 后期 Coordination / CPS 命令仲裁落在指挥/编队层之间的命令域 —— 需保持「层边界稳定、功能在层内叠加」的规则解释力；
- 新子系统加入前必须先回答「它属于哪一层/哪条横切，预算与信息面是什么」（CONSTITUTION 义务）。

## Related Canonical Rules

- CONSTITUTION#architecture-principles（§3.1）、CONSTITUTION#design-principles（P2）
- CONSTITUTION#performance-discipline（§8）

## Related Evidence

- Master Spec §2（核心系统规格载体）、§4.1（F 系列按层实现）
- legacy CONTEXT §2（三层两横切表述）

## Supersedes / Superseded by

- Supersedes：早期「单层散点」口头约定。
- Superseded by：无（仍为现行）。
