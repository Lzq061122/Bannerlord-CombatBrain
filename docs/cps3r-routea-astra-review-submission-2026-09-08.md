# CombatBrain cps3r-routea-final-blocker 复审提交

> **历史快照（入库标注 2026-09-15）**：本件为 **2026-09-08** 时点交 Astra 的复审提交摘要（当时结论 READY FOR ASTRA RE-REVIEW、未部署 DLL）；**内容不随后续裁定更新**。
> 该轮复审与其后的 r3 采样、Astra 2026-09-15 九项裁定见 `docs/cps3r-routea-astra-rereview-brief-2026-09-14.md`（§0b/§0d/**§0e**）与 `docs/knowledge/CURRENT_STATE.md`。

> 致 Astra 复审：本文件为 cps3r-routea-final-blocker-repair 任务的复审提交摘要。完整证据见收工报告（路径见 §4）。唯一结论：**READY FOR ASTRA RE-REVIEW**。

## 1. 任务身份

- 任务代号：`cps3r-routea-final-blocker-repair`
- 项目：CombatBrain（骑砍2 v1.4.8 C# AI Mod），仓库 `E:\Agent\6a9268112aeaf49072bef052`
- 基准 HEAD：`5868a02` → 收工 HEAD：`ca775a8`
- 工作树：干净（Astra 3 文件未提交修改已吸收进 commit；未跟踪 `docs/` 保留）

## 2. 改动内容（四大 P0 + P1）

| P0 | 内容 | 落点 |
|---|---|---|
| ① 唯一写入 | RouteAOrderAdapter.TryApply 集中四 Behavior setter；CanExecuteSelected 落地前全量复核（模式/开关/野战/Formation identity/ActiveBehavior ReferenceEquals/玩家四条件/有效 AI 控制/命令目标有效/Movement 域拥有）；CommandSource.CombatBrainRouteA scope 包唯一真实 setter；执行器 ApplySetOrder 对 RouteA Movement 域防双写 | FormationCommandExecutor.cs |
| ② 生命周期可逆 | Formation 身份注册表；八类事件恢复幂等（热关闭/离开 E/玩家接管/清空替换/Index 复用/增援目标改变/非野战切换/Mission 结束/初始化异常/Adapter 复核失败）；Restore 先 RecordExitRestore；ResetAll 接 Mission 生命周期（session 清零）；ApplySingle 返 bool（Rejoin 失败不注册） | FormationCommandExecutor.cs |
| ③ Rejoin 语义 | 敌快照按受控 Formation 所属 Team（IsEnemyOf(team)，禁固定玩家队）；真实战斗阶段 TeamState.Phase（禁固定 Deployment）；Rejoin 目标=Army.MainBody（禁 Team 中心）；IsRejoinRouteValid 导航校验失败→不应用+撤销权重+恢复降权；Astra 三条冲突互斥保留 | FormationCommandExecutor.cs / FormationCommandPolicy.cs |
| ④ 证据链 | 五段严格区分（Intent/BehaviorSelected/OrderApplied/ExternalReplaced/ExitRestore）；计数按 Mission session+Formation identity+CommandDomain 隔离；新 Mission 清零；Stable=同 identity 同域同 session Applied−Replaced（禁减全局）；ExternalReplaced 保留 setter Seq/Time；Exit/Restore 先记录再允许后续原版 setter 解释为正常恢复；旧三场数据未重新包装 | FormationCommandExecutor.cs / CommandProvenance.cs |

P1：仅 AI 步兵（IsRouteAInfantry）+ 预算 1 + 不每帧 new（_enemySnapsBuffer/_cache 复用）+ 5s 汇总仅诊断构造 + weightFlap 关联真实意图状态（无态势变化 2s 内 A→B→A 才算振荡）。

## 3. 验证证据

- OfflineTests：**1174 → 1192 PASS/0 FAIL**（+18：Astra +3 + t60 新增 15），基线只增不减
- Release build：0 错 0 警
- **静态 setter 扫描（grep 实测）**：全 Ai 目录精确 = 执行器 3（:353/:357/:360）+ RouteAOrderAdapter 1（:497）+ 四 CB Behavior 0（仅调 TryApply）
- **未部署 DLL**（派单 §十禁止部署游戏 DLL，本卡未执行 deploy.ps1）
- v1.4.8 调用链结论（Astra 核实 + 本卡确认）：FindBestBehavior 预计算候选时 CalculateCurrentOrder 无命令副作用；胜出后 ActiveBehavior.TickOccasionally 直接 return——无二次等价写入（注释 :843-845）

## 4. 收工报告

`E:\Agent\6a9268112aeaf49072bef052\docs\cps3r-routea-final-blocker-report.md`（12 项齐全）：
1. 动工/收工 HEAD 2. 修改文件清单 3. v1.4.8 Behavior 调用链结论 4. RouteA 授权边界+单一 Adapter 说明 5. setter 静态扫描结果 6. OfflineTests 前后数量 7. Release build 结果 8. 生命周期恢复矩阵 9. 证据链字段与重置规则 10. 已知限制与回滚点 11. 需重跑运行期测试清单（≤5 场）12. 唯一结论

## 5. 待复审点（请 Astra 逐条核验）

1. 四大 P0 是否符合派单 §四-§九（唯一写入边界/生命周期可逆矩阵/Rejoin 语义/证据链口径）
2. 静态收口是否精确（执行器 3 + Adapter 1 + Behavior 0，无旁路）
3. 证据链是否正确区分「RouteA 活跃时被覆盖」vs「RouteA 退出后原版恢复」
4. v1.4.8 调用链结论是否成立（TickOccasionally 后无二次等价写入）
5. 收工结论 READY FOR ASTRA RE-REVIEW 是否成立（未把本卡完成写成条件 5 已通过）

## 6. 复审通过后的运行期测试（报告 §11，需用户进游戏，≤5 场）

| # | 场景 | 验证点 |
|---|---|---|
| 1 | E_RouteA 模式采样（200v200 步兵野战） | 事件链五段/单编队隔离/无中段外部覆盖 |
| 2 | 战斗中热关闭 RouteA（MCM 关） | 原版恢复、无残留权重 |
| 3 | 战斗中玩家接管（手动下令） | CB 1 拍停、不抢回 |
| 4 | Rejoin 触发场景 | 目标=MainBody、导航失败路径不执行 |
| 5 | 增援/编队清空/拆分 | 生命周期恢复幂等、无状态泄漏 |
