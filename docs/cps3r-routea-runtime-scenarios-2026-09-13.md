# CPS-3R Route A 运行期验证记录（2026-09-13）

> 性质：**DESCRIPTIVE scoped claims**（运行期证据）。不改写 Normative；离线绿 ≠ 运行期绿。
> 现行验证态唯一权威 = `docs/knowledge/CURRENT_STATE.md`；本文件只登记**已完成场次**的证据与其限定。
> 关联返修：CB-REPAIR-0001 / CB-REPAIR-0002 / CB-REPAIR-0003（commit `70c9a11` / `a219c5b` / `9a2afe4`，均已 COMPLETED）。
> 部署树：`9a2afe4` 代码（`f6634f7` 仅 `测试指南.md` 文档，无代码变更）。

---

## 1. 采样前置

- MCM：`Cps3Mode=E_RouteA` + **`Cps3ObserverEnabled=true`**（G-1 硬约束：`RouteAExecutionEnabled()` 含 `&& ObserverEnabled`，CB-REPAIR-0003；关观测时 Route A 不执行且零 `[RouteA]` 行）
- 日志：`%TEMP%\CombatBrain.log`，**多场混写**，须按 `run=` 过滤
- 字段命名：汇总字段为 **`applied=`**（Astra 提交物 §11 写作 `orderApplied=`，属文档/代码命名漂移）；逐事件行为 **`event=OrderApplied`**（无空格）
- 汇总节奏：每 5 秒一条，值不变时重复打印同一行

## 2. 场景 1（E_RouteA 采样，200v200 步兵野战）：重跑 PASS（对照修复前 FAIL）

| 项 | 修复前 `run=4c6212a1…`（09-10 13:13，修复前 DLL） | 修复后 `run=ba84a11b…`（09-10 13:53，含 CB-REPAIR-0001/0002） |
|---|---|---|
| 观测挂载 | —（旧构建未含 G-1 约束） | 13:53:12 `[SetOrderObserver] 观测已挂载 prefixes=3` |
| 落地序列 | `applied=3` | 6× `OrderApplied`：t=4.53 Hold / 152.53 Advance / 175.03 Hold / 179.03 Advance / 192.03 Hold / 198.03 Advance |
| 中途外部覆盖 | t=410.53 `ExternalReplaced`（外部 orderType=5） | **t=245.53 `ExternalReplaced`（外部 orderType=5）** |
| 覆盖后行为 | **直到 t=452.89 战斗结束未再落地**（缺陷） | **t=250.54 `EdgeRetry` → `OrderApplied`（5.00s 内重落地一次）** |
| 退出 | t=452.89 `Exit(EmptyOrInvalid)` + `Restore` | t=265.76 `Exit(EmptyOrInvalid)` + `Restore` |
| 汇总 | `applied=3 replaced=1 stableSoFar=2`（**无** externalWhileActive/edgeRetries/externalDuration 字段 = 修复前构建） | `applied=7 replaced=1 externalWhileActive=12 edgeRetries=1 externalDuration=247.913s stableSoFar=6 unverified=0 dropped=0 intentChange=5 weightFlap=0 restoreFailures=0` |

判定：**原缺陷（被外部覆盖后长时间不重新落地）未复现**；「首次 `replaced=true` → 单次边沿重试」路径获得运行期样本；新证据字段在运行期真实产出。

## 3. 场景 2（战斗中热关闭 RouteA）：PASS

`run=a408fc92…`（09-13 01:11–01:17，战斗时长 362.85s）

- 01:11:07 `[SetOrderObserver] 观测已挂载 prefixes=3`（G-1 前置满足）
- RouteA 执行：6× `OrderApplied`（t=4.53 Hold / 152.53 Advance / 175.03 Hold / 179.03 Advance / 192.03 Hold / 198.03 Advance），409× `BehaviorSelected`
- 用户于战斗中主动关闭：**t=208.9818（全场 58% 处）`event=Exit reason=Disabled` + `event=Restore reason=Disabled`**
- 关闭后：344× `ExternalAfterExit`（RouteA 已退出，原版正常写入），**无任何 `OrderApplied`**（未重新武装、未抢回）
- 汇总：`applied=6 replaced=0 externalWhileActive=3 edgeRetries=0 externalDuration=0.000s stableSoFar=6 unverified=0 dropped=0 intentChange=5 weightFlap=0 restoreFailures=0`

判定：热关闭 → 立即 `Exit(Disabled)` + 权重恢复，`restoreFailures=0`；关闭后所有权彻底释放，原版接管正常。**通过**。

限定：该场 3 次 `externalWhileActive` 全部落在同一 tick（t=4.390940），故 `externalDuration=0.000s`；本场 `replaced=0`，即**未发生外部覆盖**，不能用于验收覆盖恢复路径。

## 4. 本轮未证明（运行期债，不得据本文件判 GO）

1. 场景 3（玩家接管）/ 场景 4（Rejoin）/ 场景 5（增援·编队清空·拆分）**未跑**。
2. F-2 语义分支「重试已用后再次 `replaced=true` → 立即 `Release(ExternalOverridePersisted)`」**无运行期样本**（两场 `replaced` 分别为 1 / 0，未出现第二次 `replaced=true`）。
3. `replaced=false` 路径（不释放、不消耗重试、仅计数）只被观测为计数（`externalWhileActive=3` / `12`），其对所有权与抖动的影响无运行期证据。
4. F-5（Release 位于 Harmony postfix 栈内改权重）无运行期证据；H-2（E 模式帧成本须按「RouteA + 观测」复合口径评定）未评定。
5. CPS §14.1 **条件 5 仍未满足**；Astra 复审未回签。

## 5. 复现命令

```powershell
# 按场次过滤关键事件
Select-String $env:TEMP\CombatBrain.log -Pattern 'run=<RUN_ID>' |
  Where-Object { $_.Line -match 'event=(Intent|OrderApplied|ExternalReplaced|ExternalWhileActive|EdgeRetry|Exit|Restore)\b' }

# 只看汇总（字段名 applied=）
Select-String $env:TEMP\CombatBrain.log -Pattern '\[RouteA\] run=' | Select-Object -Last 3

# 实时只看 RouteA 行
Get-Content $env:TEMP\CombatBrain.log -Wait -Tail 200 | Select-String -Pattern '\[RouteA'
```

## 6. 登记状态

- 已登记场次：场景 1（重跑）、场景 2。
- 待跑场次：场景 3 / 4 / 5；跑完后本文件续写并按 `docs/evidence/INDEX.md` §5 更新指针。
