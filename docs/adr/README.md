# CombatBrain ADR — Architecture Decision Records

> status: active
> authority: canonical-bootstrap (Phase C2 建立)
> 依据：CombatBrain Knowledge Architecture — Phase C2。
> 本文件定义 **ADR 准入标准**。ADR 正文在 C3 起按需写入 `docs/adr/NNNN-<slug>.md`。

---

## 1. Role Contract

- **Authority axis**：NORMATIVE（对已决决策的存档）。
- **职责**：记录**持久决策** —— 未来维护者需要知道：决定了什么、为什么、拒绝了哪些替代方案、后果。
- **必须不含**：把每个任务 / bug 修复 / 实现卡都变成 ADR。
- **维护 owner**：Captain / Integration Owner（ADR 采纳需 rationale，等同架构级变更）。

## 2. ADR 准入判据（全部满足才写 ADR）

1. **持久性**：决策影响未来多个迭代/维护者的理解或选择；
2. **有取舍**：存在被拒绝的替代方案，且未来可能重新被提出；
3. **不可从代码自明**：光读源码无法还原「为何如此」；
4. **非纯施工**：不是普通实现卡/bug 修复/断言调整。

不满足 → 属于 evidence（做完了什么）或 backlog（待办），不写 ADR。

## 3. 状态

`proposed` → `accepted`（或 `rejected`）→ `superseded`（注明被哪个 ADR/决策取代）。

## 4. 推荐模板（C3 起用）

```text
# ADR-NNNN: <标题>

status: <proposed|accepted|rejected|superseded>
date: <yyyy-mm-dd>
deciders: <谁>

## Context        （背景与问题）
## Decision       （决定）
## Alternatives   （拒绝的替代方案 + 拒绝理由）
## Consequences   （后果 / 代价 / 后续影响）
## Supersedes/Superseded by （相关决策链）
```

## 5. C3 候选 ADR（仅登记，C2 不写正文；CPS 相关等待 GO/ADR 闭环）

- CPS 命令仲裁：唯一仲裁 + 双路线 PoC 的取舍；
- CPS 条件 5 替代路径：路线 B（租约战斗中段样本）→ 路线 A（忠实执行证明）的正式转向；
- 反作弊闸语义边界（两层可关 / 第三层永不开放）统一表述；
- TerrainStub 合成假地形 → TerrainSampler 的删除决策；
- 工具教训类（MCM `Dropdown<string>` 离线加载限制、Harmony 参数名绑定）—— 符合准入判据才转 ADR，否则入 lessons-log。
