# CombatBrain CONSTITUTION — Canonical Normative Core

> status: active
> authority: canonical-bootstrap
> 依据：CombatBrain Knowledge Architecture — Phase C3（Normative Canonicalization）。
> 本文件是 CombatBrain **稳定规范核心（canonical NORMATIVE CORE）的唯一 canonical 家**。
> AGENTS / INDEX 只摘要或路由到本文件；Master Spec 可详述产品/设计意图，但不得与本文件静默竞争。

---

## 1. Role Contract

- **Authority axis**：NORMATIVE（规范层，最高常驻）。
- **职责**：承载稳定原则、不变量、架构约束 —— 即「应该怎样运行」。
- **必须不含**：当前 HEAD / commit 数 / 断言总数 / active task / 当前 blocker / 当前阶段 / 团队构成 / 执行日记；benchmark 实测结果（属 evidence）。
- **覆盖规则**：Operational（任务/指令）不得在无显式 valid waiver / architecture decision 时覆盖本文件。
- **冲突语义**：Descriptive（代码/证据/测试）不自动改写本文件；合法例外必须走 Waiver Contract（INDEX §4）。
- **维护 owner**：Captain / Integration Owner。内容级修订 = 架构级变更，需显式 rationale / review。

## 2. 来源层级（C3 裁决后）

```text
CONSTITUTION.md（本文件）        = canonical 稳定规范核心（NORMATIVE）
repo AGENTS.md §2                 = 不变量摘要 + 路由（指向本文件）
CombatBrain_Master_Spec.md        = long-form 产品/设计规格 + 历史设计源
                                  （§1.3/§2.x 等详述仍在；不高于 CONSTITUTION）
legacy CombatBrain_CONTEXT.md §2  = LEGACY_COMPATIBILITY 重复副本（临时并存，直到 legacy 工作流退役）
```

> 语义锚引用格式：`CONSTITUTION#<anchor>`（如 `CONSTITUTION#anti-cheat-gate`）。禁用 CONTEXT 行号作长期引用。

---

## 3. Architecture Principles

<a id="architecture-principles"></a>

### 3.1 分层与横切架构（stable）

- **三层**：指挥层（战术意图）→ 编队层（编队行为/目标）→ 个体层（走位/格斗/求生）。
- **两横切**：① 感知管线（信息迷雾，定义 AI「看得到什么」）；② 行为 LOD 调度器（定义 AI「花多大开销想」）。
- **节奏律**：上层低频、下层相对高频；各层受性能预算约束（见 #performance-discipline）。
- **层边界稳定**：功能在层内增量叠加；不得以「加功能」为由破坏分层边界。
- 来源：CONTEXT §2 三层两横切；Master Spec §1.3 P2 + §2 规格载体。语义 = SAME/COMPLEMENTARY。

### 3.2 产品边界（scope discipline）

- 只改 AI 军队的「脑子」：不改玩法数值、不加新单位、不给玩家操作新按键。
- 不重写/深度接管游戏原生 AI 行为体系；不做平衡性改动；不做玩家操控层。
- 不追求「AI 永远正确」：系统性保留误判与迟疑（拟人推论）。
- 来源：Master Spec §1.2；AGENTS §1。与 Master Spec = SAME。

---

## 4. Design Principles（产品级设计原则）

<a id="design-principles"></a>

产品级原则（Pillars）表述「产品为何如此设计」；与 §5 铁律的关系见 §5 注（DIFFERENT_LEVEL，非冲突）。

| # | 原则 | 内容（canonical 表述） |
|---|---|---|
| P1 | 拟人非全知（信息公平） | AI 决策输入限于「战场上任一士兵肉眼可见信息」（位置/朝向/规模/阵型/可见事件）；禁全图、禁敌方内部状态、禁玩家元信息；推论：AI 必须允许误判与迟疑 |
| P2 | 分层决策架构 | 指挥层（定意图）→ 编队层（编队行为与目标）→ 个体层（微操作）；上层低频下层相对高频；见 §3.1 |
| P3 | 剧场效应 | 玩家视野/关注范围内完整高智能；不可见区域自动低开销模式——「眼前聪明且流畅，远处可以笨一点」 |
| P4 | 性能纪律 | 新决策走「事件驱动 → 低频周期 → 每帧」优先级；禁止无节制逐帧全量计算；详见 #performance-discipline |

来源：Master Spec §1.3。与 CONTEXT §2 铁律的关系：**每一条铁律是一条 Pillar 的硬约束投影**（见下），P2 无对应铁律（结构性原则而非禁令）。

---

## 5. Iron Laws / Hard Constraints（铁律与硬约束）

<a id="hard-invariants"></a>

铁律 = 不可谈判的硬约束，表述为绝对禁令。任何方案/任务不得以「偶尔可能有用」「这次例外」绕过；例外只能走 Waiver。

### IL-1 拟人 > 全知无敌（信息公平的硬形式）

> 禁全图、禁读指针、禁零延迟。

- 全图/敌方内部状态/玩家元信息一律不读；决策必须容忍不确定性。
- 来源：CONTEXT §2 铁律①（禁全图、禁读指针、禁零延迟）；Master Spec P1。
- 关系：SUBSET/硬投影 —— P1 的原则语义 ⊆ IL-1 的禁令语义；IL-1 额外把「零延迟」列为明令禁止项。

### IL-2 性能预算制（性能纪律的硬形式）

> 事件驱动 > 低频周期 > 每帧；聚合 > 个体遍历。

- 来源：CONTEXT §2 铁律②；Master Spec P4。关系：SAME（不同措辞，同一条纪律）。

### IL-3 剧场效应（剧场语义的硬形式）

> 玩家可见区满血 AI，其余优雅降级。

- 高开销行为仅在玩家可见区运行；出区走幂等清理分支、交还原版/低开销路径。
- 来源：CONTEXT §2 铁律③；Master Spec P3。关系：SUBSET/硬投影 —— P3 含「观感解释」，IL-3 取其中硬约束部分。

### HC-1 稳定性（异常优雅降级）

> 任何新增路径必须异常捕获/降级，不崩游戏、不刷日志。

- 来源：Master Spec NFR N6；AGENTS 不变量 #4（稳定性）。关系：AGENTS/Master Spec = SAME，CONTEXT 未单列（由其 §7 工作约定覆盖）。
- 注：此条**不是**「第四条铁律」，属独立硬约束，与 IL-1~3 并列存在、互不隶属。

> §4/§5 关系结论（C3）：Pillars（设计意图）与 Iron Laws（硬约束投影）**按不同抽象层共存，不强行合并为一条列表**；两处来源（CONTEXT §2 / Master Spec §1.3）语义一致，无 TRUE_CONFLICT。

---

## 6. Anti-Cheat / Realism Gate

<a id="anti-cheat-gate"></a>

闸 = **代码注释纪律 + 分层开关语义**，不是单一运行时模块。canonical 语义如下：

### 6.1 本体与自检

- 闸本体 = 代码中的反作弊注释纪律（历史上核实为 15 处），非运行时模块、无玩家入口。
- 自检门：任何新方案先问「它拿到了它不该拿的信息吗？」（source：CONTEXT §2）。
- 本体不可被开关关闭。

### 6.2 可配置的现实维度（user-configurable realism dimensions）

- **A 信息获取约束**（信息迷雾）：MCM 开关（默认开）。开 = 拟人（AI 只见视野内/已知目标）；关 = 全知（跳过感知计算，等价 X6 前现状）。
- **B 反应/决策延迟**（拟人迟疑）：MCM 开关（默认开）。开 = 士兵迟疑拟人；关 = 果断即时（只缩参数：hold/margin/抖动强度；不改逻辑结构）。
- **开关硬规则**：开关只加减「约束」，不新增「信息」——全部关掉后 AI 信息集 = 现状（全知公开聚合），**不大于现状**。

### 6.3 永久强制层（permanently enforced integrity constraints）

- **C 层永不可开放**：禁读指针、禁全图遍历个体、性能红线。
- 理由语义：这不是「聪明与否」问题，是**崩溃与帧率坍塌**问题。

### 6.4 玩家可见术语约束（player-facing terminology）

- 菜单/UI 禁用「反作弊」字样；以「真实度」组下的「决策延迟」「战场信息迷雾」命名，默认均为开。
- 早期三档设计（Strict / Tactical / Omniscient，T40-01）已废弃，以两层拆分为准。

### 6.5 来源与未决点

- 来源：CONTEXT §2 + CONTEXT §4「反作弊闸重要澄清」+ Master Spec §2.7 + AGENTS 不变量 #1。语义 = COMPLEMENTARY（三源互补，无 TRUE_CONFLICT）。
- 未决（descriptive，非规范冲突）：X0 开关实际落 MCM「通用」组（命名不一致已记录，见 Master Spec §2.7）——不影响 §6.4 术语约束。

---

## 7. Command Ownership / CPS Normative Principles

<a id="command-ownership"></a>

仅收录 CPS 中**稳定规范**部分；CPS 任务卡 / 运行期报告 / CPS-3R 历史 / GO-NO-GO 记录一律不在此（frozen，later phases）。

### 7.1 归因纪律（attribution discipline）

- `CommandProvenance` 只记录 CB 自己的调用点；不得把非 CB 写入归因为 CB，反之亦然。
- 运行期数据严格区分 **Observed / Inferred / Unknown**；旧结论「原生占比 X%」「OriginalUnknown=NativeTeamAI」已废弃，不得继续当实锤引用。
- 来源：AGENTS 不变量 #6；CURRENT_STATE（归因链路历史口径废弃注记）。关系 = SAME。

### 7.2 功能冻结令（CPS GO 前）

- CPS GO 前禁止新增：新文化战术 / 新 Formation Mission / 新兵种角色 / 新播报 / 新主动移动 Glue / 新状态机 / 新直接 SetOrder 调用点。
- 冻结的解除状态 = 工程状态（见 `CURRENT_STATE.md`），本文件不维护其当前值。
- 来源：CONTEXT §4 CPS 程序定义；AGENTS 不变量 #5。关系 = SAME。

### 7.3 命令写入纪律

- 命令仅状态跃迁时下发，不每拍刷命令（避免与玩家指挥/原版 TeamAI 打架）。
- 玩家队（Team.IsPlayerTeam）豁免的判定按各功能既有 MCM 开关语义，语义统一、不逐功能发明。
- 来源：Master Spec §2.4 编队命令纪律；CONTEXT §3/CPS 各卡执行规则（抽取共性）。

### 7.4 Waiver 要求

- 任何对 CONSTITUTION / 架构约束 / system contract 的合法例外，必须先 STOP 并以显式 Waiver 记录（字段契约见 INDEX §4）。
- 已知例外形态（`Zero Harmony`、只读 diagnostic instrumentation）各自显式记录边界，禁止 Agent 自行解释二者关系。

---

## 8. Performance Discipline

<a id="performance-discipline"></a>

> 区分：本节约束 = **规范**；实测数值 = **evidence**（在 evidence 域，不进入本文件）。

### 8.1 决策通道优先级（normative）

- 事件驱动 > 低频周期 > 每帧；新增逻辑默认挂事件或低频拍，**禁止无节制逐帧全量**。
- 聚合 > 个体遍历：优先编队级聚合/官方预计算（O(1) 缓存），个体级仅在必要时且带闸门/冷却。
- 逐帧兜底必须带相位错峰 + 决策冷却。
- 来源：CONTEXT §2 铁律② / §7 工作约定；Master Spec §2.6 性能预算细则。关系 = SAME。

### 8.2 红线目标（normative acceptance target）

- 500 人基准场锁 30 FPS 时，AI 决策总开销 ≤10% 帧预算（约 3–5 ms/帧），无持续尖峰。
- 来源：Master Spec NFR N1；CONTEXT §7 性能红线。此为**目标红线**；各代实测值在 evidence/报告，不回填本节。

### 8.3 剧场联动降级

- 不可见区自动低开销；剧场半径外的高开销行为走幂等清理并交回低开销/原版路径（同 IL-3）。

---

## 9. 已裁决来源关系汇总（C3）

| 概念 | 源 A | 源 B | 源 C | 关系 | Canonical 落点 |
|---|---|---|---|---|---|
| 分层架构 | CONTEXT §2 | Master Spec P2/§2 | AGENTS §1 | SAME/COMPLEMENTARY | §3.1 |
| 产品边界 | — | Master Spec §1.2 | AGENTS §1 | SAME | §3.2 |
| 设计原则 | — | Master Spec §1.3 | CONTEXT §2（投影） | DIFFERENT_LEVEL | §4 + §5 注 |
| 铁律 | CONTEXT §2 | Master Spec P1/P3/P4 | AGENTS #1–3 | SUBSET/硬投影 | §5 |
| 稳定性 | CONTEXT §7(隐含) | Master Spec N6 | AGENTS #4 | SAME | §5 HC-1 |
| 反作弊闸 | CONTEXT §2+L205 | Master Spec §2.7 | AGENTS #1 | COMPLEMENTARY | §6 |
| 归因/冻结/Waiver | CONTEXT §4(规范部分) | AGENTS #5/#6 | INDEX §4 | SAME | §7 |

**无 TRUE_CONFLICT 被裁决为保留**；个别措辞差异（铁律 vs 支柱命名、MCM 开关分组命名）按 §4/§6.5 处理为层级/描述差异，不作冲突升级。

## 10. Legacy 兼容注记

- legacy `CombatBrain_CONTEXT.md` §2 / §4 反作弊闸澄清在 C3 **不改动**（LEGACY_COMPATIBILITY 临时重复）。
- 自 C3 起，canonical 规范权威 = 本文件；引用规范语义一律走 `CONSTITUTION#...`。
