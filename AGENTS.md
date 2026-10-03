<!-- PROMPT META
Name: CombatBrain Repo Agent Bootstrap
Version: 1.1
Author Role: Project Governance Maintainer
Model: GPT-6
Created: 2026-09-09
Updated: 2026-10-01
Target: Codex / Agents starting from the code repo
Scope: CombatBrain / Repo Bootstrap
-->

<!-- CHANGE LOG (newest first, keep last 3)
v1.1 2026-10-01 23:36 | role=Project Governance Maintainer | model=GPT-6 | editor=codex-paipos-20261001 | note=接入 PAIPOS Lite 统一工作流与 Harness 必读要求
v1.0 2026-09-10 21:20 | role=Project Governance Maintainer | model=DeepSeek V4.1 Flash | editor=manager | note=触碰时迁移补齐题头；新增 §7 提交署名（Editor-* trailer）
-->

# CombatBrain — Repo Agent Bootstrap

> status: active
> authority: canonical-bootstrap
>
> 依据：CombatBrain Knowledge Architecture v1.1 — Traffic Cutover（Phase B 验证通过，2026-09-09）。
> 本文件是 Codex / 从本仓启动的 Agent 的标准入口，不是知识中心。
> legacy `CombatBrain_CONTEXT.md` 仅按 INDEX 路由或具体未决依赖加载（LEGACY_COMPATIBILITY）。

## 1. Project Identity

- project_id: **CombatBrain**
- 产品：《骑马与砍杀 II：霸主》（Bannerlord, v1.4.8）C# 战术 AI Mod
- 本机路径只是 Machine-local Project Locator，不是项目恒定身份。
  长期 canonical 文档不得把绝对路径描述为项目身份。

## 2. Immutable Invariants（摘要；完整权威见 docs/knowledge/CONSTITUTION.md，由 INDEX 路由）

> 摘要仅用于快速自检，**不高于** CONSTITUTION；Manager 侧 Master Spec 是产品/设计规格源，不构成更高规范。
> 语义锚：`CONSTITUTION#hard-invariants` / `#anti-cheat-gate` / `#command-ownership` / `#performance-discipline`。

1. 拟人而非全知：禁全图、禁读指针、禁零延迟（反作弊闸：先问「它拿到了不该拿的信息吗」）。
2. 性能预算：事件驱动 > 低频周期 > 每帧；聚合 > 个体遍历。
3. 剧场效应：玩家可见区满血 AI，其余优雅降级。
4. 稳定性：异常捕获降级，不崩游戏。
5. CPS 功能冻结令：CPS GO 前禁新文化战术 / 新 Formation Mission / 新兵种角色 / 新播报 /
   新主动移动 Glue / 新状态机 / 新直接 SetOrder 调用点。
6. 归因纪律：CommandProvenance 只记录 CB 自己调用点；数据严格区分 Observed / Inferred / Unknown，
   旧结论「原生占比 X%」「OriginalUnknown=NativeTeamAI」不得继续当实锤。

## 3. Knowledge Entry

```
docs/knowledge/INDEX.md         ← 必读：Context Router（决定本次加载什么）
docs/knowledge/CURRENT_STATE.md ← 需要当前状态判断时读（先核对 last_verified_commit）
```

## 4. Authority & Conflict Protocol

- 三条 Authority Axis：Normative（应该怎样运行）> Descriptive（实际怎样）> Operational（此刻做什么）。
- **Operational 不得覆盖 Normative**：任务卡不能修改项目宪法 / constraints / system contract。
- **Descriptive 不自动改写 Normative**：代码违反规范 = implementation/spec mismatch，
  不是「规范自动变了」。
- 合法例外必须走 Explicit Waiver（契约见 INDEX.md），禁止任务卡偷偷绕过。
- 冲突无法调和：`STOP → record mismatch → escalate`，禁止自己挑一个喜欢的文档。

## 5. Context Budget Discipline（最小充分上下文）

- 加载目标：**最小充分上下文 + 保持正确性**。上下文不足与过量无关上下文都是缺陷。
- 追加加载前自问：当前任务里哪个具体不确定性需要这份来源？答不出 → 不加载。
- 默认链路：`Route → REQUIRED → 相关源码 → stop`。不递归追链接、不默认读 archive/evidence/Master Spec；
  历史材料只用于回答「为何选型 / 是否故意变更 / 早先实现 / 是否取代旧决定 / 是否回归已知缺陷」，**加载 ≠ 成为现行指令**。
- 能回答「当前状态 / 任务 / 约束 / 所属子系统 / 相关源码 / 所需证据」6 问即停止扩展。
- 详细契约（REQUIRED/OPTIONAL/NEVER_BY_DEFAULT 判据、停止条件、扩展审计）见 `docs/knowledge/INDEX.md` §1.1。
- 结束时能说明本次实际读取了哪些知识源。

## 6. Bootstrap Failure

找不到以下任一 → 明确报告 `BOOTSTRAP_FAILURE` 并指出缺失对象，**不得**：

- 猜路径；搜索其他像 CombatBrain 的 clone；自动退回 `_kb`；
- 根据历史 handoff 猜当前状态；用旧 CONTEXT 状态表冒充当前事实。

原则：Fail explicitly, never silently fall back to stale knowledge.

## 7. 提交署名（Editor trailer）

本机没有全局 `user.name`，人工与所有 AI 会话的提交共用同一 Git 身份，`git log` **看不出是谁改的**。因此：

- 含 Prompt 内容变更的提交，必须在 commit body 末尾携带 `Editor-Role` / `Editor-Model` / `Editor-Team`，三者与文件内 CHANGE LOG 行一致；
- 纯代码 / 构建类提交建议至少写 `Editor-Team`，便于后续区分执行方；
- 不得依赖 `git config user.name` 作为身份来源。

规则出处：`E:\MBmod\manager\Prompt 题头元数据规范.md` §16。

## PAIPOS Lite 项目工作流（所有 Harness 必读）

- 开始项目任务前，先查生命周期登记表 `E:\MBmod\CombatAI\prompts\registry.json` 中 `root=repo, path=docs/knowledge/PAIPOS_LITE.md` 为 ACTIVE，再完整读取该文件；缺失或不可用时报告 BOOTSTRAP_FAILURE，不静默沿用旧流程。
- 本规则适用于开发、设计、质检，以及所有模型和 Harness；不依赖共享聊天历史。新会话、恢复会话及角色接手均须加载；正在运行的其他 Harness 须在下一次任务开始前重新读取。
- 用户手动切换 Harness；当前角色准备简短交接，不自动创建或调度其他角色。小而可控的修改自检即可，跨模块、核心行为或重要交付增加独立质检。
- PAIPOS Lite 统一角色切换和检查力度；项目宪法、冻结令、部署硬门及用户有效约束继续适用。旧建组模板中固定三角色、每次强制独立 review、每次小修改强制建任务/台账的要求，按本工作流的影响范围执行。
- 不自动发现项目规则的 Harness，须在其项目指令或启动提示词中显式注入本段和代码仓 locator；首次接手简短报告实际读取的工作流路径及版本。
