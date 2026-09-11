# CombatBrain NOMENCLATURE — Stable Identifiers & Naming Rules

> status: active
> authority: canonical-bootstrap (Phase C2 建立)
> 依据：CombatBrain Knowledge Architecture — Phase C2。
> 角色契约见下。本文件是**编号/术语的 canonical 家**；CONTEXT 头部与 Master Spec §0.1 的重复表在 C3 去重。

---

## 1. Role Contract

- **Authority axis**：NORMATIVE（对**标识符**而言）。
- **职责**：承载稳定标识符、命名规则、编号体系、历史改名映射 —— 供解释任何现行/历史材料。
- **必须不含**：进度日志、当前状态、HEAD / 断言数 / active task。
- **判据**：一条编号/术语只有进入本文件才具备跨文档引用效力；引用必须写明体系（如「功能 F1」「流水线 S2」），禁止裸写。
- **维护 owner**：Captain / Integration Owner；新增前缀（如新系列编号）需在引入文档中先登记于此。

## 2. 编号体系（stable；来源：CONTEXT 头部 + Master Spec §0.1，两源在 C3 去重）

| 前缀 | 含义 | 备注 |
|---|---|---|
| `F<n>` | **功能编号**：F1 感知管线 / F2 意图引擎 / F3 行为LOD / **F4 预测式格挡〔个体部分 RETIRED〕** / F5 士兵素质 / **F6 走位〔个体部分 RETIRED〕** / **F7 求生重整〔逐兵撤退腿 RETIRED〕** / F8 骑兵波次 / **F9 远程〔逐兵选敌·换武器·后撤 RETIRED〕** / F10 反制+套路记忆 / F11 士气传导+撤退 / **F12 决策抖动〔个体抖动 RETIRED〕** | 产品功能，非流水线。**RETIRED 语义（2026-09-11，ADR-0005）**：编号沿用不改，但被标 RETIRED 的部分**不再是现行功能**，不得据编号推断其仍生效。仍保留：F6 编队级 MovementOrder/Mission/Route A、F7 编队级重整信号、F9 编队级远程判断、F12 指挥层与编队级迟滞 |
| `S<n>` | **流水线阶段编号**：S1 枚举层 → S2 OpponentModel → S3 ProtectGlue → S4 BraceGlue → H4 远期 | **改名史**：2026-09-01 前称 F1–F4，与功能 F1–F4 重名故废弃；旧文档中的流水线 F1–F4 = 现 S1–S4 |
| `H<n>` | 历史战术设计概念（H1–H4，S 系列的母设计） | 出处：combatbrain_historical_tactics_design.md |
| `P<n>` | 优先级 / 改进方案编号（P0–P3 序列） | 出处：combatbrain_improvement_plan.md |
| `T<n>-01` | GPT《战术 AI 增强清单》任务项（T0–T39，40 项） | 映射见 GPT清单-T0-T39-现状映射.md |
| `X<n>` | 观感补强任务项（X0–X7） | 2026-09-04 新增；实现记录属 evidence/历史，不在本表列状态。**个体项退役（2026-09-11，ADR-0005）**：**X2 手活属性模型 = RETIRED**（不再注册 `AgentStatCalculateModel`）；**X5 逐兵补位执行腿 = RETIRED**（保留编队构成分类/职责/Arrangement/纪律与纯观察性 Slot 数据）；**X4 阵框释放 = 保留**（编队级统一接战政策，附运行期验证债） |
| `B<n>` | Luna 评审落地批次（B0–B6） | **注意**：与「战局状态机 B 系列」派单编号撞名（Master Spec §5.1 C5 未决） |
| `t<n>` | 团队（agent-teams）任务编号 | 编排层任务，非产品编号 |
| `G<n>` | 博弈层落地阶段（G0–G3） | 并入 P/X 序列 |
| `CPS-<n>` | 战场命令控制面收口里程碑（CPS-0 provenance → … → CPS-4 硬门） | 程序代号，非前缀编号 |

## 3. 命名规则（跨文档引用）

1. 写全体系前缀再跟编号，禁止只写编号（例：写「功能 F1 感知管线」，不写「F1」）。
2. 引用历史材料时注明其当时编号体系（如 .bak 文件中「流水线 F1–F4」按 §2 改名映射解释为 S1–S4）。
3. 问题引用用「标题」不重编号（review-findings 口径），避免重编号漂移。

## 4. C4A 验证记录与去重（C3/C4 后续）

- 与 legacy CONTEXT 头部「编号约定」、Master Spec §0.1 逐项比对：F/S/H/P/T/X/B/t/G 及 CPS 命名**一致**（F10 全名以本表「反制+套路记忆」为准，两源同义）。
- 本文件为编号/术语的 **canonical 家**；legacy 两源在 legacy 工作流退役前保持临时重复，不做同步编辑。
- C4B+ 候选：CONTEXT 头部 / Master Spec §0.1 改为指向本文件；B 系列撞名（Luna B0-B6 vs 战局状态机 B 系列）待人工裁决后在本表登记唯一语义。
