# ADR-0003: TerrainStub → TerrainSampler 真实地形替换

- **Status**: accepted（已实施）
- **Date**: 2026-09-10（Phase C4B 登记；实施完成于 TerrainSampler.cs 落地）
- **Deciders**: 项目架构决策链（review-findings #7 驱动）

## Context

意图引擎的 Height 维与 VisibilityOracle 视线判定早期使用**合成正弦假地形**（TerrainStub），即用数学函数假装地面高度。它「看起来有数据」，但高度与真实战场地图无关 —— 感知层会据此得出**错误但自信**的地形结论（如把平坦地误判成高地），比「没有数据」更危险（review-findings #7「假数据比没数据更糟」）。

## Decision

- **删除 TerrainStub 合成正弦假地形**；
- 由 `CombatBrain/Perception/TerrainSampler.cs` 读取**战场真实高度场**替代，服务 Height 维与视线遮挡判定；
- 空/不可用地形走**优雅降级**（返回默认高度/遮挡语义），不崩溃、不退回假数据。

## Alternatives Considered

1. **保留合成正弦假地形** —— 拒绝：假数据会系统性误导意图与视线，违反「信息公平/拟人」语义（AI 该用真实可见信息）。
2. **推迟到「有真实地形」再做** —— 曾作为过渡（fake 先顶），后按 #7 原则提前删除：错误数据的风险 > 缺失数据的风险。
3. **逐 agent 读原生高度** —— 拒绝：逐 agent 采样违反性能纪律（聚合 > 个体遍历），且不必要。

## Rationale

- 真实地形 = 玩家可见、士兵肉眼可见 → 属于「公平信息」（不触碰反作弊闸）；
- 假地形会污染意图引擎 12 维态势中的 Height 与 LoS/射界判断，是观感「看起来蠢」的潜在来源；
- TerrainSampler 纯逻辑、零依赖、可离线测，符合分层与可测试性。

## Consequences

- 感知/意图的 Height 维与 VisibilityOracle 基于真实高度场；
- X5-3 TerrainWarp（阵型地形形变）消费 TerrainSampler 输出契约（Height/Slope 委托注入），null 时降级为原样副本；
- 未来 T16 扩展（坡度/可防守性/视野/射界维度）在 TerrainSampler 上延伸。

## Related Canonical Rules

- CONSTITUTION#design-principles（P1 信息公平）、CONSTITUTION#architecture-principles（感知管线 = 横切①）

## Related Evidence

- 代码注释：`CombatBrain/Ai/CombatAiMissionLogic.cs:237`、`CombatBrain/Perception/TerrainSampler.cs`、`CombatBrain/Perception/VisibilityOracle.cs`
- review-findings-2026-09-01.md #7（假数据比没数据更糟）
- Master Spec §2.5（真实地形感知 FR4.1）

## Supersedes / Superseded by

- Supersedes：TerrainStub 合成正弦假地形（全部调用点）。
- Superseded by：无。
