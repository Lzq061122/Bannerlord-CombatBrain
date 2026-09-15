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
last_verified_commit: 395fe13ac4ec3df1b51832d4b3efda28bc075b0f   # 生产语义基线（r3）；Astra 2026-09-15 裁定推进
last_verified_at: 2026-09-15  # 离线 1297 PASS/0 FAIL（r3）；现网 DLL = r3 `395fe13`（`7E5FC25A…`，已由 09-15 部署的诊断构建回滚回本 DLL，src=dst）；诊断构建 a24e921 另记等价诊断证据（受限，见下）
owner: captain / integration-owner（角色位，非 session）
state: WAITING_EXTERNAL       # Astra 2026-09-15 裁定：证据链 PASS、条件 5 判 PASS；CB-REPAIR-0004 台账已关闭（COMPLETED）；CPS-4 仍 Pending、GO 不通过；等用户裁决 CPS-4/GO 与玩家豁免口径
active_task: none（收口执行清单 T1–T10 已完成：口径落修已提交、CB-REPAIR-0004 已关、覆盖恢复语义卡 CB-REPAIR-0007 置 DEFERRED）
```

---

## Current Mainline

- **主线 = CPS（战场命令控制面收口）**：CPS-0 provenance 落地 → CPS-1 唯一命令仲裁 → CPS-2 双路线 PoC → CPS-3 运行期 A/B 验证 → CPS-3R Route A 修复收口 + 覆盖失效返修（现状）→ CPS-4 硬门清单 & GO/NO-GO。
- 分支 `main`，**已验证代码基线 = `395fe13`（r3，生产语义）**——Astra 2026-09-15 裁定将验证基线推进至生产语义 `395fe13`（离线 1297 PASS/0 FAIL），其运行期验收按裁定 2/3 以「r3 的诊断衍生构建」样本计入；更早基线 `9a2afe4`（2026-09-11「fix(cps): Route A 覆盖语义裁决与 externalDuration 口径统一」）为上一已验基线。**现网 DLL = CB-REPAIR-0004 r3 `395fe13`**（2026-09-14 09:34 部署，src/dst SHA 一致，DLL SHA256 `7E5FC25A…`；2026-09-15 诊断构建 `a24e921` 采样后**已回滚回本 DLL**，回滚后 dst SHA = 备份 SHA，`Get-Process Bannerlord` 为空）；诊断构建 `a24e921` **另记等价诊断证据**（限定见裁定 2，不得称同一 DLL）。
- **功能冻结令生效中**：CPS GO 前禁新文化战术 / 新 Formation Mission / 新兵种角色 / 新播报 / 新主动移动 Glue / 新状态机 / 新直接 SetOrder 调用点。

## Current Phase

- **CPS-3R Route A 已修复并部署**（部署校验 src/dst SHA 一致，`Modules\CombatBrain`；游戏进程退出硬门通过；**验证基线以 `last_verified_commit` 为准 = 生产语义 `395fe13`，上一已验基线 `9a2afe4`**）。
- **Astra 2026-09-15 复审裁定：证据链 PASS**（探针 P1 已闭、无代码阻断）。九项裁定要点：①诊断构建样本**可计入** r3 运行期验收，限定为「**r3 的诊断衍生构建**」，绑定须含 `395fe13→a24e921` 差分 / DLL SHA / run·session / 同配置 / 1327/20/4=4 / 异常 0（**不得称同一 DLL、不得外推原生权重**）；②**S5 选①**——接受「离线证据 + 本轮机制观测」替代，**停止继续等正样本**，无需先取原生权重；③覆盖恢复语义另立设计决策卡且列 **DEFERRED**（冻结期不授权改码，不阻断本轮收口）；④**归因口径选 A**（只改文档口径：S5 归因改为以 `[RouteAEvent]` 的 `OrderApplied/SetterBegin/SetterCompleted` 自洽验 Route A，`[OrderTraceAgg]` 只验普通执行器与释放后窗口）；⑤**条件 5 本评审判 PASS**，但账面在文档落修前保持未通过，**不推出 CPS-4/GO**；⑥`CB-REPAIR-0004` **已具备关闭条件**，验证基线推进至生产语义 `395fe13`、`a24e921` 另记等价诊断证据，**CPS-4 保持 Pending**；⑦J-2 实现忠实、无更高规范硬冲突，「坏窗口」定义为「**本暂停期两次重取耗尽且无真实选中**」（不是"首次超时即退役"）；⑧玩家豁免列 GO 前产品口径裁决，`NavigationFailed`、J-3/J-4 列 **P2** 运行期/工程债，不阻断 0004。
- **运行期验证（可执行场景已全部完成）**：场景 1（E_RouteA 采样）重跑 **未复现**「被外部覆盖后长时间不重落地」缺陷（`ExternalReplaced` → 5.00s 内 `EdgeRetry` → `OrderApplied`）；场景 2（战斗中热关闭）**PASS**（`Exit(Disabled)` + `Restore`，`restoreFailures=0`，关闭后无重新武装）；场景 5（增援/清空）**PASS**（run `b0e2e0a4`：6 次 Exit 全部配对 Restore，含 3 次 `LostAIControl`、1 次 `EmptyOrInvalid`；`restoreFailures=0`、`weightFlap=0`；每次退出后重新 `OrderApplied`；`identity` 恒为 1 = 同一对象重挂载）。**场景 3、4 与场景 5 子项「拆分」在本次采样口径下不适用（§3a/§3b/§3c）；其中 Rejoin 成功路径已于 2026-09-14 取得 1 次运行期样本（§3j，`run=4156cc25` session=1），但 `NavigationFailed` 失败路径仍无样本——不得声称结构上永久不可达。** **注意**：场景 5 采样使用了游戏快进与低 BattleSize，响应延迟不构成证据。
- **Astra 离线回签（2026-09-14，历史块）**：CB-REPAIR-0004 r3（`395fe13`）**离线复审通过、允许进入受控部署采样**；该块当时记录「尚未取得运行期验收、条件 5 仍未满足、CPS-4/GO 不批准」——**运行期验收与条件 5 的状态已由 2026-09-15 复审更新（见上方 Current Phase 裁定块）**；**CPS-4/GO 仍不批准**（未变）。最终 GO 前仍须裁决**玩家豁免口径**（产品/规范事项）。非阻断待整理（P2）：重试事件 revision 配对；t12 源码不可重建。
- **CB-REPAIR-0004（占位不开火 → 周期重占位）离线已闭、台账已闭环（`COMPLETED`，2026-09-15）**：r1 `723b457` → 质检 needs_revision（R4-1）→ r2 `bc8449a` → **Astra 判 FAIL（P1）**（连续态势数值变化可在超时释放后下一拍重武装，占空比 ≈99.83%）→ **r3 `395fe13`**（重武装改为具名/离散/有界：意图真变化 + 10s 冷却 + 本暂停期授予 <2；可观测 `SuspensionRetry`/`suspensionRetries=`），**质检 t12 独立复核 = pass-with-runtime-debt**（真 r2/r3 生产构建双向复现：r2 三类变化 duty 98.75~99.88%、r3 acquires=1 且 40/100/150s 零重取、执行器 75~93% 连续窗口；CHURN 下 acquires 封顶 3）。派单 = `prompts/active/CombatBrain-CB-REPAIR-0004-r3-周期重占位返修派单-2026-09-14.md`（CB-PROMPT-0041）。**台账 `COMPLETED`（2026-09-15 关闭，`commit=238e31a5…`，证据 4 条全 PASS；Astra 裁定 7 认定具备关闭条件、用户授权关闭）**。
- **当前动作**：**收口执行清单已全部完成（T1–T10，2026-09-15）**——文档口径落修已提交、`CB-REPAIR-0004` 已由 `repair-close` 关闭（`repair-verify=ALL CONSISTENT`）、覆盖恢复语义卡 `CB-REPAIR-0007` 已置 `BLOCKED`/DEFERRED；**当前无 active task**，等用户裁决 CPS-4 / GO 与玩家豁免口径。任何进一步部署/回滚/产品代码改动仍须用户另行授权（冻结令生效中）。
- **诊断构建 `a24e921` 部署与采样（2026-09-15，已回滚）**：CB-REPAIR-0005 只读探针构建于 2026-09-15 部署（DLL `ADCC641E…`，src=dst MATCH）并完成采样（4 场 mission / 2 run、14 条 `[RouteASilence]`、窗口 44,497 行异常 0），随后**回滚回 r3 `395fe13`**（dst SHA = 备份 SHA `7E5FC25A…`）。该构建的样本经 Astra 裁定**可计入 r3 运行期验收**（限定为「r3 的诊断衍生构建」，绑定字段见 `docs/cps3r-routea-diagnostic-sampling-2026-09-15.md`）。
- **r3 运行期样本（累计七场：§3e–§3j，其中 §3j 为一个 run 含两场，2026-09-14）**：
  - 样本①`run=c81c4f5e…`（§3e）：武装后引擎全程未选中 → **恰好 10.00s `Exit(SelectionTimeout)`+`Restore`**，余场 304s **零再武装** → S1 **PASS（强）**、S3 **PASS**、S4 间接 PASS、S5 部分、S2 未触发。
  - 样本②`run=6223ad9b…`（§3f，`agents=601`、`BattleDuration=505.51s`、异常 0）：健康路径**强 PASS**——`applied=9`/`stableSoFar=9`/`selected=746`/`lastSelectedAt=493.531s`/`weightFlap=0`/`restoreFailures=0`；`Exit`×6（`LostAIControl`×5 + `EmptyOrInvalid`×1）**全部配对 `Restore`** 且每次资格恢复后重新武装；意图真变化链 Hold→Advance→Retreat→Hold **逐次落地**；S4 **直接 PASS**。**S2、S5-edgeRetry 当时无样本**。
  - 样本③`run=ce970979…`（§3g）：短样本（武装 1s 后 `LostAIControl` 释放，全场未再武装），`restoreFailures=0`，无新验收点。
  - 样本④`run=d43c302e…`（§3h，**1501 人 / 1365.66s 长局**）：**S2 补齐 PASS**——3 次 `SuspensionRetry` 各对应一次具名意图真变化、每次合格事件只授予一次；**S1 进一步加强**（`t=685→1179` **494 秒**零再武装，同期 94 次外部写）；**S3 PASS ×4**（四次 `SelectionTimeout` 均在武装/重取后 10.000–10.006s 触发并配对 `Restore`）；**冷却门禁与预算上界均获实机确认**。
  - 样本⑤`run=0496f7b6…`（§3i，**1426 人 / 3809.58s 超长局**、异常 0、武装对象 `f=1`）：**S5 负样本**——`t=1115.09 ExternalReplaced` 真实发生，但 `EdgeRetry`=0（覆盖后 Behavior 再未被选中，`lastSelectedAt=1114.587`），9.5s 后按期限 `Exit(SelectionTimeout)`+`Restore` → **S5 正向形态还需"覆盖后 10s 内仍被选中"**；**S1 再加强**（`1171.8→2067.7` **895.9s** 零再武装）；S3 ×3（Δ≈10.0s 全配对）；63.5 分钟 `weightFlap=0`/`restoreFailures=0`/异常 0。日志已归档：`MBmod/CombatAI/logs/CombatBrain-2026-09-14-full.log`（SHA256 `63ECC4A9…`）与切片 `run-0496f7b6-20260914-longbattle.log`（`585DCEAD…`）。
- **条件 5：Astra 2026-09-15 评审判 PASS，本文件已按该裁定登记；但 CPS-4 与 GO 仍不通过**（裁定 6：条件 5 的 PASS 不推出 CPS-4/GO；CPS-4 保持 Pending）。
- **样本⑥`run=4156cc25…`（§3j，2026-09-14 15:35–16:00，一个 run 含两场：session=1 混编 635v616 非申报场、session=2 申报场 1016v994 纯步/射各半、`agents=2032`、无快进、异常 0）**：**Rejoin 分支首次取得运行期样本**（session=1 部署期 `SupportMainEffort` → `Intent(Rejoin)` → `OrderApplied(ot=1)`，`NavigationFailed=0`，仅成功路径）；session=2 **S5 负样本再现**（`t=325.529 ExternalReplaced` → 覆盖后再未被选中、`edgeRetries=0` → `t=335.035 Exit(SelectionTimeout)`+`Restore`，Δ=9.507s）；**S1 再次 PASS**（超时后 213.7s 零再武装）、**S3 PASS**（Δ=10.005s 自末次选中）、**S4 直接 PASS**（188 次连续 `ExternalAfterExit` 跨 213.7s）；session=1 健康路径 `applied=11/stableSoFar=11`、`Exit`×8 全配对 `Restore`。**唯一遗留缺口 = S5 正向形态，已连续两场未取得**；该口径已由 Astra 2026-09-15 裁定收口（裁定 6/7，选①）。
- **样本⑦`run=4e885a4e…`（§3k，2026-09-14 20:49–20:55，混编 635v616、`agents=2032`、`BattleDuration=347.43s`、无快进、异常 0）——S5 负样本第 3 次（最强）**：覆盖（`t=220.528 ExternalReplaced`）发生于**每 0.5s 连续选中的健康局**中（覆盖前 379 次选中 / 15 次落地）；覆盖后 **127.3s 零选中至战斗结束**，期间 CB **两次重取**（`SuspensionRetry`×2，各 10s 窗口）引擎仍未回调 → `edgeRetries=0`。**S2 PASS（第 2 样本**：两次重取各对应一次具名意图变化、冷却内与达上限后不授予）、**S1 PASS**（连续变号零重取）、**S3 PASS ×3**、**S4 PASS**（释放后 `ExternalAfterExit`×190 / 总跨度 117.2s，**内含 CB 两次重新持有所有权，实得三段 ≈10.0s/30.7s/56.5s ≈97.2s**）、健康路径 `applied=15/stableSoFar=14`、`Exit`×6 全配对 `Restore`、`weightFlap=0`。**口径更正（Astra P1 复审）**：① 上文 117.2s 不是单一连续放行窗口；② 覆盖后外部写 58 次**分布非均匀**（含 11.0s 与 31.0s 间隔，最大 31.0s），来源仍是 `ExternalUnknown`，"原生行为胜出"是**待探针确认的推断**，不得写成已证明。**S5 正向形态现为 3/3 全负**（§3i/§3j/§3k）；**该口径的判定权已于 2026-09-15 由 Astra 裁定收口（裁定 6/7：S5 选①，接受离线证据 + 本轮机制观测，停止继续等正样本；条件 5 判 PASS）——不再"待裁决"**，样本⑧（诊断构建 `a24e921`）经裁定 2 可计入 r3 运行期验收（限定为「r3 的诊断衍生构建」）。

- **样本⑧ = 诊断构建 `a24e921` 运行期采样（`docs/cps3r-routea-diagnostic-sampling-2026-09-15.md`，2026-09-15 14:37–16:04，4 场 mission / 2 run、1949–2032 agents、无快进、窗口 44,497 行异常 0）——机制问题已由只读探针确立**：3 次 `ExternalReplaced` 覆盖，`[RouteASilence]` 共 **14 条**采样拍**全部**为 `activeIdx=0` / `activeType=BehaviorCharge` / `aiSame=1` / `aiCtl=1` / Route A 侧权重恰一个 `10.00` ⇒ **覆盖后引擎胜出者是原生 charge 行为并保持整个 10s 交付窗口**，CB 行为不再被回调（`edgeRetries=0`），由交付期限释放（M4 Δ=9.502s 自覆盖 / 10.002s 自末次选中）。⇒ **r3 类构建上的覆盖事件累计 6 次、S5 正向形态 0 次**（r3 前构建曾有 1 次，`ba84a11b` Δ=5.00s）。**本结论替换样本⑦里"原生行为胜出"的 Inferred 表述。** 限定：样本来自**诊断构建**（≠ r3 DLL，"能否计入 r3 运行期验收"由 Astra 认定，汇报件 §5-7）；单窗口未选中 ≠ 永久不可达。

## Active Task

- **CB-REPAIR-0004 r3：受控部署 + 运行期采样 —— 台账已于 2026-09-15 关闭（`COMPLETED`）**（`commit=238e31a56dd89f637d057499defb1cd6c0fc34a2`、`commit_root=repo`、证据 4 条全 PASS、`review_state=PASSED`；用户 2026-09-15 授权关闭，Astra 裁定 7 认定具备关闭条件）。采样验收点：①连续变号不重取（`suspensionRetries`=0）②合格意图变化仅一次 `SuspensionRetry` ③零选中后按期限 `SelectionTimeout` + `Restore` ④普通 Movement 持续恢复 ⑤健康 `EdgeRetry`、归因（按裁定 5 口径 A）与 `RestoreFailures=0`。**关闭不等于 GO**：CPS-4 保持 Pending、GO 不通过。
- **CB-REPAIR-0005：Route A「覆盖后断流」只读诊断探针 —— 已 COMPLETED**（`a6a4ec5`，`commit_root=repo`；派单 `CB-PROMPT-0043`、交付报告 `CB-PROMPT-0044`；`source_type=maintenance`，按策略无需独立 review）。**实现 + 离线验收完成**：Release 0 错 0 警；OfflineTests **1320 PASS / 0 FAIL**（r3 基线 1297，+23）；`RouteAStaticChecks` 20 项 PASS 且生产 setter 站点 **4=4 无新增**。落地面 = 新增只读行 `[RouteASilence]`（覆盖后每 ≥1s 一拍、上限 10 拍 + 超时释放前一拍），用于判定「`ExternalReplaced` 后引擎胜出者是否仍是 Route A 行为」，把该 Inferred 结论变为可判定。**部署与采样（T1 落修）**：该探针已于 2026-09-15 作为**诊断构建 `a24e921`** 部署（r3 `395fe13` + 只读探针 + r1 输出边界隔离；DLL `ADCC641E…`，src=dst MATCH）并完成运行期采样（4 场 mission / 2 run、14 条 `[RouteASilence]`、窗口 44,497 行异常 0，见 `docs/cps3r-routea-diagnostic-sampling-2026-09-15.md`），随后**已回滚回 r3 `395fe13`**（现网 DLL = `7E5FC25A…`，dst SHA = 备份 SHA）。其 r1 尾修（输出边界隔离）另立 `CB-REPAIR-0006`（`a24e921`，已 COMPLETED）。
- **样本⑦见 §3k**（2026-09-14 20:49–20:55，`run=4e885a4e`）：S5 负样本第 3 次（最强），S2 第二样本 PASS；**S5 正向形态现为 3/3 全负**；该口径已由 Astra 2026-09-15 裁定收口（裁定 6/7，不再待裁决）。
- 任何 Agent 不得因「无 active task」推断「项目完成」——当前是 `WAITING_EXTERNAL`；CB-REPAIR-0004 台账已关闭（`COMPLETED`，2026-09-15）但**项目未完成**：条件 5 虽判 PASS，**CPS-4 仍 Pending、GO 不通过**，且 open 项仍有 P2 三条 + 玩家豁免（GO 前产品口径）+ `CB-REPAIR-0007`（覆盖恢复语义重设计，`BLOCKED`/DEFERRED）；CB-REPAIR-0005 诊断探针**已实现、已部署为诊断构建 `a24e921`、已采样、已回滚回 r3**。

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
- 部署：**现网 DLL = r3 `395fe13`（`7E5FC25A…`，src=dst SHA 一致；2026-09-14 09:34 首次部署；2026-09-15 诊断构建 `a24e921` 采样后已回滚回本 DLL）**；更早部署为上一基线 `9a2afe4`（src/dst 一致）。诊断构建样本按 Astra 裁定 2 计入运行期验收（限定为「r3 的诊断衍生构建」）。

## Frozen / Deferred

- **功能冻结令（CPS GO 前）**：上述六类新功能一律禁止。
- `opponent_priors.json` 未随 deploy 拷贝 → 已 fallback 常量表（P1 遗留）。
- review-findings P1×4 / P2×6 未处理（截至 2026-09-01 清单口径，后续已有 CPS 相关新卡）。
- `_kb` 镜像修复：Phase A Quarantine 中，留待 Phase E 前修 sync（不删、不重建、不改 sync 脚本）。
- **收口期独立审计残留（低危，可读性类，Captain 2026-09-15 裁定不阻塞、不在本轮修改）**：①`docs/cps3r-routea-astra-rereview-brief-2026-09-14.md` 的 §5（提问口径）与 §0e（裁定回填）分置两处，属有意历史留存；②P2 存在两套编号（§0e 的 Astra 三条 / 本档的 P2-1…4），已互相锚定；③`MBmod/CombatAI/tools/cps3r-routea-p2-debt-consolidation-2026-09-15.md` 的措辞返修未落（该档属已复核证据，按纪律不改正文）。三项均不支撑任何结论；明细见 `MBmod/CombatAI/tools/astra-ruling-landing-inventory-2026-09-15.md` §4b 与 `…closure-stale-statement-audit-2026-09-15.md`。

## Known Blockers

1. ~~Astra re-review 未回~~ → **已回签（2026-09-15）：证据链 PASS、九项裁定已锁定，见 Current Phase 与裁定台账**。按命令链推进 CPS-4 **仍不批准**（CPS-4 Pending、GO 不通过）——回签不等于 GO。
2. **条件 5：Astra 2026-09-15 评审判 PASS，本文件已按裁定登记（裁定 6）。但该 PASS 不推出 CPS-4/GO——CPS-4 保持 Pending、GO 不通过。** Route A 的可执行运行期场景：场景 1、2、5 **PASS**；场景 3（玩家接管）与场景 5 子项（拆分）在**采样口径下不适用**；场景 4（Rejoin）**成功路径已取得运行期样本**（§3j），`NavigationFailed` 失败路径仍无样本（列 P2 运行期债，不得声称结构上永久不可达）；**S5 正向形态按裁定 6/7 选①（接受离线证据 + 本轮机制观测），停止继续等正样本**。详见 `docs/cps3r-routea-runtime-scenarios-2026-09-13.md`。
3. **CB-REPAIR-0004：已闭环（`COMPLETED`，2026-09-15）**：占位不开火 → r2 被 Astra 判 FAIL/P1（周期重占位，占空比 ≈99.83%）→ **r3 `395fe13` 已消除该机制并经质检 t12 双向独立复现判否（r3 acquires=1、40/100/150s 零重取、执行器 75~93% 连续窗口）**，verdict = pass-with-runtime-debt。**Astra 2026-09-15 裁定具备关闭条件（裁定 7）后，用户授权关闭**：`repair-close` 已执行（`commit=238e31a56dd89f637d057499defb1cd6c0fc34a2`、证据 4 条 PASS、`repair-verify=ALL CONSISTENT`）；验证基线 = 生产语义 `395fe13`；运行期样本 = r3 类构建覆盖事件累计 6 次、S5 正向形态 0 次，按裁定 2/3 认定。**现网 DLL = r3 `395fe13`（`7E5FC25A…`）。**
4. **路线 A「本场退役」口径（已裁决，J-2；Astra 2026-09-15 复核为忠实实现）**：r3 的预算重置只在「持所有权时真实选中／非超时真实释放／`BeginSession`」发生 ⇒ **预算（≤2 次重取）耗尽后本场不可自愈**（CHURN 实测 acquires 封顶 3）。用户 2026-09-14 **裁决接受**：一场内出现一次坏窗口 = **本场 Route A 退役**；Astra 2026-09-15 确认其实现忠实、无更高规范硬冲突，并把「坏窗口」明确为「**本暂停期两次重取耗尽且无真实选中**」（**不是**"首次超时即退役"）；已写入 `docs/cps4-hardgate-checklist-2026-09-08.md`（2026-09-14 更新节 + 2026-09-15 裁定节）。判定含义：采样中"该场后续无 Route A 活动"属既定语义，不得当作新缺陷；自愈机制另立卡设计（当前 DEFERRED，见裁定 4）。
5. **口径分歧（属产品/规范裁决项，未裁决、未改代码）**：Route A 的玩家队排除**不受** MCM 开关 `PlayerTeamExemptionEnabled` 约束，与其它 7 个模块口径不一致；当前仅作 Descriptive 记录，冻结令下不擅自收敛。**Astra 2026-09-15 裁定：列为 GO 前的产品口径裁决项**（不阻断 CB-REPAIR-0004）。
6. ~~`docs/` 目录未纳入 Git tracking~~ → 已解决：`AGENTS.md` + `docs/knowledge/` 三个 canonical 文件已纳入 tracking（commit `62502e9`、`d5743dd` 及 Traffic Cutover commit）。既有 CPS 报告是否入库仍待决策（不属本 cutover）。

## Known Engineering Debt

- 归因链路历史口径（OriginalUnknown=NativeTeamAI、35–80% 占比结论）已废弃，待 CPS-4 以 Observed/Inferred/Unknown 严格口径重建基线。
- CommandProvenance 的 `Enable()` 运行期启动调用点尚待 cps0-runtime-baseline 卡接入。
- **Route A 归因渠道缺环（2026-09-14 观察；Astra 2026-09-15 裁定采用方案 A：只改文档口径、不改码）**：`RouteAOrderAdapter` 直接 `SetMovementOrder`（仅 `CommandSourceScope`）而不调用 `CommandProvenance.Instance.Record`（全仓唯一调用点 `FormationCommandExecutor.cs:147`）⇒ Route A 的写入不进 5s `[OrderTrace]` 与终局 `[OrderTraceAgg]` 聚合（`CombatBrainRouteA=0/o0`，4 场复现），其归因只存在于 `[RouteAEvent] source=CombatBrainRouteA` 渠道（36 条 = 12 次落地×3，自洽）。**裁定落修口径**：S5 归因改为以 `[RouteAEvent]` 的 `OrderApplied/SetterBegin/SetterCompleted` **自洽**验 Route A；`[OrderTraceAgg]` 只用于验普通执行器与释放后窗口（不用于 Route A 落地计数）。冻结令下不接线。
- **P2（不阻断 CB-REPAIR-0004，Astra 2026-09-15 列项）**：①重试事件 revision 配对；②t12 源码不可重建；③引擎侧「为何停止选中」的候选解释（原生 `BehaviorCharge` 权重高于 Route A 行为上限）仍为 **Inferred 未取证**（探针只报 `ActiveBehavior` 身份与 CB 侧权重，不报原生权重）；④`NavigationFailed` 失败路径与 J-3（证据表满时计数不一致）、J-4（超时暂停依赖先清后设次序）为运行期/工程债，均不阻断 0004 关闭。
- 63 处 CONTEXT 引用、`_kb` sync 脚本、team.json 历史膨胀：均属后续阶段，Phase A 不碰。

## Latest Verification

- **证据等级：Astra 2026-09-15 复审裁定（证据链 PASS）+ git log + 已闭环返修卡质量门（t2/t4/t6）+ CB-REPAIR-0004 的 r3 独立复核（t12，双向复现）+ 部署校验 + 运行期场景 1/2/5 实测 + 诊断构建 `a24e921` 运行期采样（14 条 `[RouteASilence]`、4 场 mission、异常 0）。**
- **Astra 2026-09-15 裁定的运行期验收认定（T2 落修）+ T10 关闭留痕**：验证基线 = **生产语义 `395fe13`**（离线 1297 PASS / 0 FAIL）；诊断构建 `a24e921` = **等价诊断证据**（离线 1327 PASS / 0 FAIL、`RouteAStaticChecks` 20 项 PASS、生产 setter 站点 4=4 无新增、采样窗口异常 0；`run=14ff27b0` s1–s3 与 `run=6e2a87d2` s1、1949–2032 agents、无快进），**限定为「r3 的诊断衍生构建」样本**——不得称同一 DLL、不得据此外推原生权重。**T10 台账关闭证据（4 条 PASS）与 commit 记于 `prompts/repairs/registry.json` 的 `CB-REPAIR-0004` 条目：`commit=238e31a56dd89f637d057499defb1cd6c0fc34a2`、`commit_root=repo`、`closed_at=2026-09-15T18:53:04+08:00`、`repair-verify=ALL CONSISTENT`（REPAIRS=7 / JOURNAL=26）。**
- 离线：Release 0 错 0 警；**上一基线 `9a2afe4` = 1241 PASS / 0 FAIL**；**现基线 r3 `395fe13`（现已部署）= 1297 PASS / 0 FAIL**（Check 站点 1100→1120，`RouteAStaticChecks.ps1` 实跑 20 条 PASS）；静态 setter 去 trivia 4=4 无新增调用点。
- 运行期：场景 1 重跑未复现覆盖失效缺陷；场景 2 热关闭 `Exit(Disabled)`+`Restore` PASS；场景 5 增援/清空 6 次 Exit 全部配对 Restore、每次重新接管、`restoreFailures=0` PASS（含快进采样，延迟不作证据）。明细见 `docs/cps3r-routea-runtime-scenarios-2026-09-13.md`。
- 范围说明：场景 3 与「拆分」为**采样不适用**；Rejoin **成功路径**已取得运行期样本（§3j）、失败路径仍无样本；未取得样本的分支：新 Formation 对象身份隔离、Rejoin/`NavigationFailed`、**S5 正向形态（`ExternalReplaced → EdgeRetry → OrderApplied`；r3 类构建累计 6 次覆盖、0 次正向，已由裁定 6/7 选①收口）**、F-2 的 Release 分支；F-5、H-2 无运行期证据；**CB-REPAIR-0004 = 离线已闭（r3 + t12 独立复核）+ 运行期验收已按裁定 2/3 认定 + 台账已闭环（`COMPLETED`，2026-09-15 关闭）**。R4-1 与 F-3/F-4 已随 r2/r3 闭合（见运行场景档 §3d）；J-1（开发侧反向验证产物未按契约登记，按未验证记账、风险已由更强证据闭合）、J-3/J-4（low 未修，列 P2）记录在案。
- OfflineTests 与运行期语义不同权：离线绿 ≠ 运行期绿（历史有 provenance/生命周期语义失败先例）。

## Evidence Pointers

- `docs/cps3r-routea-diagnostic-sampling-2026-09-15.md`（诊断构建 `a24e921` 运行期采样：探针行全量、绑定表、归档 SHA；**Astra 裁定 2 的等价性绑定（差分包）见其 §8**）
- `MBmod/CombatAI/tools/cps3r-routea-attribution-gap-report-2026-09-15.md`（归因渠道缺环调查报告）+ `…-attribution-verification-r2-…`（独立复核）、`MBmod/CombatAI/tools/cps3r-routea-p2-debt-consolidation-2026-09-15.md` + `…-consolidated-verification-r3-…`（P2 债务合并与复核）——**归因口径 A 已采纳（Astra 2026-09-15），落点在采样表 §2 的 S5 行、`docs/cps4-hardgate-checklist-2026-09-08.md` 的 2026-09-15 更新节、与本节状态档；该两份已复核报告正文不改（其 `:146/:147/:149/:158/:172` 的「待裁/需裁决」表述以本处口径覆盖，属历史证据）。**
- `MBmod/CombatAI/tools/CB-REPAIR-0006-部署前核对-2026-09-15.md`（§7 部署执行、§8 采样与回滚）
- `docs/cps3r-routea-runtime-scenarios-2026-09-13.md`（Route A 运行期场景记录：场景 1、2、5 **PASS**；场景 3 与「拆分」采样不适用；Rejoin 成功路径见 **§3j**、失败路径仍无样本；§3c 的 CB-REPAIR-0004 根因与轮次、**§3d 的 r1→r2→r3 SHA 对应与 t12 双向独立复现**、§3e–§3k r3 运行期样本、**§3l 诊断构建采样（机制已由观测确立）**；含 run id、判定命令与体检工具 `MBmod/CombatAI/tools/routea-rejoin-check.ps1`）
- `MBmod/CombatAI/prompts/active/CombatBrain-CB-REPAIR-0004-r3-周期重占位返修派单-2026-09-14.md`（r3 派单，registry CB-PROMPT-0041）
- `docs/cps3r-routea-final-blocker-report.md`（Route A 收口报告，commit a879688 同源）
- `docs/cps4-hardgate-checklist-2026-09-08.md`（硬门清单）
- `docs/cps3r-routea-astra-review-submission-2026-09-08.md`（Astra 复审提交物）
- `MBmod/CombatAI/prompts/repairs/registry.json`（CB-REPAIR-0001/0002/0003/**0004**/0005/0006 均 `COMPLETED`；**0007 = 覆盖恢复语义重新设计，`BLOCKED`/DEFERRED（Astra 裁定 4，冻结期不改码）**）
- 测试链：《测试指南.md》（repo 根）— 编译 → 回归 → 部署 → 提交，顺序不得绕过；§0a 为 E 模式采样前置（ObserverEnabled 硬约束）

## Staleness Notes

- 本文件状态验证到代码基线 **`395fe13`（Astra 2026-09-15 裁定后的生产语义基线）**。**注：本节不逐提交跟踪 HEAD**——2026-09-15 收口期间 HEAD 多次前移（`30e299e`/`238e31a`/`66c72e6`/`399bce4`/后续文档提交），均为**文档与状态类**提交、不含新产品代码语义；核对时以 `git log` 与本节口径为准，不把更新的 HEAD 当已验证代码状态。若 Git HEAD 前进且含**产品代码**语义变更而未完成新验证 → 本文件**不自动**把新 HEAD 当已验证状态；应回报 `state may be stale / repository advanced beyond last verified baseline`。`a24e921` 为诊断衍生构建（已在裁定 2 的限定下记为等价诊断证据）；CB-REPAIR-0004 r1 `723b457`／r2 `bc8449a` 既未部署也未运行期验证，不得当作已验证状态；其中 r2 的早期同标题提交 `6f632ae` 已被 amend 取代、不在祖先链。
- 运行期部分覆盖场景 1、2、5 与采样不适用项（场景 3 及「拆分」；Rejoin 成功路径已取样、失败路径未验证）；场景 5 采样含游戏快进与低 BattleSize，其数字不得与实时场次混比；诊断构建样本仅在裁定 2 的限定下计入；任何「Route A 运行期已验证」的表述都必须同时限定场次、构建与采样条件。
- 维护责任：Captain / Integration Owner 唯一直接维护；普通 Sub-agent 只能提建议与 evidence。
- 本文件由 MBmod manager 按 v1.1 Phase A 规范代建（2026-09-09），后续归属 Captain / Integration Owner。
