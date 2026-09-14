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
last_verified_at: 2026-09-13  # 离线 1241 PASS/0 FAIL + 已部署（现网 DLL）；此后代码提交均未部署
owner: captain / integration-owner（角色位，非 session）
state: WAITING_EXTERNAL       # 等 Astra 复审；场景 1、2、5 已 PASS；场景 3、4 与「拆分」本次采样不适用（Rejoin 仍未验证）
active_task: CB-REPAIR-0004（r3 返修中，台账 IN_PROGRESS；不部署、不采样）
```

---

## Current Mainline

- **主线 = CPS（战场命令控制面收口）**：CPS-0 provenance 落地 → CPS-1 唯一命令仲裁 → CPS-2 双路线 PoC → CPS-3 运行期 A/B 验证 → CPS-3R Route A 修复收口 + 覆盖失效返修（现状）→ CPS-4 硬门清单 & GO/NO-GO。
- 分支 `main`，**已验证且已部署的代码基线 `9a2afe4`**（2026-09-11「fix(cps): Route A 覆盖语义裁决与 externalDuration 口径统一」）；**HEAD 已在其之后包含未部署的 Route A 代码提交**（CB-REPAIR-0004：`723b457`、`bc8449a`，及 r3 在途）——这些提交**未经运行期验证，也不在现网 DLL 中**。
- **功能冻结令生效中**：CPS GO 前禁新文化战术 / 新 Formation Mission / 新兵种角色 / 新播报 / 新主动移动 Glue / 新状态机 / 新直接 SetOrder 调用点。

## Current Phase

- **CPS-3R Route A 已修复并部署**（代码基线 `9a2afe4`；部署校验 src/dst SHA 一致，`Modules\CombatBrain`；游戏进程退出硬门通过）。
- **运行期验证（可执行场景已全部完成）**：场景 1（E_RouteA 采样）重跑 **未复现**「被外部覆盖后长时间不重落地」缺陷（`ExternalReplaced` → 5.00s 内 `EdgeRetry` → `OrderApplied`）；场景 2（战斗中热关闭）**PASS**（`Exit(Disabled)` + `Restore`，`restoreFailures=0`，关闭后无重新武装）；场景 5（增援/清空）**PASS**（run `b0e2e0a4`：6 次 Exit 全部配对 Restore，含 3 次 `LostAIControl`、1 次 `EmptyOrInvalid`；`restoreFailures=0`、`weightFlap=0`；每次退出后重新 `OrderApplied`；`identity` 恒为 1 = 同一对象重挂载）。**场景 3、4 与场景 5 子项「拆分」在本次采样口径下不适用（§3a/§3b/§3c）；其中 Rejoin 分支仍未验证，不得声称结构上永久不可达。** **注意**：场景 5 采样使用了游戏快进与低 BattleSize，响应延迟不构成证据。
- **CB-REPAIR-0004（占位不开火 → 周期重占位）已在离线层面闭环，但卡未关、未部署**：r1 `723b457` → 质检 needs_revision（R4-1）→ r2 `bc8449a` → **Astra 独立复审判 FAIL（P1）**（连续态势数值变化可在超时释放后下一拍重武装，占空比 ≈99.83%）→ **r3 `395fe13`**（重武装改为具名/离散/有界：意图真变化 + 10s 冷却 + 本暂停期授予 <2；可观测 `SuspensionRetry`/`suspensionRetries=`），**质检 t12 独立复核 = pass-with-runtime-debt**（以真 r2/r3 生产构建双向复现：r2 三类变化 duty 98.75~99.88%、r3 acquires=1 且 40/100/150s 零重取、执行器 75~93% 连续窗口；CHURN 下 acquires 封顶 3）。派单 = `prompts/active/CombatBrain-CB-REPAIR-0004-r3-周期重占位返修派单-2026-09-14.md`（CB-PROMPT-0041）。**台账保持 `IN_PROGRESS`**：须 Astra 复审通过 + 部署/运行期证据齐备才可关。
- **条件 5 仍未满足**：不判 GO（判定权在 Astra 复审与用户）。

## Active Task

- **CB-REPAIR-0004（r3）返修进行中**：占位不开火/周期重占位；台账 `IN_PROGRESS`，派单 CB-PROMPT-0041；不部署、不做游戏内采样。在途外部：**Astra 对 CPS3R Route A 的复审**。
- 任何 Agent 不得因「无 active task」推断「项目完成」——当前是 `WAITING_EXTERNAL`，且有一张返修卡未闭环。

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
2. **条件 5 未满足（判定权在 Astra 与用户）**：Route A 的可执行运行期场景已全部完成——场景 1、2、5 **PASS**；场景 3（玩家接管）、场景 4（Rejoin）、场景 5 子项（拆分）在**本次采样口径下不适用**（**Rejoin 分支仍未验证**，不得声称结构上永久不可达）。本文件不据此判 GO。详见 `docs/cps3r-routea-runtime-scenarios-2026-09-13.md`。
3. **CB-REPAIR-0004 尚未闭环（离线已闭、卡未关、未部署）**：占位不开火 → r2 被 Astra 判 FAIL/P1（周期重占位，占空比 ≈99.83%）；**r3 `395fe13` 已消除该机制并经质检 t12 双向独立复现判否（r3 acquires=1、40/100/150s 零重取、执行器 75~93% 连续窗口）**，verdict = pass-with-runtime-debt。台账仍 `IN_PROGRESS`（须 Astra 复审通过 + 部署/运行期证据齐备才可关）；**新提交均未部署**（现网 DLL 仍为 `9a2afe4` 基线）。该卡闭环前不得推进条件 5。
4. **路线 A「本场退役」口径（已裁决，J-2）**：r3 的预算重置只在「持所有权时真实选中／非超时真实释放／`BeginSession`」发生 ⇒ **预算（≤2 次重取）耗尽后本场不可自愈**（CHURN 实测 acquires 封顶 3）。用户 2026-09-14 **裁决接受**：一场内出现一次坏窗口 = **本场 Route A 退役**；已写入 `docs/cps4-hardgate-checklist-2026-09-08.md`（2026-09-14 更新节）并保留在交 Astra 的问题清单。判定含义：采样中"该场后续无 Route A 活动"属既定语义，不得当作新缺陷；自愈机制如需另立卡设计。
5. **口径分歧（属产品/规范裁决项，未裁决、未改代码）**：Route A 的玩家队排除**不受** MCM 开关 `PlayerTeamExemptionEnabled` 约束，与其它 7 个模块口径不一致；当前仅作 Descriptive 记录，冻结令下不擅自收敛。
6. ~~`docs/` 目录未纳入 Git tracking~~ → 已解决：`AGENTS.md` + `docs/knowledge/` 三个 canonical 文件已纳入 tracking（commit `62502e9`、`d5743dd` 及 Traffic Cutover commit）。既有 CPS 报告是否入库仍待决策（不属本 cutover）。

## Known Engineering Debt

- 归因链路历史口径（OriginalUnknown=NativeTeamAI、35–80% 占比结论）已废弃，待 CPS-4 以 Observed/Inferred/Unknown 严格口径重建基线。
- CommandProvenance 的 `Enable()` 运行期启动调用点尚待 cps0-runtime-baseline 卡接入。
- 63 处 CONTEXT 引用、`_kb` sync 脚本、team.json 历史膨胀：均属后续阶段，Phase A 不碰。

## Latest Verification

- **证据等级：git log + 已闭环返修卡质量门（t2/t4/t6）+ CB-REPAIR-0004 的 r3 独立复核（t12，双向复现）+ 部署校验 + 运行期场景 1/2/5 实测。**
- 离线：Release 0 错 0 警；**现网基线 `9a2afe4` = 1241 PASS / 0 FAIL**；**未部署的 r3 `395fe13` = 1297 PASS / 0 FAIL**（Check 站点 1100→1120，`RouteAStaticChecks.ps1` 实跑 20 条 PASS）；静态 setter 去 trivia 4=4 无新增调用点。
- 运行期：场景 1 重跑未复现覆盖失效缺陷；场景 2 热关闭 `Exit(Disabled)`+`Restore` PASS；场景 5 增援/清空 6 次 Exit 全部配对 Restore、每次重新接管、`restoreFailures=0` PASS（含快进采样，延迟不作证据）。明细见 `docs/cps3r-routea-runtime-scenarios-2026-09-13.md`。
- 范围说明：场景 3、4 与「拆分」为**本次采样不适用**（Rejoin 仍未验证）；未取得样本的分支：新 Formation 对象身份隔离、Rejoin/`NavigationFailed`、F-2 的 Release 分支；F-5、H-2 无运行期证据；**CB-REPAIR-0004 = 离线已闭（r3 + t12 独立复核）、卡未关、未部署**。R4-1 与 F-3/F-4 已随 r2/r3 闭合（见运行场景档 §3d）；J-1（开发侧反向验证产物未按契约登记，按未验证记账、风险已由更强证据闭合）、J-3/J-4（low 未修）记录在案。
- OfflineTests 与运行期语义不同权：离线绿 ≠ 运行期绿（历史有 provenance/生命周期语义失败先例）。

## Evidence Pointers

- `docs/cps3r-routea-runtime-scenarios-2026-09-13.md`（Route A 运行期场景记录：场景 1、2、5 **PASS**；场景 3、4 与「拆分」本次采样不适用（Rejoin 仍未验证）；§3c 的 CB-REPAIR-0004 根因与轮次、**§3d 的 r1→r2→r3 SHA 对应与 t12 双向独立复现**；含 run id、判定命令与体检工具 `MBmod/CombatAI/tools/routea-rejoin-check.ps1`）
- `MBmod/CombatAI/prompts/active/CombatBrain-CB-REPAIR-0004-r3-周期重占位返修派单-2026-09-14.md`（r3 派单，registry CB-PROMPT-0041）
- `docs/cps3r-routea-final-blocker-report.md`（Route A 收口报告，commit a879688 同源）
- `docs/cps4-hardgate-checklist-2026-09-08.md`（硬门清单）
- `docs/cps3r-routea-astra-review-submission-2026-09-08.md`（Astra 复审提交物）
- `MBmod/CombatAI/prompts/repairs/registry.json`（CB-REPAIR-0001/0002/0003 台账，均 COMPLETED）
- 测试链：《测试指南.md》（repo 根）— 编译 → 回归 → 部署 → 提交，顺序不得绕过；§0a 为 E 模式采样前置（ObserverEnabled 硬约束）

## Staleness Notes

- 本文件状态验证到代码基线 `9a2afe4`。若 Git HEAD 前进至新 commit 且未完成新验证 → 本文件**不自动**把新 HEAD 当已验证状态；应回报 `state may be stale / repository advanced beyond last verified baseline`。**当前 HEAD（`395fe13`）已超出该基线且含未部署的 Route A 代码提交（CB-REPAIR-0004 r1 `723b457`／r2 `bc8449a`／r3 `395fe13`）——这些提交既未部署也未运行期验证，不得当作已验证状态；其中 r2 的早期同标题提交 `6f632ae` 已被 amend 取代、不在祖先链。**
- 运行期部分覆盖场景 1、2、5 与本次采样不适用项（场景 3、4 及「拆分」；Rejoin 仍未验证）；场景 5 采样含游戏快进与低 BattleSize，其数字不得与实时场次混比；任何「Route A 运行期已验证」的表述都必须同时限定场次与采样条件。
- 维护责任：Captain / Integration Owner 唯一直接维护；普通 Sub-agent 只能提建议与 evidence。
- 本文件由 MBmod manager 按 v1.1 Phase A 规范代建（2026-09-09），后续归属 Captain / Integration Owner。
