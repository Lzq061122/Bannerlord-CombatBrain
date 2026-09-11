# QC 复核报告 —— Agent 级写入退役（CB-PROMPT-0038 / t4 review r1）

> 复核人：质检5（QC，AgentTeams combatbrain-开发组-20260911）
> 复核日期：2026-09-11
> 复核对象：分支 `feature/agent-control-retirement`，**复核锚点 = `1ed91e126620aa8a57ad3fa032b6b1dc604c9646`（HEAD，工作区 clean）**
> 锚点纪律：本报告落盘产生的 docs-only 提交不构成基准变更；若后续 HEAD ≠ 1ed91e1（除本报告提交外），按「锚点≠当前 HEAD」重审。
> 任务链：t2(6a33df4/7fc07bb) → t7/t8（承接归属）→ t3(8c4228e/8aea592) → t9(90d2fbf) → Captain(b0b1628 构建同步 / 9a584cb 指示文档回填) → t10(1ed91e1)
> 依据：派单卡 CB-PROMPT-0038 §五/§六/§八/§九/§十；t6 基线 `E:\MBmod\CombatAI\tools\cps-evidence-baseline-2026-09-11.md`

## 0. Verdict

**pass-with-runtime-debt（有条件通过；离线+静态证据充分，运行期 A/B 未执行）**

- 全部四条 verify 命令由 QC 在锚点状态独立复跑通过（§1）；
- §八 十二条逐条判定：10 条成立，2 条（§八.7/§八.8 的运行期计数证据）以静态证据成立、运行期观测随 A/B 待办（§2）；
- 范围账目（SCOPE-AMEND-1/2）闭合：37 个变更路径逐文件唯一归属，无未声明路径（§3）；
- 断言差额 −31 / −48 / −15 逐条名单核验，**无与退役无关的断言消失**，终值 1147 为 QC 实测（§4）；
- 对抗性扫描 + 门禁允许名单审查：生产 Agent 级主动写入唯一实调用 = 阵框释放（产品裁决保留项），允许名单 1 条目/文件+符号粒度/附理由，无目录级豁免（§5）；
- §六 玩家队豁免 7 项逐项核验成立（§6）；阵框四条件合规（§7）；
- 污染核验：部署 DLL/主检出/CPS 证据/日志零污染成立（§8）；
- findings：7 条 low（F-1~F-6 文档/代码卫生；F-7=SoldierTier 零消费者枚举残留，应 captain 补充清单核验后同日补录，§9.1；全部转 t5 承接，无 medium/high/blocker，§9）；
- 运行期验证债：D-1~D-3（§10），未闭环前本结论不得改写为无条件 pass。

---

## 1. QC 独立复跑（锚点 1ed91e1，clean；本机无 pwsh → 按 captain 指令用 powershell.exe 5.1）

| # | 命令 | 结果 | QC 实测 |
|---|---|---|---|
| 1 | `dotnet build "CombatBrain\CombatBrain.csproj" -c Release --nologo` | **0 warning / 0 error**，exit 0 | §八.12 ✓ |
| 2 | `dotnet run --project "OfflineTests\OfflineTests.csproj" -c Debug` | **ALL PASS，exit 0**；[PASS] 行计数 = **1147**，[FAIL] 行 = 0 | §八.11 ✓ |
| 3 | `powershell.exe -NoProfile -ExecutionPolicy Bypass -File OfflineTests\RouteAStaticChecks.ps1` | 全部 [PASS]（20 项），exit 0 | Route A 无回归 ✓ |
| 4 | 同上 `-File OfflineTests\AgentWriteStaticChecks.ps1` | 全部 [PASS]（**68 项**；96 生产文件），exit 0 | 静态门禁 ✓ |

> verify 契约中 `pwsh -File` 写法因本机未安装 pwsh 而以 `powershell.exe -File` 等价执行（环境事实，cap 已确认），非偏差。

## 2. §八 十二条逐条判定

| # | 要求 | 判定 | 证据（QC 亲验） |
|---|---|---|---|
| 1 | 默认无 X2 模型注册 | **成立** | `SubModule.cs` 无 OnGameStart/AddModel（源码 + 门禁「registers no game model」）；注释明确不触碰 AgentStatCalculateModel 链 |
| 2 | 无格挡/招架/攻击概率/射击误差写入 | **成立** | `AgentDrivenProperties.*=` 全仓零命中（门禁硬断言）；`CombatBrainAgentStatCalculateModel.cs` 已删除且类型零引用 |
| 3 | 近战无 SetMovementDirection | **成立** | QC 独立正则扫描：`.SetMovementDirection(` 生产代码零命中（含退役链硬断言）；MovementGlue/MovementController 文件级删除 |
| 4 | 无 EnforceShieldUsage | **成立** | 同上零命中；TickBlock/`_blockTimer`/`_jitterBlock` 零残留（注释留痕除外） |
| 5 | 无周期逐兵 SetTargetAgent / 强制换武器 | **成立** | 两 API 零命中；RangedGlue 重写为 0.3s 编队级只读弹药快照（源码复核：无任何 Agent 写入调用） |
| 6 | F7 编队级重整信号可触发；单兵不自行移动 | **成立（离线）** | 实测 `== F7 编队级重整信号 ==` 段窗口/阈值渐进 PASS；`Survival/CasualtyReformSignal.cs`（5s 窗/阈值 8）保留；`SurvivalGlue` 仅 publish IntentSwitched/Reform，零 Agent 写入；单兵移动 API 已不存在 |
| 7 | 玩家队豁免开启时主动写入计数为零 | **静态成立；运行期待 A/B** | 所有残余写入路径（阵框释放/编队命令/行为权重/租约）均有编队层前置豁免（§6 表）；运行期 `[Diag]` 计数实证属 D-1 |
| 8 | 关闭豁免不复活已退役逐兵系统 | **静态成立；运行期待 A/B** | 逐兵写入点在源码中不存在（编译期不可能复活），门禁②–⑥零容忍；MCM HintText 明确「关＝只军队/编队级接管」；运行期观测归 D-1 |
| 9 | 敌方编队意图/Army Coordination/Formation Mission/CPS 仲裁/Route A 无回归 | **成立（离线）** | 1147 全过含 X1/X3/X4/X5/T26/ATC1-4/CPS3r/RouteAFinalBlocker(62 项)/N1-N3 等段；RouteAStaticChecks 20 项全过 |
| 10 | Mission 结束无每 Agent 状态残留 | **成立** | `OnAgentRemoved`→`_agentLod/_survival/_frameReleaseCooldown.Remove`（:1567-1570）；`ClearMissionStaticState` 双钩子→`MissionStateLifecycle.ClearStatic`（Provenance/Feedback/DiagnosticCounters/Opponent/Lease 复位）；RangedGlue 快照每拍 Clear 不增长；AttackSuppression `_suppressedAttack` 静态已随文件删除 |
| 11 | 全量离线 0 FAIL | **成立** | 本表 #2（QC 自跑） |
| 12 | 构建 0/0 | **成立** | 本表 #1（QC 自跑） |

## 3. 范围账目（SCOPE-AMEND-1/2，逐文件唯一归属）

全量 `git diff --name-status f6634f7..1ed91e1` = 37 路径；逐提交 name-status 与归属核对：

| 归属 | 路径（数量） | 一致性 |
|---|---|---|
| t2 声明 | Ai/{CombatAiMissionLogic,RangedGlue}、4×D(X2/F4 文件)、SubModule、MissionStateLifecycle、OfflineTests{.csproj,Program.cs}、AgentWriteStaticChecks.ps1(A)、docs{audit,ADR-0005}(A) 等 13 条 | ✓ 与 t2 自述一致 |
| t7 承接 | Ranged/RangedBrain.cs(M)、Combat/AttackStats.cs(D)、Combat/PredictiveBlock.cs(D) | ✓ |
| t8 承接 | CombatBrainSettings.cs(M)（HandSkillStrength 死开关删除） | ✓ 生产代码零命中（QC grep） |
| t3（含 4 Ai/ 承接） | Ai/{MovementGlue(D),DecisionJitterGlue(D),DecisionObservatory(M),FrameReleaseGlue(M)} + DecisionDelay、DiagnosticCounters(A)、FormationCommandExecutor、FormationSlotGlue、SurvivalGlue、Compliance/DecisionJitter(D)、SlotAssigner、X5ExecutionPolicy、Mcm、gate/csproj/Program、ab-checklist(A)、audit(M) | ✓ |
| t9（SCOPE-AMEND-2） | Movement/MovementController.cs(D)、Survival/SurvivalInstinct.cs(D)→Survival/CasualtyReformSignal.cs(A，同目录 rename)、Combat/FighterProfile.cs(M 素质表+工厂删除，SoldierInput/PrimaryWeaponClass 保留) | ✓ 与 90d2fbf diffstat（-114 / -59+37 / -66）一致 |
| Captain b0b1628 | OfflineTests.csproj link 重定向 + Program.cs 移除已退役断言块（构建性同步，未放宽任何存活断言） | ✓ 独立审查通过 |
| Captain 指示 9a584cb（归属 t5 声明） | docs/agent-control-surface-audit.md 回填（§0b.4-B/M7-M10/口径 15 条） | ✓ 内容真实性逐条核对与 diff/实测一致（§4/§5/§8） |
| t10 1ed91e1 | AgentWriteStaticChecks.ps1（软→硬 55→68）+ Program.cs（仅注释 14→15 更正与逐条指引） | ✓ Program.cs 无断言增删 |

**结论：无未声明路径；每路径唯一归属成立；审计表 §0b.4-A/B 的 SCOPE-AMEND 记录与实际一致。**
（QC 对 9a584cb 特记的核验：其「FighterProfile 整表退役」表述为素质表+工厂删除+输入类型保留，与 §0b.4-B 表格描述、diff、门禁 R3 断言三方一致，非掩盖缺口。）

## 4. 断言账目（1241 → 1147；三段逐条，QC 独立从 diff 重建）

- **t2 段 −31**：删除 35 条调用行（X2 HandSkillProfile 公式 21 / AttackSuppressionState 状态机 9 / F9 `RangedBrain.SelectTarget` 5）− 新增 4 条（DecideFallback 0.15/0.16 阈值边界，属**保留合同加固**）。全部对应退役模块 ✓。
  注：审计表仅有模块级映射（§0a），无 −31 逐条名单；本报告即为名单来源，t5 归档时应引用本报告附录或补 §0b（见 F-5 说明，归入 §9 文档项）。
- **t3 段 −48**：QC 提取删除名单 53 行 − 新增 5 行 = 48，与审计表 §0b.2 的 8 组映射（F6 12 / F7 3 / X5 腿 5 / X5 Bench 3 / X5 冷却 5 / SlotAssigner 2 / F12 15 / ScaleProfile 3）**逐条一致**；ShouldSkipAgent 3 参→2 参替换（删 4 增 4）与 §0b.2「条数不变」一致 ✓。
- **t9 段 −15**：`TestFighterProfile` 12×Classify + 3×Create = 15（QC 前轮实测 15 vs 口径 14 的 F-A 偏差已由 9a584cb/1ed91e1 按 15 修正，§0b.2 补记/M10 现为 15 条）✓。
- **t10 段 ±0**：Program.cs 无断言增删（仅注释）✓。
- **终值交叉**：1241−31−48−15=1147 = QC 运行时实测 [PASS] 计数 ✓。**无任何与退役无关的断言消失。**

## 5. 对抗性扫描与门禁审查

- QC 独立正则（非开发者路径）扫生产代码：`SetMovementDirection/SetTargetAgent/TryToWieldWeaponInSlot/EnforceShieldUsage/SetScriptedFlags/AgentDrivenProperties=.*/MBAPI.` **全部零实调用**；`agent.Set*` 宽口径唯一命中 = `CombatAiMissionLogic.cs:2097 SetFormationFrameDisabled`（允许项）；`SetControlledByAI` 仅 `Formation` 级（LeaseGlue :375/:426/:455，默认关，属 §十.9 L2 保留清单）。
- 允许名单审查：1 条目、文件+符号双粒度、逐条理由（脚本 :49-57）、命中打 [INFO] 且要求审计表 §5 溯源（有：K1 行）；**无目录级/通配豁免** ✓。
- 退役链：11 个退役文件删除断言 + 13 个退役类型零引用断言 + 7 个逐兵残留符号断言全部为硬断言（t10 升级），QC 实测全过。
- 防过度退役：F7 信号/F9 DecideFallback/DecisionDelay.HoldTime/编队执行器/SoldierInput 链路均有「保留在位」硬断言 ✓。
- 小缺口（不阻断，转 t5 备忘）：门禁未把 Agent 级 `SetControlledByAI`/`AddAiBehavior` 列入禁止模式（现状 Agent 级零调用，属回归哨兵缺口，见 F-6）。

## 6. §六 玩家队豁免 7 项逐项

| 承诺项 | 判定 | 证据 |
|---|---|---|
| 不改玩家队 Formation 命令 | ✓ | 唯一执行器 `FormationCommandExecutor.cs:230` 玩家队 gate；意图/提案各 Glue（13+ 判定点）编队层前置豁免 |
| 不改编队行为权重 | ✓ | RouteA Adapter `PlayerOwned` 拒写（:141）+ 租约/权重路径玩家队排除 |
| 不改士兵移动方向 | ✓ | API 全退役（写入点不存在，任何队都不写） |
| 不强制格挡/换防/选敌/换武器 | ✓ | 同上，四类 API 零命中 |
| 不改 AI 士兵战斗能力属性 | ✓ | X2 链整体删除，AgentDrivenProperties 零写入 |
| 不释放玩家队士兵阵框 | ✓ | `TickFrameRelease` :2057 `ShouldExempt` 在队迭代最前，玩家队不进入执行体 |
| 只读观测允许且不改行为 | ✓ | TickLod/Observatory/Telemetry/DiagnosticCounters 纯只读；LOD 全量评估无行为面 |
| （附）MCM 语义 | ✓ | 「玩家队豁免（全控制面）」+ 7 项全覆盖 HintText（CombatBrainMcmSettings.cs:23-30），「玩家本人≠玩家队」歧义已消除 |

## 7. 阵框释放（K1/D1）裁决四条件

1. **无附带逐兵战术判断** ✓：触发全为编队级量（Charge/ChargeToTarget + ≤25m + >25 人），逐兵分支仅非战术过滤（存活/IsHuman/LOD≥3/逃跑/latch/冷却）；目标选择用编队级 X1（TryGetFormationTarget）。
2. **5s 冷却 + 一次性 latch** ✓：`FrameReleaseCooldown.CooldownSeconds=5f` + `ShouldSkipAgent(runningAway, !IsFormationFrameEnabled)` 只读引擎状态；离线 X4 段 20 项含 5s 边界/latch/去重测试，实测 PASS。
3. **玩家队零写入** ✓（§6 表第 6 行）。
4. **v1.4.8 API 核验证据 + 运行期只读计数** ✓/部分：API 行号级证据（:1444/:2305/:2315/:3891/:2051 + HumanAI 每帧重建恢复边界 + TeamAI 上报）落 FrameReleaseGlue 文件头与审计表 §0b.3，QC 核对为真实反编译坐标引用；运行期计数 `CountFrameReleaseWrite` 已接唯一写入点前（:2096）→ 振荡/重复写实证 = **D-2 运行期债**（审计表 §2.4.1⑥ 与裁决一致地未宣称闭环）。

## 8. 污染核验（对照 t6 基线 13:56:32）

| 对象 | 基线 | QC 复核（17:0x） | 判定 |
|---|---|---|---|
| 部署 DLL Modules\CombatBrain\...\CombatBrain.dll | 227840B / 09-11 00:11:45 / A39AC964…FB22 | **逐字节一致** | **零污染**（全程无部署） |
| SubModule.xml / opponent_priors.json | 1407B 09-04 / 774B 09-01 | 一致 | 零污染 |
| 主检出 HEAD + 5×??docs/cps* | f6634f7 + 5 未跟踪 | 一致；worktree list 正常 | 用户未提交文件**未被清理/覆盖** |
| %TEMP%\CombatBrain.log | 36065720B / 13:55:58 | 36249032B / **13:58:15** | **仅尾部追加**：边界时间戳单调延续（t=162→298 同一会话）、尾部=旧构建（f6634f7 DLL 仍在部署）自然收尾 `[遥测] 落盘 13:58:14`；**全部早于团队首次写入（14:18）** → 非退役工作污染 |
| CombatBrain.telemetry.json | 669B / 09-10 13:14 | 669B / 09-11 13:58:14 重写 | Mission 结束一次性落盘（该文件语义即覆盖写），由上述同场会话收尾产生；无构建关联 |
| .agent-teams 归档（archive/开发组/coldstart-audit） | 09-08/09 | 未变 | 零污染 |
| .agent-teams 其余（20260910 dir / retired-members.json 14:07-14:08） | 09-11 00:15 / 09-09 | 变动 | Captain 成员管理操作（harness 状态，非 CPS 证据），不构成产品污染；已通报确认 |

## 9. Findings（全部 low，回流 t5 文档收口消化；无 medium/high/blocker）

| # | 严重度 | 文件:行 | 问题 | 必要修复 | 验收 |
|---|---|---|---|---|---|
| F-1 | low | docs/agent-control-surface-audit.md §5 K1/L1-L3 行（:368 附近） | K1 状态列仍「待裁决」且行号为 f6634f7 时点（:2188/:2151；终态实为 :2097/:2057），与 §0b.3/M7「已裁决保留」漂移 | t5 更新状态列为 resolved + 标注「行号坐标=f6634f7 审计时点」或改写终态行号 | t5 diff 复核 |
| F-2 | low | docs/agent-control-retirement-ab-checklist.md :78 | 引用门禁「55 条断言」，终态实测 68 | t5 文档同步为 68（或去掉具体数字引用） | 重跑门禁计数比对 |
| F-3 | low | CombatBrain/Intent/BracePlan.cs :50-53（**既有**，非本分支引入） | survivalRetreating 参数注释仍描述已退役逐兵语义；唯一生产调用 BraceGlue.cs:138 恒传 false（f6634f7 前即如此） | t5 记入残留清单：简化参数或注释改为「编队级撤退预留位」；本分支无改码义务（产品代码不在 t4/t2-t10 终改范围） | t5 裁决记录 |
| F-4 | low | CombatBrain/Ai/DiagnosticCounters.cs :36 / :49-72 | ① `FrameReleaseDistinctAgents` 公开字段无写入点（去重实际用字典 Count）；②–⑥ 计数器无调用点＝结构性恒 0 的回归哨兵 | 后续清理：删死字段或在注释/ab-checklist 标注「②–⑥为哨兵语义，运行期证据只对 ①⑦ 有观测意义」；静态门禁仍是「零写入」主证据 | t5 或后续小型卫生提交 |
| F-5 | low(文档) | OfflineTests/Program.cs :286 | 「14 条」已按 F-A 更正为 15 ✓，但 t2 段 −31 的逐条名单只存在于本报告（审计表为模块级映射） | t5 将本报告 §4 名单并入审计表 §0b 或引用本报告 | t5 diff 复核 |
| F-6 | low(门禁) | OfflineTests/AgentWriteStaticChecks.ps1 :66-74 | 禁止模式未含 Agent 级 `SetControlledByAI`/`AddAiBehavior`（现状零调用；属未来回归哨兵缺口） | 后续门禁加固周期补两条禁止模式（不阻断本次） | 下轮门禁 PASS 含新断言 |

### 9.1 补录（同日，应 captain 17:0x 补充清单；锚点不变，verdict 不变）

| # | 严重度 | 文件:行 | 问题 | 判定与必要修复 | 验收 |
|---|---|---|---|---|---|
| F-7 | low | CombatBrain/Enums.cs :36-42（定义）+ :44（注释） | QC 全仓复核确认：`SoldierTier` 在 t9 后**零代码消费者**（仅 Enums.cs 定义 + Program.cs 两处退役注释）；:44 注释仍写「F10/F6 **FighterProfile.Classify** 输入」，指向已删除方法 | **不构成 needs_revision**，理由：① 不违反 §八任何一条与 t2/t3/t9 验收（M10 裁决文本=素质表四字段+工厂，未含枚举）；② `Enums.cs` 从未在任何任务 inScope 内，追溯要求已 completed 任务改动它反而制造越界写入；③ 零行为/零测试影响；属 R3「消费者归零」精神的延伸残留＋§七「编号不得暗示旧功能有效」文档层，**归 t5 单独承接**：删除 SoldierTier 枚举、改写 :44 注释为实际消费者（TroopClassifier/X5-1），承接任务契约 inScope 须显式含 `CombatBrain/Enums.cs` 并同步 link 检查（OfflineTests 编译链） | t5/承接任务 diff + 四命令复跑全绿 |

**处置口径**：F-7 以 low 登记并直接归入 t5 承接清单（与 F-3 同类：既有/延伸卫生项），不触发 repair + 下一轮 review——该机制针对影响验收结论的缺口，本项不改变 §0 verdict=pass-with-runtime-debt 的任何判定基础。

## 10. 运行期验证债（未闭环项——本 verdict 上限的决定因素）

| # | 内容 | 闭环路径 |
|---|---|---|
| D-1 | §九 游戏内 A/B（A1–A8：观感/战术保留/CPS 正常/500 人性能/②–⑥ 恒 0）未执行 | ab-checklist §3/§7 采集，游戏进程退出后由 Integration Owner 授权独立构建部署 |
| D-2 | 阵框释放「无重复抢写/无动作振荡 + 恢复后重新整齐」运行期实证（裁决 M7 附带条件⑥） | ab-checklist §5 + `[Diag] FrameRelease` 单列；异常→回报 Captain，禁止静默放宽 |
| D-3 | §八.7/§八.8 的运行期形态（豁免开/关两态的写入计数日志） | 并入 D-1 采集 |

**判定纪律**：以上三项未闭环前，本任务链任何对外结论只能是 `pass-with-runtime-debt`；「测试通过」不替代行为解释（派单 §十末段）。

## 11. 复核实际读取的知识源（加载纪律）

CombatAI/AGENTS.md → repo AGENTS.md → docs/knowledge/INDEX.md（bootstrap）；派单卡 prompts/active/CB-PROMPT-0038；t6 基线文件；worktree 终态源码（SubModule/CombatAiMissionLogic 关键段/FrameReleaseGlue/RangedGlue/SurvivalGlue/DiagnosticCounters/MissionStateLifecycle/MCM）；门禁脚本全文；审计表全文；ab-checklist 全文；git diff 逐段名单（脚本化提取）。未读：legacy CONTEXT、research archive、_kb、Master Spec 全文、历史 handoff。

---

## 12. 增量复核 round 2（t11 卫生清理与门禁加固）—— 2026-09-11

> 复核人：质检5（t4 之 round-2 增量；任务 t12，review kind）
> **round-2 锚点 = `31823b6aa81a58b2166e63959c5a345b66897608`**（HEAD，工作区 clean；进入与 finalize 前各核一次，无漂移）。
> 与 §0 的关系：t4 在锚点 `1ed91e1` 的 verdict **保持不变**（pass-with-runtime-debt）；本节只覆盖 t11 增量
> （`CombatBrain/Enums.cs`、`CombatBrain/Ai/DiagnosticCounters.cs`、`OfflineTests/AgentWriteStaticChecks.ps1`）。
> 锚点间链：1ed91e1 → 158ef2b/2d93bc3（QC docs-only）→ 8fc7ba8（Captain t5 治理文档，docs-only）→ 31823b6（t11 代码）。
> **自 1ed91e1 起产品测试代码零变化**（Program.cs 不在链中），t4 §2/§4 结论对本锚点继续有效。

### 12.1 四命令独立复跑（QC 自跑，powershell.exe 5.1 环境适配同前）

| 命令 | 结果 |
|---|---|
| `dotnet build CombatBrain.csproj -c Release --nologo` | 已成功生成，**0 警告 0 错误**，exit 0 |
| `dotnet run OfflineTests -c Debug` | **ALL PASS**；[PASS]=**1147**（与 1ed91e1 实测一致，**零断言增减**）；真实 FAIL=0（10 处正则误报均为 `[PASS] …fail-open…` 测试名） |
| `RouteAStaticChecks.ps1` | **20/20** [PASS]，exit 0 |
| `AgentWriteStaticChecks.ps1` | **73/73** [PASS]（68→73，+5 全部为加固项），exit 0 |

### 12.2 t11 三项验收逐条

1. **F-7/M12（SoldierTier）**：✓ `git grep -n 'SoldierTier' HEAD`（除 bin/obj）仅剩 3 处退役说明注释 + 治理文档提及，**零代码引用**；`Enums.cs:44` 注释已更正为「X5-1 兵种分类输入；消费链 Ai/FormationSlotGlue.BuildSoldierInput → Formation/TroopClassifier.Classify」，滞留的 FighterProfile.Classify 指向消除。不留「未来预留」waiver 的处置符合 R3/§七精神。
2. **F-4（DiagnosticCounters）**：✓ 死字段 `FrameReleaseDistinctAgents` 声明与 Reset 赋值两处均删，去重统一走 `FrameReleaseDistinctAgentCount()`（`_frameReleasePerAgent.Count`）；六类计数器 ①–⑥ 与 FrameRelease 单列 ⑦ **全部在位**，②–⑥ 显式标注「哨兵·结构性恒 0」并写明运行期观测只对 ①⑦ 有意义——与 t4 报告 F-4 建议口径一致；门禁三条相关断言（six write counters present / read-only / reset at lifecycle）未放宽且实测 PASS。
3. **F-6（门禁扩充）**：✓ 新增 Agent 级 `SetControlledByAI`/`AddAiBehavior` 禁止规则（收信者变量名分类：agent/a/unit/soldier/defender/attacker 拦截；f/formation/AI 放行）+ 注册断言 + 正/负两条元测试（实测 `got 2: agent.SetControlledByAI(,agent.AddAiBehavior()` 与 `got 0`）。编队级保留项**未被误伤**：`FormationControlLeaseGlue.cs:375/426/455`（`f.` 收信者）与 `RouteAOrderAdapter.cs:175`（`f.AI.`）在新规则下全仓扫描零命中（两条 unauthorized 断言 hits 为空）。allowlist 仍仅阵框释放 1 条目；重构后 Pattern 分支语义与原逻辑一致，既有 68 项逐条在位。**残余观察（low，不立项）**：收信者启发式依赖变量命名（如假想 `ai.SetControlledByAI` 会被放行），属前瞻哨兵的固有近似，已由元测试双向锚定，不构成缺陷。
4. **范围纪律**：✓ 31823b6 diff 恰为 3 个声明文件（+65/−25）；F-3（BracePlan 死参数注释）按最小范围明确不动并留理由——正确处置（该项归 t5 残留清单，captain 已在 8fc7ba8 final-report §5 登记）。

### 12.3 断言账目（round 2）

- 离线：1147 → **1147**（±0；Program.cs 不在 1ed91e1..31823b6 链中）——t11 声明「零断言增减」与 QC 实测一致，无新增抵充需求。
- 门禁：68 → 73（+5）：规则注册断言 ×1 + 正/负元测试 ×2 + 新增扫描规则断言 ×2；**无删除、无放宽**。

### 12.4 污染快照（增量复核时点）

部署 DLL 未触碰（t11 未部署；游戏 Modules 维持 t6 基线 A39AC964…）；主检出不受影响（t11 与 8fc7ba8 均落在 worktree 分支）；本轮唯一新增写入为本节（docs-only）。运行期债 **D-1/D-2/D-3 维持开放**，不因增量复核改变；§0 verdict 不改写。

### 12.5 Round-2 verdict

**pass（增量范围）**——t11 三项验收全部独立成立、四命令全绿、账目闭合、无误伤、无新 finding 立项（残余观察 1 条 low 不阻断不回流）。本结论仅覆盖 t11 增量及其与 1ed91e1 锚点的一致性；整体任务链状态仍为 §0 的 **pass-with-runtime-debt**（游戏内 A/B 未闭环前不得升级为无条件 pass）。

### 12.6 对抗性探针（应 captain 18:0x 点名项 3：分类规则可绕过性与元测试自证循环）

QC 以门禁同构逻辑（复制收信者正则+名单，独立运行，不跑开发者的脚本本体）实测 10 类样本：

| 样本形态 | 结果 | 判定 |
|---|---|---|
| `this.Agent.SetControlledByAI(…)` | 命中 1 | 属性链末端 `Agent` 被收（正则取点前最后标识符） |
| `someField.Agent.SetControlledByAI(…)` | 命中 1 | 链式同上，不可借前缀绕过 |
| `agent⏎ .AddAiBehavior(…)`（跨行） | 命中 1 | `\s*` 覆盖换行 |
| `a.SetControlledByAI(…)` | 命中 1 | `a` 在名单 |
| `formation.`/`f.AI.` | 命中 0 | L2/L3 保留语义正确放行 |
| `var x = agent; x.…` / `ai.` / `aI.` | 命中 0 | **理论绕过面存在**，但见下「三层防御」分析 |

**三层防御分析（结论：残余面不构成缺陷）**：
1. **主战场无绕过面**：真正危险的四类逐兵 API（SetMovementDirection/SetTargetAgent/EnforceShieldUsage/TryToWieldWeaponInSlot）+ AgentDrivenProperties 写入 + SetScriptedFlags 用的是**裸模式、不区分收信者一律拦截**——别名/链式/任何命名都必命中，且带「retired fully removed」双重断言。
2. **收信者规则保护的是编译期不存在的调用**：按审计表 §2.10 J3/J7（v1.4.8 反编译核验），`SetControlledByAI` 是 `Formation` 级、`AddAiBehavior` 是 `FormationAI` 级——Agent 类型上不存在这两个成员，`agent.SetControlledByAI` 形态当前**根本无法编译**；该规则是防「引擎升版后 Agent 出现同名 API」的**前瞻回归哨兵**，命名启发式的固有近似成立。
3. **元测试非自证循环**：正/负样本为手写常量字符串 + 期望硬编码计数（2/0），正向样本同串混排 `agent.*` 与 `f.*` 证明的是**规则内判别力**而非规则复读；其作用是「未来改名单/改正则时立刻报警」（篡改检测），生产扫描断言（空命中）才是对当前分支的独立证据——两层证据源不同，结构成立。

**探针总结论**：§12.5 的 pass 维持；残余观察（命名启发式）从 low 精确化定性为「前瞻哨兵的固有近似，当前不可利用」，不立项、不回流。

### 12.7 锚点后漂移登记（如实报告，不改写基准）

round-2 代码基准锁定 `31823b6`；其后分支新增两个 **docs-only** 提交：`c89276a`（本报告 §12）与 `eb74170`（Captain t11/t12 收尾登记：审计表 M12→resolved、final-report §5 台账同步）。产品代码与测试代码零变化，无需重新基准化复核；QC 核对 eb74170 内容与实测一致（M12 行、:410 台账行），**唯一措辞滞留**：final-report :127 风险第 5 条仍把 `DiagnosticCounters` 哨兵计数与 `SoldierTier` 死枚举列为「既有残留（承接任务）——已登记」，而两者已分别以 t11 执行/设计保留闭环——trivial（:112/:114 明细行已是正确状态），提请 captain 下次文档触碰时顺带收齐，不单独立项。F-3（BracePlan 死参数注释）的「登记不动」处置确认**可接受**：f6634f7 前既有、非行为缺陷、不在 t11 契约 inScope，追溯改动反而违反最小范围；已正确留痕于 final-report F-3 行。
