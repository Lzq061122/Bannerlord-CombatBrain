# CombatBrain — Current Verified State

> status: active
> authority: canonical-bootstrap
>
> 依据：CombatBrain Knowledge Architecture v1.1 — Traffic Cutover（Phase B 验证通过）。
> 职责：描述当前 checkout / 当前主线**已验证**的工程状态。不是 Captain 日记、不是 task history、
> 不是 Master Spec、不是 team.json 镜像。

```yaml
project_id: CombatBrain
status_schema: 1
branch: main
worktree: E:\Agent\6a9268112aeaf49072bef052   # machine-local locator（非身份）
last_verified_commit: 9a2afe4bdded41e1ace602382e462aa1e7b5687d
last_verified_at: 2026-09-13  # 离线 1241 PASS/0 FAIL + 已部署 + Route A 运行期场景 1 重跑 / 场景 2 通过
owner: captain / integration-owner（角色位，非 session）
state: WAITING_EXTERNAL       # 等 Astra 对 CPS3R Route A 复审；运行期场景 1、2 已完成，场景 3 改判 N/A，4、5 待跑
active_task: none             # 无进行中实现任务（三张返修卡已闭环）
```

---

## Current Mainline

- **主线 = CPS（战场命令控制面收口）**：CPS-0 provenance 落地 → CPS-1 唯一命令仲裁 → CPS-2 双路线 PoC → CPS-3 运行期 A/B 验证 → CPS-3R Route A 修复收口 + 覆盖失效返修（现状）→ CPS-4 硬门清单 & GO/NO-GO。
- 分支 `main`，**已验证代码基线 `9a2afe4`**（2026-09-11「fix(cps): Route A 覆盖语义裁决与 externalDuration 口径统一」）；当前 HEAD 在此之后仅有文档提交（`f6634f7` 测试指南 §0a）。
- **功能冻结令生效中**：CPS GO 前禁新文化战术 / 新 Formation Mission / 新兵种角色 / 新播报 / 新主动移动 Glue / 新状态机 / 新直接 SetOrder 调用点。

## Current Phase

- **CPS-3R Route A 已修复并部署**（代码基线 `9a2afe4`；部署校验 src/dst SHA 一致，`Modules\CombatBrain`；游戏进程退出硬门通过）。
- **运行期验证进行中**：场景 1（E_RouteA 采样）重跑 **未复现**「被外部覆盖后长时间不重落地」缺陷（`ExternalReplaced` → 5.00s 内 `EdgeRetry` → `OrderApplied`）；场景 2（战斗中热关闭）**通过**（`Exit(Disabled)` + `Restore`，`restoreFailures=0`，关闭后无重新武装）。**场景 3（玩家接管）改判 N/A**：Route A 对玩家队无条件豁免（`CombatAiMissionLogic.cs:2387`、`RouteAOrderAdapter.cs:141-142`），玩家无法对非玩家队编队下令，该动作结构上不可达 → 替代路径与新增验收点见运行期证据文件 §3a。场景 4–5 待跑。
- **条件 5 仍未满足**：不得据已跑场景判 CPS GO；Astra 复审未回签。

## Active Task

- `none`（无进行中实现任务）。在途：**Astra 对 CPS3R Route A 的外部复审**。
- 任何 Agent 不得因「无 active task」推断「项目完成」——当前是 `WAITING_EXTERNAL`。

## Completed Major Milestones

- V1 骨架 / F1–F12 功能全部接入（感知/意图/LOD/格挡/素质/走位/求生/骑兵/远程/反制/士气/抖动）。
- 第二波意图升级 S1–S4 流水线（枚举层 8→12 维 / OpponentModel / ProtectGlue / BraceGlue）。
- P0 评审整改六项全修复（`11f66c7..9d818ec`）。
- **CPS-0 provenance**：SetOrderObserver 只读观测 + CommandSourceScope + 来源语义修正（902 PASS 实测）。
- **CPS-3R**：六硬门（`cps3r-route-a-hardgates` 1138→1144 PASS）、D-RouteA 运行期验证模式（1159）、MCM 下拉补 E（1165）、叙事阵营名修复（1174）、Route A 最终阻断修复（`a879688`）。
- **CB-REPAIR-0001**（`70c9a11`）：外部覆盖通知 RouteAControl + 单次边沿重试 + 连续覆盖立即 Release + 汇总新增 `externalWhileActive/edgeRetries/externalDuration`。
- **CB-REPAIR-0002**（`a219c5b`）：`externalDuration` 口径统一为「最后一次外部覆盖时间结算」；F-2 覆盖释放语义裁决并在测试中锁定。
- **CB-REPAIR-0003**（`9a2afe4`）：`RouteAExecutionEnabled()` 增加 `&& ObserverEnabled` 硬约束（E 模式无观测不执行）。
- 离线回归基线：**1241 PASS / 0 FAIL 实测**（2026-09-13 口径，只增不减；历史 1174 为过期口径）。
- 部署：代码基线 `9a2afe4` 已部署，src/dst SHA 一致。

## Frozen / Deferred

- **功能冻结令（CPS GO 前）**：上述六类新功能一律禁止。
- `opponent_priors.json` 未随 deploy 拷贝 → 已 fallback 常量表（P1 遗留）。
- review-findings P1×4 / P2×6 未处理（截至 2026-09-01 清单口径，后续已有 CPS 相关新卡）。
- `_kb` 镜像修复：Phase A Quarantine 中，留待 Phase E 前修 sync（不删、不重建、不改 sync 脚本）。

## Known Blockers

1. **Astra re-review 未回**（CPS3R Route A：修复已部署，等外部复审回签后方可按命令链推进 CPS-4）。
2. **条件 5 未满足**：Route A 运行期验证仅完成场景 1、2；场景 3 经代码核实**不可执行**（Route A 玩家队无条件豁免）已改判 N/A，场景 4、5 待跑（见 `docs/cps3r-routea-runtime-scenarios-2026-09-13.md`）。不得据部分场景判 GO。
3. **口径分歧（待裁决，未改代码）**：Route A 的玩家队排除**不受** MCM 开关 `PlayerTeamExemptionEnabled` 约束，与其它 7 个模块口径不一致；当前仅作 Descriptive 记录，冻结令下不擅自收敛。
2. ~~`docs/` 目录未纳入 Git tracking~~ → 已解决：`AGENTS.md` + `docs/knowledge/` 三个 canonical 文件已纳入 tracking（commit `62502e9`、`d5743dd` 及 Traffic Cutover commit）。既有 CPS 报告是否入库仍待决策（不属本 cutover）。

## Known Engineering Debt

- 归因链路历史口径（OriginalUnknown=NativeTeamAI、35–80% 占比结论）已废弃，待 CPS-4 以 Observed/Inferred/Unknown 严格口径重建基线。
- CommandProvenance 的 `Enable()` 运行期启动调用点尚待 cps0-runtime-baseline 卡接入。
- 63 处 CONTEXT 引用、`_kb` sync 脚本、team.json 历史膨胀：均属后续阶段，Phase A 不碰。

## Latest Verification

- **证据等级：git log + 三张返修卡质量门（t2/t4/t6）+ 部署校验 + 运行期场景 1/2 实测。**
- 离线：Release 0 错 0 警；**OfflineTests 1241 PASS / 0 FAIL**；静态 setter 扫描 4=4 无新增调用点（代码基线 `9a2afe4`）。
- 运行期：场景 1 重跑未复现覆盖失效缺陷；场景 2 热关闭 `Exit(Disabled)`+`Restore` 通过。明细见 `docs/cps3r-routea-runtime-scenarios-2026-09-13.md`。
- 范围说明：**场景 3–5 未跑**；F-2「重试后再次覆盖→立即 Release」分支、F-5（Harmony postfix 栈内改权重）、H-2（RouteA+观测复合帧成本）均**无运行期证据**。
- OfflineTests 与运行期语义不同权：离线绿 ≠ 运行期绿（历史有 provenance/生命周期语义失败先例）。

## Evidence Pointers

- `docs/cps3r-routea-runtime-scenarios-2026-09-13.md`（Route A 运行期场景记录：场景 1 重跑 / 场景 2 通过，含 run id 与判定命令）
- `docs/cps3r-routea-final-blocker-report.md`（Route A 收口报告，commit a879688 同源）
- `docs/cps4-hardgate-checklist-2026-09-08.md`（硬门清单）
- `docs/cps3r-routea-astra-review-submission-2026-09-08.md`（Astra 复审提交物）
- `MBmod/CombatAI/prompts/repairs/registry.json`（CB-REPAIR-0001/0002/0003 台账，均 COMPLETED）
- 测试链：《测试指南.md》（repo 根）— 编译 → 回归 → 部署 → 提交，顺序不得绕过；§0a 为 E 模式采样前置（ObserverEnabled 硬约束）

## Staleness Notes

- 本文件状态验证到代码基线 `9a2afe4`。若 Git HEAD 前进至新 commit 且未完成新验证 → 本文件**不自动**把新 HEAD 当已验证状态；应回报 `state may be stale / repository advanced beyond last verified baseline`。该基线之后的 HEAD 均为文档提交，代码内容与 `9a2afe4` 一致。
- 运行期部分同样只覆盖已跑场次（场景 1、2）与已裁决项（场景 3 = N/A）；场景 4、5 跑完前，任何「Route A 运行期已验证」的表述都必须限定场次范围。
- 维护责任：Captain / Integration Owner 唯一直接维护；普通 Sub-agent 只能提建议与 evidence。
- 本文件由 MBmod manager 按 v1.1 Phase A 规范代建（2026-09-09），后续归属 Captain / Integration Owner。
