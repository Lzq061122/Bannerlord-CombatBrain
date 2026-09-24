# CombatBrain Tooling — Stable Technical & Operator Facts

> status: archived
> authority: non-authoritative (historical record)
> superseded-by: E:\Agent\6a9268112aeaf49072bef052  (current 1.x canonical authority)
> historical-note: original canonical-bootstrap declaration was established in Phase C2
> 依据：CombatBrain Knowledge Architecture — Phase C4A（Operational & System Canonicalization）。
> 角色契约见下。稳定工具链/操作事实 C4A 已自 legacy CONTEXT §5/§8 迁入；CONTEXT 段落保留为临时重复。

---

## 1. Role Contract

- **Authority axis**：DESCRIPTIVE（技术/工具链事实域）。
- **职责**：承载稳定技术/工具链事实与操作流程（工具用法、环境约束、稳定验证/部署程序）。
- **必须不含**：动态值（HEAD / 断言数 / active task）；子系统实现规格（按归属/域拆分到别处，不建大而全 `system.md`）。
- **去重规则**：与 `测试指南.md`（repo 根，编译/回归/部署/提交命令链）不重复 —— 用引用，不复制命令链正文。
- **维护 owner**：Captain / Integration Owner。

## 2. 环境与工具链事实（stable）

- 目标框架：.NET Framework 4.7.2；引用游戏程序集 `TaleWorlds.*`。
- 游戏环境：Bannerlord（本机 Steam 安装）；部署目标 `Modules\CombatBrain`。
- 代码仓 locator：repo 根 AGENTS.md / INDEX §0（project_id: CombatBrain；本机路径仅 locator）。

## 3. BannerlordSage 工具链（查官方 API / 本地源码）

- CLI 桥接器：`E:\MBmod\CombatAI\tools\bannerlord-sage-cli.js`；
  调用约定：`bun run "<桥接器路径>" <工具名> @<参数JSON文件>` —— 参数写 JSON 文件、路径用 `\\`（避免 PowerShell 引号问题）。
- 常用工具：
  - 官方源码：`search_source` / `read_csharp_type` / `read_file`；
  - 本地 Mod 源码：`mod_source_status` / `search_mod_source` / `read_mod_file` / `read_mod_type`（workspaceRoot 见 repo locator；本地已建索引）；
  - 自然语言入口：`search_bannerlord_knowledge`；
  - 诊断：`bannerlord_index_status` / `bannerlord_doctor`；
  - 项目记忆：`project_memory_wakeup` / `project_memory_search` / `project_memory_capture_session`。
- 官方反编译源码直读目录：BannerlordSage 发行目录 `dist/games/bannerlord/assets/Source`（可用 grep/read）。
- **版本匹配纪律**：游戏当前版本与本地反编译源码精确匹配；版本不匹配时**以本地反编译源码为准**。
- 辅助：`E:\MBmod\CombatAI\api_reflect.ps1` 可直接反射游戏 DLL 查真实 API 签名。

## 4. 运行期观测渠道（stable）

- 日志：`%TEMP%\CombatBrain.log`（意图切换 / 事件链 / `[Bench]` 性能报告等）。
- 遥测：`CombatBrain.telemetry.json`（战斗遥测计数器 + 异常检测）。
- 游戏内反馈：左下角叙事反馈（N 系列，观战/指挥双视角）。
- 无完整运行期 UI；观测以日志 + 画面间接观察为主。
- 观测/采样操作手册（如 CPS 采样流程）在 CPS 域文档，本文件不复制。

## 5. 稳定操作规则（与 测试指南.md 互补）

- 验证/部署命令链与顺序以 repo 根 `测试指南.md` 为准（本文件只引用，不复述）。
- 已知稳定约束：
  - 本机无全局 git `user.name` → 任何 commit 必须显式带身份；
  - 部署类验证仅在游戏进程退出后执行；
  - deploy 不拷 `ModuleData`（含 `opponent_priors.json`，见 BACKLOG BL-008）；
  - 部署以 `CombatBrain\deploy.ps1` 为准，源/部署 DLL 需 SHA256 一致校验（历史部署闸口径）。
