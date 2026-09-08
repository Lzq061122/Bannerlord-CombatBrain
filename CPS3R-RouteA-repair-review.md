# CPS-3R Route A 最终阻断修复收工记录

任务：cps3r-routea-final-blocker-repair，2026-09-08。

## 基线与交付边界

活动代码仓库为 `E:\Agent\6a9268112aeaf49072bef052`，不是 `E:\MBmod\CombatAI` 文档仓库。
本轮实际动工 HEAD 为 `ca775a83e64c0bca219afd4820552ad16f4da659`，其父版本为派单指定的 `5868a02`。
动工时跟踪文件干净，仅既有 `docs/` 未跟踪。派单描述的 Astra 三文件修改已经被 ca775a8 吸收；本次保留该提交及其中候选阶段禁发令、普通执行器 Movement 互斥、三条意图冲突边和回归测试。
本报告与修复代码一起形成一个独立提交；收工 HEAD 的完整值见交付消息（避免在提交内容中自引用尚未生成的提交哈希）。未改文档仓库、未覆盖 docs/、未部署 DLL。

最新正式派单 §十一授权检查全部通过后提交，取代初始审计请求的“不提交”交付规则。

## 阻断点与修正

以下旧行号均指动工版本 ca775a8 的 `CombatBrain/Ai/FormationCommandExecutor.cs`，可用 git show 复核。

1. 483–500：Adapter 每次回调重复写入，缺少命令/目标复核、完成观测和强制 Tick 隔离。535–547 的开关 OR 使关闭独立开关仍可执行。现在只允许真实选中且资格有效的回调，经唯一 Adapter 落地；同 revision/activation 不重复发送，失败立即释放。
2. 459、487、522：对象哈希充当身份，注册未保存完整原值。现在单槽保存对象引用及四个被改动的 CB 权重，先清旧后选新；所有释放在恢复前撤销权限，重复调用无副作用。原版 Behavior 权重未被修改。
3. 599–609：Rejoin 的“导航校验”只有纯距离/接敌条件。现在使用候选所属 Team 的已知敌军帧、真实 Phase、有置信度的 MainBody 坐标，实际检查边界、起终点 navmesh 和公开路径距离；失败不保留上拍所有权。
4. 旧 RouteAEventLogger 将 lifetime Applied 与有限 CPS-0 ring 的替换数相减，且全局汇总仍减全局替换数。现在同步消费对象引用和真实 setter 完成事件，分 session/identity/Movement 保留计数，不依赖 128 条 ring，不将 Prefix 被跳过的调用当作 Applied。
5. 592、596、608、613 每拍创建捕获工厂闭包，旧振荡判定未关联真实态势。现在按 FormationAI 缓存四个实例，复用已有快照；逐拍仅值计算，诊断事件固定缓冲，5 秒/退出落盘。A→B→A 仅在两秒内且所有中间采样态势均未变时计 flap。

## 实际引擎调用链

检查的是本机游戏 `bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.dll`，SHA256：
`19387F31557FF840D14F378F6BBDF1D58FCFF406AB9FF2294DFBB3E49A50B87E`。
该 DLL 的版本元数据为 1.0.0.0，不能单凭该字段证明发行版号；可重跑静态脚本确认实际安装文件及调用链。

反编译 FormationAI.FindBestBehavior：候选 PrecalculateMovementOrder（源码映射 190）先于 ActiveBehavior 赋值（201）。因此原 t52 在 CalculateCurrentOrder 写入不能证明真实选中，也超出了原“执行器三处”的声明。
正常 TickOccasionally 分支调用胜出 Behavior.TickOccasionally（254）后 return；288 的 setter 是另一个 fallback 分支，并非正常分支自动再应用 CurrentOrder。
另有 Formation.SetControlledByAI(true)：ForceTickOccasionally=true → AI.Tick → 单独 SetMovementOrder(AI.ActiveBehavior.CurrentOrder)（808）。Adapter 在强制 Tick 时释放，避免同一路径 CB+原版双写；预计算清除公开 IsCurrentOrderChanged 标记，避免候选结果进入 fallback 写入。

批准架构为：意图 → 原版竞争/选择 → 胜出 CB TickOccasionally → RouteAOrderAdapter.TryApply → Movement setter。
这是正式派单批准的适配器执行边界，不再声称原版会自动替 CB 落地，也不再声称全 Ai 只有三个 setter。未增加 Harmony 阻断，未读引擎私有状态。

## 文件与静态结果

- `CombatBrain/Ai/FormationCommandExecutor.cs`：提前跳过 Route A 所有的 Movement，并清旧写入缓存；保留 Facing/Arrangement。
- `CombatBrain/Ai/RouteAOrderAdapter.cs`：唯一 Route A setter、四 Behavior、资格和导航桥接、实例缓存、日志排空。
- `CombatBrain/Ai/CombatAiMissionLogic.cs`：真实 Team/Phase/快照、执行器之前预留所有权、生命周期释放。
- `CombatBrain/Coordination/RouteAControl.cs`：引用身份注册、原权重恢复、选中/去重、态势变更判定。
- `CombatBrain/Coordination/RouteAEvidence.cs`：独立有界事件流、可信落地与替换关联。
- `CombatBrain/Coordination/CommandProvenance.cs`：保留 CPS-0 语义，窄域接入只读 Movement Prefix/Postfix；不阻断 setter。
- `CombatBrain/Settings/Cps2PoCSettings.cs`、`Cps3RuntimeSettings.cs`、`CombatBrain/Mcm/CombatBrainMcmSettings.cs`：预算固定一支，E 模式与独立热开关共同门控。
- `OfflineTests/OfflineTests.csproj`、`Program.cs`、`RouteATests.cs`、`RouteAStaticChecks.ps1`：共享生产核心回归及实际 DLL/编译产物检查。
- 本报告。

生产直接 setter：执行器 370/374/377 行三域各一处；Adapter 35 行 Movement 一处；四 Behavior 零处；其他生产代码零处。扫描去除注释和字符串。

## 验证结果

ca775a8 源码通过 git archive 在忽略目录独立编译运行：1192 PASS，0 FAIL。
修复后相关回归随全量 OfflineTests 执行：1227 PASS，0 FAIL，退出码 0。替换了绑定旧计数器/空状态的断言；新增回归调用共享生产控制器和 ExecuteWinner 的真实 sink，覆盖对象碰撞、非零原权重恢复、失败释放、重复 Tick、超过 128 条历史、诊断中断/溢出等。
Release 构建成功：0 warning、0 error；实际 DLL 与编译后四 Behavior 的 20 项静态契约检查通过；git diff --check 通过。
离线替身不执行游戏 native setter/导航，也不能证明 Harmony 在真实战斗的挂载情况。静态调用链依据实际 DLL 反编译，运行期结果仍待实测。

## 生命周期恢复矩阵

| 触发 | 生产入口 | 恢复行为/验证边界 |
| --- | --- | --- |
| 热关闭、离开 E、总开关关闭 | 每拍 Reconcile；Adapter 再检查 | 下个逻辑拍前或落地前释放；离线门控/非零权重/幂等通过 |
| 玩家接管、出现玩家兵员 | Reconcile、Adapter、OnLostAIControl | 撤销 Movement；重复释放不重写；真实玩家操作待场测 |
| 清空、失效、对象替换、Index 复用、增援换目标 | 候选枚举前检查旧对象，Update 比较引用 | 先恢复旧对象，新对象重新捕获；哈希碰撞/替换通过，增援场景待实测 |
| AI 实例替换、非野战、强制 Tick | 资格复核 | 释放；强制 Tick 公开调用链已核实 |
| Mission 切换/结束/清场 | BeginSession、OnMissionStateFinalized、OnClearScene | base teardown 前 ResetAll；重复调用无状态泄漏；实际钩子顺序待场测 |
| 初始化异常、Adapter 失败、Rejoin 不可达 | catch/验证失败 | 清权限，逐项恢复；已销毁对象异常单独计数并移除注册 |

## 证据链与旧证据

每条事件含 run GUID、session、严格事件 seq、Mission 时间、引用注册 identity、team、formation、Movement 域、事件种类、intent、revision、严格 setterSeq、原观测器 observerSeq、relatedSeq、source、orderType、退出原因、态势 signal、目标 x/y。
Intent/BehaviorSelected/OrderApplied 独立记录；setter 完成后且原方法确实运行、落地类型/目标核验一致才算可信 Applied。嵌套 setter 不继承 Adapter 的一次性直接来源令牌。
活跃期间第一个实际外部写入关联并替换尚未被替换的 Applied；退出先记 Exit，再恢复权重并记 Restore；退出后的 setter 归 ExternalAfterExit，关联 Exit 序号。
StableSoFar=同 session/identity/Movement 的可信 Applied−对应 ExternalReplaced，仅表示截至观察时点，不能替代持续时间验收。
新 Mission 清身份/计数/事件，session 递增；独立 setterSeq 不受 CPS-0 Enable/Clear 重置影响。身份最多64、事件缓存2048；溢出或诊断中断显式 dropped，观测缺失记 unverified，这类样本不得用于验收。身份表满停止新接管。

旧原始 CombatBrain.log 的场 3：09:55:24.700 累计 Applied759/extRep0；09:55:29.707 Applied760/extRep2，之后 Applied 仍增长。这只能证明两次新增替换落在汇总区间内，缺逐 setter/Exit 链，不能严格归类为退出后恢复或活跃时覆盖。旧场次还混入累计 Applied；只见 Hold/Rejoin，Advance/Retreat 缺完整原始证据。旧三场全部失效，未重新包装。

## 已知限制、回滚点与最多五场补测

公开路径查询的战场耗时、引擎/第三方 Harmony 的实际顺序、真实权重恢复效果尚无修复后场测证据。上下文超过一秒即释放；Rejoin 移动目标变化累计达到五米才产生新 revision；不靠高频重发掩盖原版覆盖。强制 Tick 主动交还原版，不视为 Route A 应用成功。回滚点为 ca775a8；回滚本独立提交会恢复已知阻断状态，仅供代码比较，不代表可部署版本。

所有场次从开场开启来源观测，留完整原始日志和版本哈希；dropped/unverified 非零、缺 Selected→真实 setter、恢复失败均拒绝作为通过样本。每种意图至少记录一次完整 Intent→Selected→Applied，并连续观察10秒或至明确 Exit，逐条关联覆盖。

1. 普通野战、AI 步兵、稳定态势60秒：触发 Hold→Advance，观察原版实际选中、每 revision/activation 单 setter、普通 CB Movement 零竞争、Facing/Arrangement 保持、重复挂载/分配及两秒 flap。
2. 与玩家敌对 Team 的脱离主力步兵：触发 Rejoin，记录真实 Phase/MainBody 和实际目标；用不可达/边界外或接敌变化使校验失败，须同拍撤权恢复，无旧 Rejoin 残留。
3. 士气/阶段自然触发 Retreat：获取该意图完整链；在 Route A 活跃时观察一次原版覆盖，再热关闭后观察一次原版恢复，用 setterSeq、relatedSeq、Exit/Restore 精确区分。
4. 同场反复 E 进入/退出、独立热关闭、玩家接管/交还 AI；随后清空/增援/重组目标并覆盖 Index 复用。核对旧引用退出先于新 Intent、四权重恢复原值、无玩家队/第二编队 Route A 写入。
5. 结束场4并紧接新 Mission（含一次非野战对照），验证双清理钩子幂等、session 清零、旧回调不污染；新普通野战以500人持续运行至少120秒，记录 Route A 耗时/分配、对象和日志缓冲上界。

CPS Pending；条件5不满足；P3、ADR、GO/NO-GO 最终判定继续暂停。完成本卡只表示代码修复可重新审查。

**READY FOR ASTRA RE-REVIEW**
