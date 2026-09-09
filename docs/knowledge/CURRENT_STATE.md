# CombatBrain — Current Verified State

> status: pilot
> authority: not-yet-exclusive
>
> 依据：CombatBrain Knowledge Architecture v1.1 — Phase A（§12–§18 CURRENT_STATE Contract）。
> 职责：描述当前 checkout / 当前主线**已验证**的工程状态。不是 Captain 日记、不是 task history、
> 不是 Master Spec、不是 team.json 镜像。

```yaml
project_id: CombatBrain
status_schema: 1
branch: main
worktree: E:\Agent\6a9268112aeaf49072bef052   # machine-local locator（非身份）
last_verified_commit: a879688237900af4f9706b4a0ca894229ca649c7
last_verified_at: 2026-09-08  # CONTEXT 更新日；git log 同日含该 HEAD
owner: captain / integration-owner（角色位，非 session）
state: WAITING_EXTERNAL       # 等 Astra 对 CPS3R Route A 复审
active_task: none             # 无进行中实现任务（有外部验证在途）
```

---

## Current Mainline

- **主线 = CPS（战场命令控制面收口）**：CPS-0 provenance 落地 → CPS-1 唯一命令仲裁 → CPS-2 双路线 PoC → CPS-3 运行期 A/B 验证 → **CPS-3R Route A 修复收口（现状）** → CPS-4 硬门清单 & GO/NO-GO。
- 分支 `main`，HEAD `a879688`（2026-09-08，「fix(cps): close Route A execution and lifecycle blockers」）。
- **功能冻结令生效中**：CPS GO 前禁新文化战术 / 新 Formation Mission / 新兵种角色 / 新播报 / 新主动移动 Glue / 新状态机 / 新直接 SetOrder 调用点。

## Current Phase

- **CPS-3R Route A 修复完成，提交 `a879688`，报告 `docs/cps3r-routea-final-blocker-report.md` 已出：READY FOR ASTRA RE-REVIEW（未部署 DLL）。**
- 本机部署类动作不得在此状态下执行（等待复审裁决后按命令链部署）。

## Active Task

- `none`（无进行中实现任务）。在途：**Astra 对 CPS3R Route A 的外部复审**。
- 任何 Agent 不得因「无 active task」推断「项目完成」——当前是 `WAITING_EXTERNAL`。

## Completed Major Milestones

- V1 骨架 / F1–F12 功能全部接入（感知/意图/LOD/格挡/素质/走位/求生/骑兵/远程/反制/士气/抖动）。
- 第二波意图升级 S1–S4 流水线（枚举层 8→12 维 / OpponentModel / ProtectGlue / BraceGlue）。
- P0 评审整改六项全修复（`11f66c7..9d818ec`）。
- **CPS-0 provenance**：SetOrderObserver 只读观测 + CommandSourceScope + 来源语义修正（902 PASS 实测）。
- **CPS-3R**：六硬门（`cps3r-route-a-hardgates` 1138→1144 PASS）、D-RouteA 运行期验证模式（1159）、MCM 下拉补 E（1165）、叙事阵营名修复（1174）、**Route A 最终阻断修复（a879688，1174+ 断言）**。
- 离线回归基线：**1174 PASS / 0 FAIL 实测**（2026-09-08 口径，持续只增）。

## Frozen / Deferred

- **功能冻结令（CPS GO 前）**：上述六类新功能一律禁止。
- `opponent_priors.json` 未随 deploy 拷贝 → 已 fallback 常量表（P1 遗留）。
- review-findings P1×4 / P2×6 未处理（截至 2026-09-01 清单口径，后续已有 CPS 相关新卡）。
- `_kb` 镜像修复：Phase A Quarantine 中，留待 Phase E 前修 sync（不删、不重建、不改 sync 脚本）。

## Known Blockers

1. **Astra re-review 未回**（CPS3R Route A，`a879688` 未部署 = 未最终验证）。
2. **`docs/` 目录未纳入 Git tracking**（规范断言已实测确认：`git ls-files docs` 为空）。Phase A 将新增 3 个 canonical 文件纳入 tracking，既有 CPS 报告是否入库待决策（不属 Phase A）。

## Known Engineering Debt

- 归因链路历史口径（OriginalUnknown=NativeTeamAI、35–80% 占比结论）已废弃，待 CPS-4 以 Observed/Inferred/Unknown 严格口径重建基线。
- CommandProvenance 的 `Enable()` 运行期启动调用点尚待 cps0-runtime-baseline 卡接入。
- 63 处 CONTEXT 引用、`_kb` sync 脚本、team.json 历史膨胀：均属后续阶段，Phase A 不碰。

## Latest Verification

- **证据等级：git log + CONTEXT（2026-09-08 更新）确认 HEAD=`a879688`，1174 PASS/0 FAIL 实测口径，报告 `docs/cps3r-routea-final-blocker-report.md`。**
- 范围说明：以上为「代码 + 离线断言」验证；**运行期游戏内验证（Route A 稳定/归因采样）依赖 Astra 复审 + 游戏内 E 模式采样，未完成**。
- OfflineTests 与运行期语义不同权：离线绿 ≠ 运行期绿（历史有 provenance/生命周期语义失败先例）。

## Evidence Pointers

- `docs/cps3r-routea-final-blocker-report.md`（Route A 收口报告，commit a879688 同源）
- `docs/cps4-hardgate-checklist-2026-09-08.md`（硬门清单）
- `docs/cps3r-routea-astra-review-submission-2026-09-08.md`（Astra 复审提交物）
- 测试链：《测试指南.md》（repo 根）— 编译 → 回归 → 部署 → 提交，顺序不得绕过

## Staleness Notes

- 本文件状态仅验证到 `a879688`。若 Git HEAD 前进至新 commit 且未完成新验证 → 本文件**不自动**把新 HEAD 当已验证状态；应回报 `state may be stale / repository advanced beyond last verified baseline`。
- 维护责任：Captain / Integration Owner 唯一直接维护；普通 Sub-agent 只能提建议与 evidence。
- 本文件由 MBmod manager 按 v1.1 Phase A 规范代建（2026-09-09），后续归属 Captain / Integration Owner。
