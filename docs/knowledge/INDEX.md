# CombatBrain Knowledge INDEX — Context Router

> status: pilot
> authority: not-yet-exclusive
>
> 依据：CombatBrain Knowledge Architecture v1.1 — Phase A。
> 本文件是 **Context Router**，不是目录清单。它决定 Agent 冷启动后加载什么、不加载什么。
> Phase A：旧文档物理位置不变；legacy `CombatBrain_CONTEXT.md` 仍对旧 Harness 权威（见 §24 规范）。

## 0. 稳定地址约定

```text
repo 根：Machine-local locator（当前机器 = E:\Agent\6a9268112aeaf49072bef052）
project_id: CombatBrain   ← canonical identity，与物理路径解耦
```

文档定位尽量记 **logical role + canonical identity**，本机绝对路径仅作为当前 locator。

## 1. 三种加载集合（每条 route 只认这三种）

| 集合 | 含义 | 默认动作 |
|---|---|---|
| REQUIRED | 本任务类型必须读取 | 读全文 |
| OPTIONAL | 仅在明确相关时读 | 先判断再读 |
| NEVER_BY_DEFAULT | 默认禁止加载 | 不读（除非任务显式要求且说明理由） |

加载纪律：不默认全读 docs 树；二级引用不自动追踪；archive/evidence 默认不加载；
Router 的目的是**减少 context**，不是把全读变成层层跳转后仍全读。

## 2. Bootstrap Contract（所有 Agent 最终进入这里）

```text
Codex / repo 内 Agent:
  repo 根 AGENTS.md → docs/knowledge/INDEX.md → CURRENT_STATE.md(按需)

Harness Captain / member（Phase A 尚为 spec，未强制注入）:
  CombatAI/AGENTS.md 定义的 locator → repo:/docs/knowledge/INDEX.md
```

任何入口缺失 → `BOOTSTRAP_FAILURE`（详见 repo 根 AGENTS.md §6）。

## 3. 权威模型摘要

| 轴 | 问题 | 依据顺序 |
|---|---|---|
| Normative | 应该怎样运行 | Constitution/invariants > 架构约束 > 子系统规格 > Waiver > 历史 |
| Descriptive | 代码实际怎样 | source / runtime evidence / tests / git state —— 都是 scoped claims，非永恒真相 |
| Operational | 此刻做什么 | 用户授权边界 > bootstrap > Active Task > CURRENT_STATE > System Spec > handoff |

冲突协议：Operational 不能覆盖 Normative；Descriptive 不自动改写 Normative；
合法例外必须走 **Waiver**；无法调和 → `STOP → record → escalate`。

## 4. Waiver Contract（机制定义；Phase A 不建 registry）

任何对 Constitution / constraints / system contract 的合法例外，必须满足：

```yaml
waiver_id: <唯一 id>
scope: <例外覆盖范围>
reason: <为什么必须例外>
owner: <责任人>
status: <draft/active/expired>
applies_to: <受影响的规范条款>
expiry_or_supersession: <到期或被谁取代>
source_decision: <批准该例外的决策出处>
```

已知真实例外示例（供格式参考，不代表本文件已批准）：
`Zero Harmony` 与 `只读 diagnostic instrumentation 例外` 必须各自显式记录边界，
禁止让 Agent 每次自行解释二者关系。
新的例外主张 → 先 STOP 并升级，禁止任务卡悄悄绕过。

## 5. Context Routes

### Route R1 — 通用冷启动 / 任意任务开始
- REQUIRED: `docs/knowledge/CURRENT_STATE.md`
- OPTIONAL: 任务相关子系统 spec
- NEVER_BY_DEFAULT: 历史 handoff、旧 X/N/T prompts、research archive、Master Spec 全文

### Route R2 — CPS（战场命令控制面收口）/ 命令归因域
- REQUIRED: `docs/cps4-hardgate-checklist-*.md`（最新一张）、
  `CombatBrain/Coordination/CommandProvenance.cs`、当前派单卡所在区
- OPTIONAL: `docs/cps3*-*.md` 运行期报告、CONTEXT §4 CPS 条目
- NEVER_BY_DEFAULT: 已关闭的 PoC 路线报告（cps2-route-a-research.md 等，除非对比需要）

### Route R3 — Formation / command-control 实现任务
- REQUIRED: CURRENT_STATE、当前 active task、formation/coordination 源与规格
- OPTIONAL: command-control spec（若涉所有权）、相关 evidence
- NEVER_BY_DEFAULT: 历史 handoffs、旧 prompts、research archive、Master Spec 全文

### Route R4 — 文化 / 战术设计 / 产品级判断
- REQUIRED: `CombatBrain_Master_Spec.md`（当前唯一产品规格；版本见其 §0，可能落后于代码）、
  对应文化/战术规格
- OPTIONAL: CONTEXT §2 核心设计、历史战术设计
- NEVER_BY_DEFAULT: 派单措辞、验收断言写法、施工细节

### Route R5 — 纯代码修改 / bug 修复（不涉架构）
- REQUIRED: 目标子系统源码、CURRENT_STATE、`测试指南.md`（编译/回归/部署命令链）
- OPTIONAL: 相关 tests
- NEVER_BY_DEFAULT: Master Spec、architecture docs、历史设计

## 6. `_kb` 状态（Phase A Quarantine）

```text
_kb status: NON-AUTHORITATIVE
DO NOT use _kb as source of truth.
DO NOT use _kb for cold start.
DO NOT edit canonical project knowledge through _kb.
```

`_kb` 可能经 hard link 直接改到 source，也可能因 atomic save 断链静默分叉。
若 `_kb` 与 source 内容不同 → 相信 source。

## 7. CONTEXT.md 当前角色

```text
CombatBrain_CONTEXT.md: KEEP / UNCHANGED / AUTHORITATIVE FOR LEGACY BOOTSTRAP
（Harness 旧工作流继续全文读取；新架构不再把它设计成长期知识中心。）
```

Phase A 期间不瘦身、不 redirect、不删 section、不修引用。
