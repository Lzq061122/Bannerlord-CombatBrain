# CPS-3R Route A — 诊断构建运行期采样记录（2026-09-15）

> 性质：**DESCRIPTIVE scoped claims**（诊断构建 `a24e921` 上的只读探针运行期证据）。
> 目的：判定「`ExternalReplaced` 后引擎是否/为何不再回调 Route A 行为」——此前为 Inferred，现由 `[RouteASilence]` 直接观测。
> 现行验证态唯一权威 = `docs/knowledge/CURRENT_STATE.md`；条件 5 / CPS-4 / GO **不由本件判定**。
> 依据：`MBmod/CombatAI/tools/CB-REPAIR-0006-部署前核对-2026-09-15.md`（§7 部署执行记录 + §5 绑定模板）；探针来源 = CB-REPAIR-0005（`a6a4ec5`）与其 r1 异常边界隔离（`a24e921`）。

---

## 1. 绑定（采样的唯一前提）

| 项 | 值 |
|---|---|
| 构建 | **诊断构建 `a24e921`**（= r3 `395fe13` + CB-REPAIR-0005 探针 + r1 输出边界隔离）；**不得冒充 r3** |
| 部署 DLL | `Modules\CombatBrain\bin\Win64_Shipping_Client\CombatBrain.dll`，SHA256 `ADCC641E04231DD628BB80F1FAFF887062D81C76894467D83D394A682AAF397C`，231,424 B（2026-09-15 部署，src=dst MATCH） |
| r3 可回滚备份 | `E:\MBmod\_backups\CombatBrain-r3-395fe13-7E5FC25A.dll`（`7E5FC25A…`） |
| 观测 | 每次 mission 均出现 `[SetOrderObserver] 观测已挂载（Harmony patch OK, id=com.combatbrain.setorderobserver, prefixes=3）` |
| MCM | `Cps3Mode = E_RouteA`（`Cps3ObserverEnabled` 随 E 武装；有 `[RouteA] run=` 行产出即证明 Route A 在跑） |
| 快进 | **未使用**（4 场 mission/wall 比值均 ≈1.0：2954.6s/2954s、1154.13s/1158s、861.97s/864s） |
| 异常 | 本窗口 44,497 行内 **`Exception` 命中 0** |

## 2. 本窗口的 4 场 mission（2 个 run）

| # | run / session | agents(首) | 开战编队（观测） | BattleDuration | `selected/applied/replaced` | 覆盖后的窗口结局 |
|---|---|---|---|---|---|---|
| M1 | `14ff27b0` s1 | 2032 | Team0 717（步178/射180/骑90/骑射179/骑90）、Team1 696（步218/射239/骑射239） | 114.94s | 0 / 0 / 0 | 无覆盖 |
| M2 | `14ff27b0` s2 | 1949 | Team0 650、Team1 629（混编） | **2954.6s** | 470 / 4 / **1** | `t=2946.297 ExternalReplaced` **同拍 `Exit(MissionEnding)`** ⇒ 探针已 arm 但无采样拍 |
| M3 | `14ff27b0` s3 | 1949 | 同上 | 1154.13s | 433 / 8 / **1** | `t=220.533` 覆盖 → 探针 ×4（1–4s）→ `t=225.008 Exit(Disabled)`（用户侧关闭/切模式） |
| M4 | `6e2a87d2` s1 | 1949 | 同上 | 861.97s | 547 / 6 / **1** | `t=280.533` 覆盖 → 探针 ×**10**（1–9s + `final=1` @9.502s）→ `t=290.035 Exit(SelectionTimeout)`（Δ 自覆盖 9.502s / 自末次选中 10.002s） |

> 说明：run id 为进程级（一次游戏进程一个 run），`session` 每场 mission 递增 ⇒ `14ff27b0` 含 s1/s2/s3 三场。

## 3. 探针行（`[RouteASilence]`，共 14 条，全部同形）

| 场次 | 采样时刻（mission time） | `silentFor` / `sinceReplaced` | `activeIdx` | `activeType` | `aiSame` / `aiCtl` | Route A 侧 `weights` | units |
|---|---|---|---|---|---|---|---|
| M3 | t=221.533 / 222.533 / 223.533 / 224.533 | 1.5→4.5s / 1.0→4.0s | **0** | **`BehaviorCharge`** | 1 / 1 | `0.00/10.00/0.00/0.00` | 63 |
| M4 | t=281.533 / 282.533 / 283.533 | 1.5→3.5s / 1.0→3.0s | **0** | **`BehaviorCharge`** | 1 / 1 | `10.00/0.00/0.00/0.00` | 107→106 |
| M4 | t=284.533 … 289.534（probe 4–9） | 4.5→9.5s / 4.0→9.0s | **0** | **`BehaviorCharge`** | 1 / 1 | `0.00/10.00/0.00/0.00` | 106 |
| M4 | t=290.035（**`final=1`**，紧接 `Exit(SelectionTimeout)`） | 10.002s / 9.502s | **0** | **`BehaviorCharge`** | 1 / 1 | `0.00/10.00/0.00/0.00` | 106 |

字段读法：`activeIdx=0` ⇒ 引擎 `Formation.AI.ActiveBehavior` **不是**四个 Route A 行为中的任何一个；`activeType=BehaviorCharge` ⇒ 胜出者是**原生** charge 行为；`aiSame=1` ⇒ FormationAI 对象未被替换；`aiCtl=1` ⇒ 编制仍受 AI 控制；`weights` 始终**恰有一个行为 = 10.00、其余 0.00** ⇒ CB 侧权重完好（M4 中期由 Hold 切到 Advance，符合当时的意图更新）。

## 4. 判定（Observed）

1. **覆盖前** CB 行为确为胜出者：`BehaviorSelected` 连续到覆盖前 0.5s（M4 `lastSelectedAt=280.033s`、覆盖在 280.533s；M3 `220.033s` / 220.533s）。
2. **覆盖后整个交付窗口内，引擎的胜出者都是原生 `BehaviorCharge`**（14/14 采样拍），CB 的 Route A 行为一次都没被选中 ⇒ `armed` 的单次边沿重试**没有任何兑现机会**（`edgeRetries=0`），最终按期限 `Exit(SelectionTimeout)` 释放（M4 Δ=9.502s），或由用户侧 `Disabled` / mission 结束提前终止（M3/M2）。
3. **排除三类替代解释**：行为表/对象被换（`aiSame=1`）、资格丢失（`aiCtl=1`、且未出现 `Exit(EmptyOrInvalid/LostAIControl)`）、CB 权重被清零（`weights` 始终有一个 10.00）。
   ⇒ **机制结论（本轮由观测确立，替换此前的 Inferred）**：`ExternalReplaced` 之后，原生 `BehaviorCharge` 成为并保持 `ActiveBehavior`；Route A 的行为不再被回调，故「覆盖恢复」这条语义在当前实现下只能靠**引擎重新把胜出权交回 CB 行为**——本轮 3 次覆盖窗口（M2/M3/M4）**均未发生**。
4. **候选解释（Inferred，未验）**：Route A 自身行为的有效权重上限为固定 `10 × GetAiWeight()`，而原生 `BehaviorCharge` 在"该冲锋"局面下的权重可能更高 ⇒ 一旦原生侧接管，CB 无法再赢回选择权。**该解释需对原生行为权重取证**（现有探针只报 `ActiveBehavior` 身份与 CB 侧权重，不报原生权重），本轮不据此改码。

## 5. 对验收口径的影响（供 Astra 裁定，本件不下结论）

- **S5 正向形态（`ExternalReplaced → EdgeRetry → OrderApplied`）**：r3 类构建上的覆盖事件现累计 **6 次**（r3 三场 §3i/§3j-s2/§3k + 本轮诊断构建三场 M2/M3/M4），**正向形态 0 次**；其中本轮三场由探针直接给出"胜出者是原生行为"的解释。r3 前构建曾出现 1 次（`ba84a11b`，Δ=5.00s）。
- ⇒ 请裁决：**接受离线证据 + 本观测机制**替代该运行期验收点（据此关 `CB-REPAIR-0004` 运行期验收）／**要求继续采样**（请给上限）／**要求另立卡重新设计覆盖恢复语义**（如由 CB 侧主动、具名、有界地重取）。
- **诊断构建样本能否计入 r3 运行期验收**：本轮样本来自 `a24e921`（r3 + 只读探针 + 单点异常边界隔离）。探针为**纯增量只读**、异常边界已隔离并经 7 条离线断言 + 反向验证；但"DLL 不同 ⇒ 是否等价"须由 Astra 认定（对应汇报件 §5-7）。

## 6. 归档（研究用途）

| 文件 | 大小 / 行数 | SHA256 |
|---|---|---|
| `logs/CombatBrain-2026-09-15-full-1604.log` | 57,436,833 B / 452,264 行 | `0E1EC859725274E3C8CF3723E223A936ED12011D55CC6F071E1D326083D17045` |
| `logs/run-14ff27b0-20260915-diagnostic.log` | 656,089 B / 2,081 行 | `605AD7759644805975B04BF9B8E193981FB208F483BBD9B5508C813D9A1B14C8` |
| `logs/run-6e2a87d2-20260915-diagnostic.log` | 394,157 B / 1,292 行 | `52F9772EFD3A13B69D550BBBB552BC60113BB1A57EFDA0EDC3863C49B839E8C5` |

## 7. 复现命令（只读）

```powershell
# 探针行（本件核心证据）
Select-String $env:TEMP\CombatBrain.log -Pattern '\[RouteASilence\]'
# 某场覆盖前后的选中流与退出
Select-String $env:TEMP\CombatBrain.log -Pattern 'run=6e2a87d2' |
  Where-Object { $_.Line -match 'event=(BehaviorSelected|ExternalReplaced|EdgeRetry|Exit|Restore)\b' } |
  Select-Object -Last 20
# 汇总
Select-String $env:TEMP\CombatBrain.log -Pattern '\[RouteA\] run=' | Select-Object -Last 4
```

## 9. 等价性绑定（Astra 2026-09-15 裁定 2 要求的字段清单）

> 裁定 2：诊断样本**可计入** r3 运行期验收，限定为「**r3 的诊断衍生构建**」，绑定必须含下列字段；**不得称同一 DLL、不得外推原生权重**。

| # | 绑定项 | 值 |
|---|---|---|
| 1 | **构建差分** | `395fe13`（r3）→ `a24e921`（诊断衍生构建），链上插入 `38962f5`、`2ba07d6`、`8f8538f`（均为文档/报告类提交）；源码差异面 = 只读探针 `[RouteASilence]`（CB-REPAIR-0005，`a6a4ec5`）+ 输出边界隔离（CB-REPAIR-0006 r1，`a24e921`） |
| 2 | **DLL SHA（部署态）** | `ADCC641E04231DD628BB80F1FAFF887062D81C76894467D83D394A682AAF397C`（231,424 B；src=dst **MATCH=True**）；**≠ r3 DLL** `7E5FC25A0EF0559D718B0D5E42994141846291501E3342F94E6C0B1F713FD8D6`（228,352 B） |
| 3 | **run / session** | `run=14ff27b0` s1 / s2 / s3 ＋ `run=6e2a87d2` s1（共 4 场 mission） |
| 4 | **同配置** | 自定义战斗（`MCM Cps3Mode = E_RouteA`、`Cps3ObserverEnabled` 随 E 武装；每场开局均有 `[SetOrderObserver] 观测已挂载 … prefixes=3`）；**未使用快进**；非部署文件 `SubModule.xml` `67DE43EC…` / `ModuleData/opponent_priors.json` `6697CBD0…` 部署前后 src=dst MATCH（配置面零变化） |
| 5 | **离线门（1327 / 20 / 4=4）** | OfflineTests **1327 PASS / 0 FAIL**（r3 基线 1297 ⇒ +30）；`RouteAStaticChecks` **20 项 PASS**；生产 setter 站点去 trivia **4 = 4 无新增** |
| 6 | **异常 0** | 采样窗口 44,497 行内 `Exception` 命中 **0**（§1） |
| 7 | **回滚事实（限定语）** | 采样后已回滚回 r3（dst SHA = 备份 SHA `7E5FC25A…`；部署 DLL 内 `[RouteASilence]` 字面量 = **False**）⇒ 本节样本属于**已完成并已卸载**的诊断衍生构建 |

**限定（不得扩大）**：① 样本只证明「该诊断衍生构建上」的机制观测，**不得**表述为 r3 DLL 同字节样本；② 不得据该观测外推原生行为权重的数值（候选解释仍为 Inferred，需另取证）；③ 本节的计数与 SHA 事实来源 = `MBmod/CombatAI/tools/CB-REPAIR-0006-部署前核对-2026-09-15.md`（§7 部署执行、§8 采样与回滚）与本件 §1/§2/§6。

## 10. 不得扩大结论

1. 本轮样本来自**诊断构建**，不等于 r3 DLL 的样本；**等价性的认定 = Astra 2026-09-15 裁定 2**（可计入，限定为「r3 的诊断衍生构建」，绑定字段见 §9）。
2. `activeType=BehaviorCharge` 证明"原生行为在胜出"，**不证明**原生权重的具体数值（候选解释见 §4.4，未验）。
3. 单次覆盖窗口只有 10s（交付期限）；"窗口内未被选中"**不等于**"永久不可达"。
4. 本件**不判**条件 5 / CPS-4 / GO，也不据此关闭任何返修卡（S5 与关闭条件由 Astra 裁定的 6/7 与本闭环清单承接）。
