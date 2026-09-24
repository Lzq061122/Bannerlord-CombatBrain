# R1 · RBM 指挥/战术系统研究（相似点对照）

> 证据等级：`已读代码确认` 指本文件所列路径行号已核验；`文档声称` 指仅来自既有研究报告未逐行验证。

## §1 RBM 指挥/战术系统结构概览

RBM 的 AI 是三级联动的 Harmony 补丁体系（已读代码确认）：

- **战术层（Tactic）** — `Tactics.cs` 注册按文化/攻守的自定义 `TacticComponent` 子类（`RBMTactic*`，`Tactics.cs:200-382`）。职责仅两件事：**兵力分配**（`SetBehaviorWeight` 挂载行为权重）+ **接战前后两阶段切换**（`Utilities.HasBattleBeenJoined`，`Utilities.cs:220`）。不计算坐标。
- **行为层（Formation AI）** — 7 个自定义 `RBMBehavior*.cs`（骑兵冲锋/打带跑/弓散兵/骑射椭圆/楔形/侧翼）+ 13 个 `OverrideBehavior*.cs`（接管原生 Charge/Advance/ProtectFlank 等）。职责：算相对敌我几何的**部署位置**与**状态机**（`RBMBehaviorCavalryCharge` 四态机）。
- **单兵/个体层** — `Frontline.cs` 接管 `GetOrderPositionOfUnit`/`GetDirectionOfUnit`（`Frontline.cs:231,159`），逐兵微操；`OverrideAgent.cs`/`OverrideHumanAIComponent.cs` 改个体行为参数；`StanceLogic.cs`(112KB)+`AgentStances.cs`+`PostureDamage.cs` 逐兵姿态/体力↔攻防数值联动。
- **非 AI 改动** — `RBM.dll` 的 Combat/Spear/Unit Overhaul（伤害、武器、阵型观感），不在本对照范围。

## §2 相似点对照表（本报告重点）

| RBM 机制 | 文件路径 | 对应宪章条款 | 相似度 | 值得我们采纳的部分 | 我们不采纳及原因 |
|---|---|---|---|---|---|
| 三层解耦（战术→行为→单兵） | `Tactics.cs` / `RBMBehavior*.cs` / `Frontline.cs` | §19/§20/§27 | 中 | **计划层与执行层分离**的架构思想 | 单兵层（§20 明确非目标）不采纳 |
| 战术层只做兵力分配+行为权重，不碰坐标 | `Tactics.cs:457-555` | §7/§8（Plan≠Command） | 高 | "计划层不负责具体坐标"印证 Plan/Command 区分 | 按文化定制战术（`Tactics.cs:200-382`）违反 §17 个性暂缓+§26 通用优先 |
| 目标选择"有意义化"（大编队优先，不追零散小股） | `Utilities.cs:979 FindSignificantEnemy` | §6 敌情认知 / §10 弱点利用 | 中 | 感知层应识别"有意义目标"，避免阵型被勾走 | 它用固定兵种类型过滤写死规则，我们应走通用价值评价（§26） |
| 接战判定 + 两阶段权重切换 | `Utilities.cs:220 HasBattleBeenJoined` | §9 主动权 / §27 循环 | 中 | "阶段化状态机"建模接战前后的思路 | 用固定权重切换而非统一计划驱动（§8） |
| 装备驱动布阵（按武器归位正确阵型） | `Tactics.cs:680-743` | §5 己方结构认知 | 低 | 需理解兵种能力结构（我方感知输入） | 强制重排个体，越界 §20 |
| 系统性关闭原生弱智行为（PullBack/Stop/Reserve=0、撤退禁用） | `Tactics.cs:672-674,385-446` | §18 胜负并重 / §9 | 低 | （无直接采纳） | 靠关权重解决，违反 §26 通用机制优先 |
| 行为层基于敌我几何算位置（抗骑盾墙、骑兵四态机） | `OverrideBehaviorCharge.cs`,`RBMBehaviorCavalryCharge.cs` | §8 指令执行 | 低 | 仅作"Formation 层能下达什么粒度指令"的参考 | 属原生 Behavior 层，我们边界止于 Formation（§19） |

## §3 RBM 与宪章冲突 / 不适用的设计

- **第 20 条（不进单兵层）**：`Frontline.cs:231` Prefix 直接改写每个兵的阵位、`Frontline.cs:159` 改个体朝向、`OverrideAgent.cs` 改架枪决策——均深入 Agent 个体层，与"决定 Formation 去哪，Agent 如何战斗交给原生 AI"直接冲突。
- **第 3 条（不靠数值与隐藏优势）**：`StanceLogic.cs`+`PostureDamage.cs` 逐兵姿态→攻防数值联动是**个体数值机制**（虽玩家可见、非隐藏 Buff，属"拟真"），但本质是越界到战斗层，非指挥决策能力，与"胜负来自指挥博弈"（§25）相悖。
- **第 26 条（通用机制优先，而非专用情形规则）**：RBM 以**专用行为类堆积**取胜——7 个 `RBMTactic*` + 7 个 `RBMBehavior*` + 13 个 `OverrideBehavior*`，按文化/兵种/场景写死规则链。这恰是宪章要求避免的"大量相互独立的战术模块"，与"少量共享机制组合产生多行为"相反。
- **第 17 条**：文化定制战术表（`Tactics.cs:200-382`）是"指挥官个性系统"雏形，我们当前阶段暂缓且反对专用规则。

## §4 RBM 观感/战术契约可借鉴的表达层经验

RBM 不提供全局意图提示，但靠**克制的行为集**让玩家可读懂 AI 在干什么：`RBMBehaviorCavalryCharge` 用 Charging/Reforming 状态机 + Embolon 楔形可见（`RBMBehaviorEmbolon.cs`）；步兵见骑自动排盾墙（`OverrideBehaviorCharge.cs`）；阵型排列（盾墙/线列/松散）由装备自动选定——玩家能从阵型形态反推意图。

→ 可借鉴点：**用有限、稳定的 Formation 级行为，自然流露整体意图**，呼应宪章 §22"玩家能识别 AI 整体作战意图"。但我们更进一层：应有显式、可解释的作战目标语义，而非仅靠行为自然流露。

## §5 给总设计师的建议

1. **[可采纳的设计输入]** 分层解耦原则。RBM 印证"计划层不计算坐标、执行层算相对几何"可行（`Tactics.cs`/`RBMBehavior*`）。可直接作为我们 Plan/Command（§8）分层的架构依据。
2. **[可采纳的设计输入]** "目标选择有意义化"（`Utilities.cs:979`）。我们的敌情/威胁感知层应识别"有意义目标"并赋予价值权重，避免被小股勾走——作为通用评价输入，而非专用"发现暴露弓兵"规则。
3. **[验证基准，非设计来源]** 用 RBM 作为"强 AI 行为基线"。验证我们只做 Formation 层决策时，战场表现能否不弱于 RBM 行为层；其单兵机制仅用于标定下限，不可作为设计来源。
4. **[验证基准]** `StanceLogic.cs`/`PostureDamage.cs` 证明"个体机制能显著改变体验"，反向告诫：我们的胜负必须来自指挥博弈（§25），不能靠任何个体数值优势——包括"非隐藏"的优势。
5. **[可采纳的设计输入]** 决策限频/迟滞思路：`Frontline.cs:909 aiDecisionCooldownDict`、`OverrideMovementOrder.cs:29 positionsStorage` 印证 §16 指挥延迟/决策节奏必要性；我们应把节奏放在**指挥层**（计划最小稳定时间、命令传导延迟），而非单兵冷却。
