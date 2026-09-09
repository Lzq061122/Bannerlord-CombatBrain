# CombatBrain Tooling — Stable Technical & Operator Facts

> status: active
> authority: canonical-bootstrap (Phase C2 建立)
> 依据：CombatBrain Knowledge Architecture — Phase C2。
> 角色契约见下。正文（BannerlordSage 用法、CONTEXT §6/§8 稳定操作事实）在 C3 迁入；C2 仅契约 + 现成稳定事实。

---

## 1. Role Contract

- **Authority axis**：DESCRIPTIVE（技术/工具链事实域）。
- **职责**：承载稳定技术/工具链事实与操作流程（工具用法、环境约束、稳定验证/部署程序）。
- **必须不含**：动态值（HEAD / 断言数 / active task）；子系统实现规格（按归属/域拆分到别处，不建大而全 `system.md`）。
- **去重规则**：与 `测试指南.md`（repo 根，编译/回归/部署/提交命令链）不重复 —— 用引用，不复制命令链正文。
- **维护 owner**：Captain / Integration Owner。

## 2. 环境与工具链事实（stable，随迁入逐步增补）

- 目标框架：.NET Framework 4.7.2；引用游戏程序集 `TaleWorlds.*`。
- 游戏环境：Bannerlord（本机 Steam 安装）；部署目标 `Modules\CombatBrain`。
- 工具链：BannerlordSage CLI（桥接 `bannerlord-sage-cli.js`）查官方 API / 本地反编译源；`api_reflect.ps1` 辅助。
- 官方反编译源位置：按 BannerlordSage 发行目录（`dist/games/bannerlord/assets/Source`）约定，版本需与游戏版本匹配。

## 3. 稳定操作规则（与 测试指南.md 互补；正文在 C3 自 legacy CONTEXT §6/§8 迁入）

- 验证/部署顺序纪律以 `测试指南.md` 为准（本文件只引用，不复述）。
- 已知稳定约束（迁入后补全）：
  - 本机无全局 git `user.name` → 任何 commit 必须显式带身份；
  - 部署类验证仅在游戏进程退出后执行；
  - 部署不拷 `ModuleData`（历史已知约束，C3 核实现状后补细节）。

## 4. Planned Sections（C3 迁入占位）

1. BannerlordSage 完整用法与常用工具清单；
2. 命令/验证链操作程序（指向 `测试指南.md`）；
3. 运行期观测渠道与采样操作手册（legacy CONTEXT §4/CPS 用户操作指引）。
