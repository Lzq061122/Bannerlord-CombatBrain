# CB-REPAIR-0004 r3 受控部署与运行期采样记录（2026-09-14）

> 依据：Astra 2026-09-14 离线回签「离线 PASS、允许进入受控部署采样」。
> 状态：`395fe13` **允许部署测试**；**尚未取得运行期验收**；台账 CB-REPAIR-0004 保持 `IN_PROGRESS`、`review_state=PASSED`（离线通过）。
> 约束：**条件 5 仍未满足；CPS-4/GO 仍不批准；当前无必须继续修改的阻断代码**；最终 GO 前仍须裁决玩家豁免口径。
> 本文件是本次采样的**唯一绑定表**：跑完把 run id / 日志交回 Captain 登记；未登记 `run/session` 的采样不算证据。

---

## 1. 绑定信息（进游戏前须完整）

| 项 | 值 |
|---|---|
| 代码 commit | **`395fe13`**（CB-REPAIR-0004 r3） |
| 部署时 repo HEAD | `ae533ec`（仅含 `395fe13` 之后的文档提交，无代码差异） |
| 仓库构建产物 SHA256 | `7E5FC25A0EF0559D718B0D5E42994141846291501E3342F94E6C0B1F713FD8D6` |
| 部署目标 DLL SHA256 | **`7E5FC25A0EF0559D718B0D5E42994141846291501E3342F94E6C0B1F713FD8D6`**（= src，MATCH=True） |
| 部署时间 / 游戏进程 | **2026-09-14**（deploy.ps1 EXIT=0，ModuleData 已同步）；部署前硬门：`Get-Process Bannerlord` 为空 ✓ |
| MCM 配置 | `Cps3Mode = E_RouteA`；**`Cps3ObserverEnabled = true`**（G-1 硬约束：无观测 Route A 不执行）；RouteA 专用开关按 E 模式自动武装 |
| 战斗配置 | 自定义战斗、**200v200 步兵野战**；**BattleSize 建议恢复为 6**；**本轮不要用游戏快进**（前一轮快进使响应延迟不可作证据） |
| run / session | **待填**（`[RouteA] run=<32位id>`；一场一文件时改名前先归档日志） |

**采样前操作**：① 确认无 `Bannerlord` 进程后再部署；② 部署后核对 src/dst SHA 一致；③ 开新战斗前 `Move-Item $env:TEMP\CombatBrain.log "$env:TEMP\CombatBrain.runN.log"` 隔离场次（避免多场混写）；④ 首查 `13:xx:xx [SetOrderObserver] 观测已挂载 prefixes=3` 确认观测在位。

## 2. 本轮采样验收点（Astra 回签要求的最小集）

| # | 验收点 | 判定方法 | 通过标准 |
|---|---|---|---|
| S1 | **连续变号不重取** | 零 `BehaviorSelected` + 每拍态势变化（自然局中体现为被武装编队长期不被选中） | `suspensionRetries=0`；超时释放后**长时间不重新武装**（不再每 10s 重取） |
| S2 | **合格意图变化仅一次 `SuspensionRetry`** | `event=SuspensionRetry` 计数 | 出现**恰好一次**，且伴随可见的意图变化；不得出现第 2 次以上周期重取 |
| S3 | **零选中后按期限 `SelectionTimeout` + `Restore`** | `event=Exit reason=SelectionTimeout` 与随后 `event=Restore` | `ExternalReplaced`（或被选中停止）后 **≤10s** 出现；`Restore` 紧随、`restoreFailures=0` |
| S4 | **普通 Movement 持续恢复** | 该编队执行器写入 | 释放后普通执行器获得**持续**（非单帧）Movement 写窗口，之后原版正常接管（`ExternalAfterExit`） |
| S5 | **健康路径与归因** | `[RouteA]` 汇总 + `[OrderTraceAgg]` | 正常选中场景 `edgeRetries` 语义正确（=授权次数）；`OrderTraceAgg` 归因与执行器一致；`restoreFailures=0`、无异常行 |

**判定命令**：

```powershell
# 场次关键事件
Select-String $env:TEMP\CombatBrain.log -Pattern 'run=<RUN_ID>' |
  Where-Object { $_.Line -match 'event=(OrderApplied|ExternalReplaced|EdgeRetry|SuspensionRetry|Exit|Restore)\b' }
# 汇总（new: suspensionRetries / lastSelectedAt）
Select-String $env:TEMP\CombatBrain.log -Pattern '\[RouteA\] run=' | Select-Object -Last 3
# Route A 相关实时盯屏
Get-Content $env:TEMP\CombatBrain.log -Wait -Tail 200 | Select-String -Pattern '\[RouteA'
```

## 3. 采样后动作（Captain）

1. 记录 `run/session`、MCM 与战斗配置、日志归档路径到本表；
2. 在 `docs/cps3r-routea-runtime-scenarios-2026-09-13.md` 新增 §3e：本场逐项（S1–S5）判定与证据；
3. 更新 `docs/knowledge/CURRENT_STATE.md`：运行期证据、条件 5 状态（**仍不判 GO**，由 Astra/用户裁）；
4. 台账 CB-REPAIR-0004 仍保持 `IN_PROGRESS`——**只有运行期验收齐备 + Astra 复核通过才可 `repair-close`**。

## 4. 非阻断待整理（P2，Astra 标注）

- P2-1：**重试事件 revision 配对**（`SuspensionRetry` 事件与 revision 的配对关系待整理）；
- P2-2：**t12 源码不可重建**（质检 harness 的 r3 侧源码抽取/重建方式待整理）。

两项**不阻断本轮采样**，在后续证据口径整理中处理（不得借此在本轮改代码）。
