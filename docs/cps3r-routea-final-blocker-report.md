# cps3r-routea-final-blocker-repair 收工报告

> **历史快照（入库标注 2026-09-15）**：本件为 **2026-09-08** 时点的收工报告（当时收工 HEAD `ca775a8`、OfflineTests 1192、未部署 DLL）；**内容不随后续裁定更新**——其后经 r1/r2/r3 三轮返修与 Astra 2026-09-15 九项裁定，现基线为 `395fe13`（1297 PASS），权威见 `docs/knowledge/CURRENT_STATE.md`。

日期：2026-09-08　任务：cps3r-routea-final-blocker-repair（派单 E:\MBmod\CombatAI\CombatBrain CPS-3R Route A 最终阻断修复正式派单.md）

## 1. 动工/收工 HEAD
- 动工 HEAD：`5868a02`（叙事阵营名修复）
- 收工 HEAD：本任务独立 commit（见 git log 顶部）
- 工作树输入：Astra 未提交 3 文件（FormationCommandExecutor.cs +52/-13、FormationCommandPolicy.cs +6/-2、OfflineTests/Program.cs +11）——已吸收，未覆盖；未跟踪 docs/ 保留。

## 2. 修改文件清单
- CombatBrain/Ai/FormationCommandExecutor.cs（吸收 Astra CanExecuteSelected+TickOccasionally 落地；新增 RouteAOrderAdapter 唯一 setter 出口；ApplySingle 加 phase+返回 bool；Restore 记录 ExitRestore；证据链 per-identity 隔离；执行器防双写）
- CombatBrain/Coordination/FormationCommandPolicy.cs（吸收 Astra 冲突互斥三条边补全：Advance→Rejoin、Rejoin→Hold、Retreat→Rejoin）
- CombatBrain/Ai/CombatAiMissionLogic.cs（真实阶段/敌快照按 Team/MainBody 目标/步兵过滤/缓冲复用）
- OfflineTests/Program.cs（吸收 Astra +11 行；新增 TestCps3rRouteAFinalBlocker 15 断言）

## 3. Bannerlord v1.4.8 Behavior 调用链结论
Astra 核实并写入注释（FormationCommandExecutor.cs:843-845）：FindBestBehavior 会预计算尚未胜出的候选，CalculateCurrentOrder 必须无命令副作用；普通行为胜出后 FormationAI 调 ActiveBehavior.TickOccasionally 并直接 return。**结论：TickOccasionally 后同一 CB Behavior 不会产生第二次等价写入**（胜出后引擎直接应用 ActiveBehavior 的 CurrentOrder，无二次回调）。落地移到 TickOccasionally + RouteAOrderAdapter，CalculateCurrentOrder 仅设 CurrentOrder 字段。

## 4. Route A 授权边界与单一 Adapter 说明
- 授权边界：CombatBrain 产生 per-Formation 意图 → 原版 FormationAI 负责 Behavior 竞争/ActiveBehavior 选择 → 胜出 CB Behavior 的 TickOccasionally 进入 RouteAOrderAdapter.TryApply → Adapter 复核并执行唯一一次 Movement setter。
- 复核清单（CanExecuteSelected）：模式+RouteA 开关、普通单机野战（IsFieldBattle && !Siege && !SallyOut）、Formation 对象身份、ActiveBehavior 确为当前 CB Behavior（ReferenceEquals）、非玩家队四条件（PlayerOwner/HasPlayerControlledTroop/IsPlayerTroopInFormation/玩家队）、Formation 有效且 AI 控制、CommandSource.CombatBrainRouteA scope 包住唯一 setter。
- 复核失败 → RestoreIfArmed（恢复权重 0+清注册+记录 ExitRestore）。
- 执行器防双写：ApplySetOrder 对 Movement 域先查 CbRouteAPoC.IsMovementOwnedByRouteA(identity)，RouteA 活跃时跳过；Facing/Arrangement 仍按仲裁。

## 5. setter 静态扫描结果（全 Ai 目录实调用，非注释）
- FormationCommandExecutor.cs:345（Movement）、:349（Facing）、:352（Arrangement）——执行器 3 ✓
- FormationCommandExecutor.cs（RouteAOrderAdapter:489）——适配器 1 ✓
- 四 CB Behavior（CbHold/CbAdvance/CbRejoin/CbRetreat）：0 直接 setter（仅调 Adapter）✓
- 其他生产代码：0 直接 setter（其余 grep 命中均为注释）
- **精确满足派单目标「执行器 3 + 批准适配器 1 + 四 Behavior 0」**

## 6. OfflineTests 前后数量及结果
- 动工基线：1174 PASS / 0 FAIL
- 收工：1192 PASS / 0 FAIL（新增 18：Astra +3 方向性冲突回归 + t60 卡 15 断言）
- 基线只增不减；ALL PASS

## 7. Release 构建结果
- dotnet build CombatBrain.csproj -c Release：0 警告 0 错误
- OfflineTests（link 编译）：0 错误

## 8. 生命周期恢复矩阵（派单 §五 八类事件）
| 事件 | 恢复路径 | 验证 |
|---|---|---|
| 热关闭 RouteA/离开 E | ApplySingle routeOn=false → Restore | ShouldRestoreRouteA(false,true,true) ✓ |
| 玩家接管/玩家兵员 | CanExecuteSelected 玩家条件 false → RestoreIfArmed | ShouldRestoreRouteA(true,false,true) ✓ |
| 清空/失效/对象替换 | ApplySingle formationValid=false → Restore | ShouldRestoreRouteA(true,true,false) ✓ |
| Index 复用 | identity=对象哈希（非 Index） | 架构性 ✓ |
| 增援目标改变 | 每拍复核+非候选编队 Restore | 代码路径 ✓ |
| 非野战切换 | eligibleField=false → Restore | ✓ |
| Mission 结束 | ResetAll（接 Mission 生命周期） | ✓ |
| 初始化异常/复核失败 | TryApply 失败 → RestoreIfArmed | 代码路径 ✓ |
恢复与重复恢复幂等（_armed.Contains 短路）；新对象不继承旧状态（identity 隔离）；已销毁对象由 ResetAll 清空；清理路径不依赖候选遍历（ApplySingle 独立处理每编队）。

## 9. 证据链字段及重置规则（派单 §七）
- 事件链：Intent（RecordIntentApplied）→ BehaviorSelected（TickOccasionally 真实胜出）→ OrderApplied（Adapter 落地，per-identity RecordAppliedByIdentity）→ ExternalReplaced（SetOrderObserver ring 按 identity+Movement+Stage 过滤，保留 Seq/Time）→ ExitRestore（Restore 先记录）。
- 隔离：Mission session（ResetAll/Reset 清零）+ Formation identity（_appliedByIdentity 字典）+ CommandDomain（RouteA 仅 Movement）。
- Stable：StableByIdentity(identity) = AppliedByIdentity − ReplacedByIdentity（同 identity 同域同 session，禁减全局）。
- 日志可判断「RouteA 活跃时被覆盖 vs RouteA 退出后原版恢复」：ExitRestore 事件时间点 vs ExternalReplaced 事件时间点对比。
- 旧三场数据不得复用：新 Mission Reset 清零。

## 10. 已知限制与回滚点
- 已知限制：①RouteAOrderAdapter/CanExecuteSelected 依赖 TaleWorlds 类型，离线测试仅覆盖纯逻辑（IsRejoinRouteValid/ShouldRestoreRouteA/StableByIdentity 等）；②真实导航可达性 API 以 IsRejoinRouteValid（MainBody 有效+非贴敌）为近似，未做 GetNavMesh 逐点校验（公开 API 边界内）；③第一阶段仅 AI 步兵（QuerySystem.IsInfantryFormation 过滤）。
- 回滚点：git revert 本 commit；Astra 原始修改内容见 commit 说明（吸收未覆盖）。

## 11. 需重新执行的运行期测试（≤5 场）
1. E_RouteA 模式 + RouteAEnabled：单 Formation Route A 采样 1 场（验证 Adapter 落地+事件链四段+稳定计数）
2. 热关闭（战斗中关 RouteAEnabled）：验证真恢复（权重还原，日志 ExitRestore）
3. 玩家接管（选编队/下命令）：1 拍停 RouteA 不抢回
4. Rejoin 场景（编队远离主力）：Rejoin 目标=Army MainBody、非贴敌才挂载
5. 增援/拆分：权重重设后 RouteA 继续正确接管（幂等重挂载）
战斗后读 %TEMP%\CombatBrain.log 的 [RouteA] 汇总（intent/selected/orderApplied/stable/exitRestore/externalReplaced/sameTick/crossTick）。

## 12. 唯一结论
```
READY FOR ASTRA RE-REVIEW
```
（本卡仅修复 Astra 审计确认的 Route A BLOCKER；未部署 DLL；未把本卡完成写成条件 5 已通过。）
