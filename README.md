# CombatBrain

《骑马与砍杀 II：霸主》（Mount & Blade II: Bannerlord，v1.4.8）的 C# 战术 AI Mod。

本仓库同时容纳两个层次的项目资产：

- **`CombatBrain/`** —— 当前可运行的 Mod 源码（1.x 实现）
- **`CombatBrain-2.0/`** —— 下一代设计层资产（Charter / Roadmap / 设计草案），**不含代码**

---

## 仓库结构

| 目录 / 文件 | 文件数 | 内容 |
|---|---|---|
| `CombatBrain/` | 109 | Mod 源码（C#）。`SubModule.xml` 的 `Id = CombatBrain`，`Version = v0.1.0` |
| `CombatBrain-2.0/` | 18 | 2.0 设计层：Charter、Roadmap、功能拆解草案、Agent 角色定义。**0 个 `.cs`** |
| `docs/` | 25 | 架构决策记录（ADR）、知识库、CPS 系列运行期报告 |
| `OfflineTests/` | 6 | 离线回归测试套件与静态检查 |
| `测试指南.md` | 1 | 编译 → 回归 → 部署 → 提交 的完整验证链 |
| `AGENTS.md` | 1 | Agent 启动入口（仓库身份、不可变约束、权威冲突协议） |
| `CPS3R-RouteA-repair-review.md` | 1 | Route A 修复的独立复核报告 |

### `CombatBrain/` 模块划分

`Ai` `Battle` `Bench` `Cavalry` `Combat` `Compliance` `Coordination` `Events`
`Experience` `Formation` `Intent` `Lod` `Mcm` `Morale` `Movement` `Perception`
`Ranged` `Settings` `Survival` `Telemetry`

---

## 相关仓库

| 仓库 | 内容 |
|---|---|
| [`Lzq061122/Bannerlord-CBATH`](https://github.com/Lzq061122/Bannerlord-CBATH) | **CBATH（CombatBrain Autonomous Test Harness）** —— 独立的自动化测试基础设施，目标是不经人工操作完成「启动游戏 → 进入战斗 → 观测 → 采集证据 → 判定」闭环 |

CBATH 与本仓库**无源码依赖**：其 108 个跟踪文件中引用本仓库路径的数量为 0。它只在运行期通过两个自带的 legacy adapter 读取 CombatBrain 日志做时间关联。

CBATH 的定位是**验证工具，不是 CombatBrain 2.0 决策能力的完成证据**（见 `CombatBrain-2.0/charter/CombatBrain-2.0-Development-Roadmap-v0.1.md`）。

---

## 两个目录的关系

`CombatBrain-2.0/` 是 **2.0 的设计权威来源**，`CombatBrain/` 是 **当前在跑的代码**。
二者不是"新旧两个版本"的简单关系：

- `CombatBrain-2.0/charter/Charter V0.2.md` —— 2.0 的最高层项目约束
- `CombatBrain-2.0/charter/CombatBrain-2.0-Development-Roadmap-v0.1.md` —— 定义 P0–P6 七阶段，并明确「CBATH（测试工具）是验证工具，不是 CombatBrain 2.0 决策能力的完成证据」
- `CombatBrain-2.0/Program/README.md` —— 2.0 源码根的职责声明（当前尚无实现；**实现层不得重新定义或覆盖 Charter 的上位产品目标和约束**）

**`CombatBrain-2.0/` 必须位于仓库根，不可移入 `CombatBrain/` 内。**
`CombatBrain/CombatBrain.csproj` 是 SDK 风格工程、显式 `<Compile Include>` 为 0 条，默认通配 `**/*.cs` —— 移入会被编译扫描或污染工程边界。

---

## 构建与验证

环境要求：**Bannerlord v1.4.8**、**.NET SDK**、`rg`、`ilspycmd`、`Git`。
目标框架 `net472`，`LangVersion 7.3`。

质量门顺序（**编译 → 回归 → 部署 → 提交**，不得跳过）：

```powershell
# 1–2) 编译 + 完整离线回归（通过标志：退出码 0 且末尾 OFFLINE SUITE PASS）
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "OfflineTests\RunOfflineTests.ps1"

# 3) 部署（目标 Modules\CombatBrain；游戏进程必须已退出）
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "CombatBrain\deploy.ps1"
```

离线入口会依次重新构建生产 Release、重新构建测试 Debug、运行回归、运行 `RouteAStaticChecks.ps1`，并在 `OfflineTests/bin/verification/<run-id>/` 保存各阶段日志与 `result.json`（含 Git HEAD、源码输入摘要、生产 DLL / 测试 EXE / 游戏 DLL 的 SHA256）。

**证据边界**：离线通过不代表游戏内调度、Harmony 实际挂载或运行期验收通过 —— 报告会明确标记 `Runtime=NOT_RUN`。

游戏内观测：日志位于 `%TEMP%\CombatBrain.log`，另有 `CombatBrain.telemetry.json`。
详见 [`测试指南.md`](测试指南.md)。

---

## 文档入口

从 [`docs/knowledge/INDEX.md`](docs/knowledge/INDEX.md) 开始 —— 它是上下文路由器，决定本次任务应加载哪些知识源。

| 文件 | 用途 |
|---|---|
| `docs/knowledge/INDEX.md` | 必读：Context Router |
| `docs/knowledge/CONSTITUTION.md` | 项目宪法（不可变约束的完整权威） |
| `docs/knowledge/CURRENT_STATE.md` | 当前状态（先核对 `last_verified_commit`） |
| `docs/knowledge/NOMENCLATURE.md` | 术语表 |
| `docs/knowledge/PAIPOS_LITE.md` | 项目工作流 |
| `docs/knowledge/BACKLOG.md` | 待办与已知缺口 |
| `docs/adr/` | 架构决策记录 |

约束：加载目标是**最小充分上下文**。不递归追链接、不默认读 archive / evidence / Master Spec；历史材料只用于回答「为何选型 / 是否故意变更 / 早先实现 / 是否取代旧决定 / 是否回归已知缺陷」，**加载 ≠ 成为现行指令**。

---

## 分支

| 分支 | 说明 |
|---|---|
| `main` | 主开发线 |
| `feature/agent-control-retirement` | Agent 控制权移交治理归档（历史留档，不再开发） |
