# CombatBrain 个体控制面退役 —— 游戏内 A/B 验收步骤与只读诊断计数（§九）

> 依据：CB-PROMPT-0038 `CombatBrain-个体控制面退役与产品边界收口-正式派单-2026-09-11.md` §九（游戏内验收）与 §八（测试要求）。
> 配套：`docs/agent-control-surface-audit.md`（审计表与实施回填；§0b.3 是阵框释放的 v1.4.8 API 核验）。
> status: 步骤已就绪、**尚未执行**（运行期验证债；未执行前任何结论不得写成无条件 pass）。

---

## 1. 验收目标（判定口径）

| # | 观测项 | 通过判据 | 证据来源 |
|---|---|---|---|
| A1 | 我方 AI 士兵不再出现规律性动作翻转 | 无 0.1/0.2/0.3 秒级周期翻转（目视 + 录制回放） | 游戏内观感 |
| A2 | 小碎步 / 反复横移 / 突然站定后立刻继续移动 | 明显消失（与 A 组对照） | 游戏内观感 |
| A3 | 盾牌防御方向不再高频变化 | 无高频盾向切换 | 游戏内观感 |
| A4 | 玩家队整体动作接近原版 | 玩家队 AI 士兵观感 ≈ A 组 | 游戏内观感 |
| A5 | 敌方仍体现军队级/编队级战术差异 | 敌编队仍按意图换阵/包夹/波次 | 游戏内观感 + 日志 |
| A6 | CPS Route A / 命令仲裁 / 编队任务仍正常 | Route A 证据链与仲裁日志正常，无异常 ExternalReplaced 风暴 | `[Cps2Observation]` / `FlushEvidence` 日志 |
| A7 | 500 人性能不劣于当前基线 | `[Bench]` 帧时/AI 段不劣化（目标：锁 30 FPS 时 AI ≤10% 帧预算） | `[Bench]` 日志 |
| A8 | 被退役的 Agent 主动写入全部为零 | 诊断计数 ②–⑥ 恒 0 | `[Diag]` 日志（见 §4） |

**说明**：A1–A5 属观感判定，必须在同一场景/兵种/阵型/命令下 A/B 对照；A6–A8 可读日志判定。

---

## 2. 前置条件（硬约束）

1. **不得污染 CPS 运行期证据**：不覆盖、不部署正在使用的 CPS 测试 DLL/日志；`%TEMP%\CombatBrain.log` 只追加读取、不删除。
2. **游戏进程必须退出后**才允许构建/部署类动作（本文件只描述流程，不授权部署）。
3. **独立构建**：B 组使用 worktree `E:\Agent\cb-agent-retirement`（分支 `feature/agent-control-retirement`）产出的独立构建；A 组使用原版（或退役前基线构建）。
4. 未合入 main、未部署到共享目录前，A/B 只在本地独立目录进行，**不得覆盖游戏 `Modules\CombatBrain`**。
5. 采集基线参考（只读）：`E:\MBmod\CombatAI\tools\cps-evidence-baseline-2026-09-11.md`（部署 DLL SHA256 / 日志大小 / mtime，供「零污染」对比）。

---

## 3. 固定场景与流程

**场景**：Custom Battle（自定义战斗）——同一地图、同一兵种构成、同一阵型、同一命令序列。

```text
A 组（对照）：原版
B 组（实验）：退役个体控制面后的 CombatBrain（worktree 构建）

流程（每组各跑 ≥2 次）：
 1) 启动 → 勾选对应构建 → 进入同一 Custom Battle；
 2) 记录开局编队构成/命令（截图 + 文本）；
 3) 全程不下新命令（观察 AI 自主行为），录制 60~90 秒；
 4) 下达同一命令序列（前进 → 冲锋 → 停止 → 后退），每步录制 20 秒；
 5) 战斗结束前保存日志片段；战斗结束后一次收尾保存遥测。
```

**规模**：先 50 人验证功能（A1–A6），再 500 人验证性能（A7）。

---

## 4. 只读诊断计数（§九）与判定口径

实现：`CombatBrain/Ai/DiagnosticCounters.cs`（**只做整数累加与索引/时间簿记，不读新信息、不写游戏、不改变行为**），
`MissionStateLifecycle` 在 Mission 初始化/收尾幂等复位；每 5s 输出一次（复用既有汇总节拍，未新增状态机）。

日志形态：

```text
[Diag] FormationWrites=<n> AgentMovement=0 AgentBlock=0 AgentTarget=0 AgentWeapon=0 AgentStatsOverride=0
[Diag] FrameReleaseWrites=<n> distinctAgents=<m> first=<s> last=<s> repeatWrite=<true|false>
```

| 计数 | 语义 | 判定 |
|---|---|---|
| ① `FormationWrites` | 编队三域命令落地次数（唯一执行器） | 应 > 0（对照项：证明 Formations 级仍在工作，未过度退役） |
| ② `AgentMovement` | `Agent.SetMovementDirection` | **必须恒 0**（§五.2/§五.7 退役） |
| ③ `AgentBlock` | `Agent.EnforceShieldUsage` | **必须恒 0**（§五.3 退役） |
| ④ `AgentTarget` | `Agent.SetTargetAgent` | **必须恒 0**（§五.6 退役） |
| ⑤ `AgentWeapon` | `Agent.TryToWieldWeaponInSlot` | **必须恒 0**（§五.6 退役） |
| ⑥ `AgentStatsOverride` | `AgentDrivenProperties` 写入 | **必须恒 0**（§五.1 退役） |
| ⑦ FrameRelease 单列 | 写入次数 / 去重 agent 数 / 首末时刻 / 是否出现同一 agent 重复写 | 写入次数 ≈ 去重 agent 数（一次性 latch）；`repeatWrite=false`；若出现重复写或时间分布呈周期振荡 → **回报 Captain 再议**（不得自行静默退役或放宽） |

静态侧已由 `OfflineTests/AgentWriteStaticChecks.ps1`（**68 条断言**，t10 终态：退役文件「必须不存在」硬断言 + 13 个退役类型零引用 + 防过度退役断言）证明 ②–⑥ 的生产写入点**不存在**；
本节的运行期计数是「零写入」的**运行期**证据（静态与运行期不同权）。

---

## 5. 阵框释放专项（K1/D1 产品裁决的实证要求）

产品裁决：**保留**，定性为「编队级统一接战政策」（引擎无编队级批量入口，逐兵调用属 API 形态）。
v1.4.8 API 核验与恢复边界证据见 `docs/agent-control-surface-audit.md` §0b.3。

运行期需确认（读 `[Diag]` + `[F7]`/`[Bench]` 日志 + 目视）：

1. **刷新频率**：`FrameReleaseWrites / distinctAgents ≈ 1`（每兵至多一次）；同一 agent 在整场中不出现第二次写入。
2. **无重复抢写**：`repeatWrite=false`；且不存在「0.5s 节拍 × 冷却 5s」被绕过的迹象（例如同一 agent 多次出现）。
3. **恢复语义**：目视确认散开后士兵在编队重排/再集合时**重新整齐**（原版 AI 每帧重建帧框的证据），不出现「永久散开」。
4. **振荡判定**：若时间分布桶出现规律性峰值（与 0.5s 节拍同频）或目视到「散开-收拢-散开」循环 → 记录并回报 Captain。

---

## 6. 玩家队豁免（§六）验收

在玩家队豁免**开启**（默认）时：

1. `[Diag]` 的 ②–⑥ 恒 0（全控制面零主动写入）；
2. 玩家队士兵动作与 A 组（原版）无明显差异；
3. 玩家队编队不出现 CB 下发的命令（`[Cps2Observation]` / 命令来源日志中玩家队零 CB 来源）。

在玩家队豁免**关闭**时（回归项）：
4. 仍不得复活任何已退役的逐兵系统：②–⑥ 仍恒 0（关闭豁免只允许军队级/编队级接管）。

---

## 7. 记录模板（每次 A/B 采集填写）

```yaml
date:            # 采集日期
build_A:         # 原版 / 基线构建标识（DLL SHA256）
build_B:         # worktree 构建标识（DLL SHA256）
scenario:        # 地图 / 兵种 / 阵型 / 命令序列
size:            # 50 / 500
A_observations:  # A1-A6 逐项：通过 / 不通过 / 存疑（附时间码）
B_observations:  # A1-A6 逐项
diag_B:          # [Diag] 末次六类计数 + FrameRelease 单列
bench_B:         # [Bench] 帧时 / AI 段（500 人）
cps_B:           # Route A / 仲裁日志摘要（异常与否）
verdict:         # pass / pass-with-runtime-debt / needs_revision（不许写无条件 pass）
debts:           # 未完成项与原因
```

---

## 8. 未执行前的结论纪律

- 本文件是**步骤与口径**，不是验收结果；在完成 §3 采集前，退役任务的可验收结论只能是
  `pass-with-runtime-debt`（离线 + 静态证据充分、运行期观感未验）或 `blocked`，**不得**写成无条件 `pass`。
- 若 A7（500 人性能）出现劣化，或阵框释放出现 §5.4 的振荡迹象，应回报 Captain 决定回滚点
  （回滚点＝删除 `CombatAiMissionLogic.TickFrameRelease` 的 `agent.SetFormationFrameDisabled()` 调用行，保留其余退役成果）。
