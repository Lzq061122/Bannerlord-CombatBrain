namespace CombatBrain.Ai
{
    using CombatBrain.Intent;

    // N2 L0 战场事件层（NarrativeEvents）—— 纯逻辑（零 TaleWorlds 依赖，可离线测）。
    // 5 类战场事件的边沿检测（§3 文案表）：消费已算好的 SituationVector（EnemCav/FlankEx/Gap）、
    // 敌方意图信念（TryReadDominant 主导 + 置信）、敌方存活衰减代理（routRatio），不加新 tick。
    // 边沿纪律：只播 无→有 的跨越瞬间；进/出双阈值迟滞带防抖；持续不变不重播（锁存 Active）。
    // 输入全部=既有公开态势与既有信念（反作弊闸：零新增禁读信息）。
    // 事件映射（NarrativeEventKind → FeedbackText.BattlefieldEvent(kind) 文案 + 颜色）：
    //   E1 骑兵威胁   v.EnemCav ≥0.55 进 / 回 <0.40 出    → 危机红
    //   E2a 缺口机会  v.Gap ≥0.60 进 / 回 <0.45 出         → 优势绿（Gap 高=敌阵缺口，已核实）
    //   E2b 侧翼暴露  v.FlankEx ≥0.60 进 / 回 <0.45 出     → 危机红
    //   E3 敌溃败     routRatio ≥0.30 且 持续衰减（斜率）进 / 回 <0.20 出 → 优势绿
    //   E4 预测冲锋   主导=FrontalAttack 且 置信≥0.5 且 EnemCav≥0.4 边沿 → 特殊紫
    // E5 战术转折不走本层（订阅 IntentSwitched 跨战略大类跳变，见胶水层）。
    public enum NarrativeEventKind
    {
        None = 0,
        E1CavalryThreat = 1,  // 骑兵威胁
        E2aGapOpportunity = 2, // 缺口机会
        E2bFlankExposed = 3,   // 侧翼暴露
        E3EnemyRout = 4,       // 敌溃败
        E4PredictCharge = 5,   // 预测冲锋
        E5TacticalShift = 6    // 战术转折（N3：仅入事件缓冲供原因 toast；播报在胶水层 E5 分支）
    }

    /// <summary>事件锁存（进/出双阈值 + 上次状态；E3 额外记上拍 routRatio 算斜率）。</summary>
    public struct NarrativeLatch
    {
        public bool Active;      // 当前是否在事件内（防持续重播）
        public float PrevRout;   // E3 专用：上拍 routRatio（持续衰减斜率）
    }

    // N3 L1 事件缓冲槽（§4）：kind + 触发时刻。纯数据结构，零 TaleWorlds 依赖（可离线测）。
    public struct NarrativeEventSlot
    {
        public NarrativeEventKind Kind;
        public float Time;       // 触发时刻（战斗累计时间，秒）

        public NarrativeEventSlot(NarrativeEventKind kind, float time)
        {
            Kind = kind;
            Time = time;
        }
    }

    // N3 L1 事件缓冲（ring 2，§4）：最近 2 条 L0 事件，TTL 3.5s。
    // 意图切换后 PeekFresh 返回未过期最近事件（→ 原因第二条 toast）；无事件/过期 → None。
    public struct NarrativeEventBuffer
    {
        public const float TtlSeconds = 3.5f; // §4：原因时效窗

        public NarrativeEventSlot Slot0;      // 最近（新写入覆盖 Slot1）
        public NarrativeEventSlot Slot1;

        public bool IsEmpty
        {
            get { return Slot0.Kind == NarrativeEventKind.None && Slot1.Kind == NarrativeEventKind.None; }
        }

        /// <summary>写入事件（ring 2：Slot1 ← Slot0 ← 新事件）。</summary>
        public void Push(NarrativeEventKind kind, float now)
        {
            if (kind == NarrativeEventKind.None) return;
            Slot1 = Slot0;
            Slot0 = new NarrativeEventSlot(kind, now);
        }

        /// <summary>读取最近未过期事件（now - time ≤ TTL；过期视为无事件）。ring 中 Slot0 恒最新——
        /// Slot0 过期时 Slot1 必然更旧更过期，故只查 Slot0（原因 toast 只需「最近一次 L0 事件」）。</summary>
        public NarrativeEventSlot PeekFresh(float now)
        {
            if (Slot0.Kind != NarrativeEventKind.None && now - Slot0.Time <= TtlSeconds)
                return Slot0;
            return new NarrativeEventSlot(NarrativeEventKind.None, -1f);
        }

        /// <summary>清理（编队解散/重置时调用，防滞留）。</summary>
        public void Clear()
        {
            Slot0 = new NarrativeEventSlot(NarrativeEventKind.None, -1f);
            Slot1 = new NarrativeEventSlot(NarrativeEventKind.None, -1f);
        }
    }

    public static class NarrativeEvents
    {
        public const float E1In = 0.55f, E1Out = 0.40f;   // 骑兵威胁 进/出
        public const float E2In = 0.60f, E2Out = 0.45f;   // 缺口/侧翼 进/出
        public const float E3In = 0.30f, E3Out = 0.20f;   // 敌溃败 进/出
        public const float E3SlopeMin = 0.05f;            // 敌溃败最小持续衰减斜率（0.5s 拍）
        public const float E4Confidence = 0.5f;           // 预测冲锋：置信阈值
        public const float E4EnemCav = 0.4f;              // 预测冲锋：敌骑集结度下限

        /// <summary>
        /// 单拍边沿检测：返回本拍触发的事件（None=无）；锁存经 ref 更新。
        /// dominantFrontal = TryReadDominant 主导意图是 FrontalAttack；dominantConf = 主导置信；
        /// latchE4 = E4 边沿锁存（独立 bool，无迟滞带回退语义）。
        /// 只播 无→有 跨越；迟滞带（进/出双阈值）防抖；持续不变不重播。
        /// </summary>
        public static NarrativeEventKind Tick(
            float enemCav, float flankEx, float gap, float routRatio,
            bool dominantFrontal, float dominantConf,
            ref NarrativeLatch latchE1, ref NarrativeLatch latchE2a,
            ref NarrativeLatch latchE2b, ref NarrativeLatch latchE3, ref bool latchE4)
        {
            // E1 骑兵威胁（危机红）：EnemCav ≥0.55 进 / 回 <0.40 出
            if (enemCav >= E1In && !latchE1.Active)
            {
                latchE1.Active = true;
                return NarrativeEventKind.E1CavalryThreat;
            }
            if (enemCav < E1Out) latchE1.Active = false;

            // E2a 缺口机会（优势绿）：Gap ≥0.60 进 / 回 <0.45 出（Gap 高=敌阵缺口，已核实）
            if (gap >= E2In && !latchE2a.Active)
            {
                latchE2a.Active = true;
                return NarrativeEventKind.E2aGapOpportunity;
            }
            if (gap < E2Out) latchE2a.Active = false;

            // E2b 侧翼暴露（危机红）：FlankEx ≥0.60 进 / 回 <0.45 出
            if (flankEx >= E2In && !latchE2b.Active)
            {
                latchE2b.Active = true;
                return NarrativeEventKind.E2bFlankExposed;
            }
            if (flankEx < E2Out) latchE2b.Active = false;

            // E3 敌溃败（优势绿）：routRatio ≥0.30 且 持续衰减（斜率）才进 / 回 <0.20 出
            float slope = routRatio - latchE3.PrevRout;
            latchE3.PrevRout = routRatio;
            if (routRatio >= E3In && slope >= E3SlopeMin && !latchE3.Active)
            {
                latchE3.Active = true;
                return NarrativeEventKind.E3EnemyRout;
            }
            if (routRatio < E3Out) latchE3.Active = false;

            // E4 预测冲锋（特殊紫）：主导=FrontalAttack 且 置信≥0.5 且 EnemCav≥0.4 边沿
            if (dominantFrontal && dominantConf >= E4Confidence && enemCav >= E4EnemCav && !latchE4)
            {
                latchE4 = true;
                return NarrativeEventKind.E4PredictCharge;
            }
            if (!dominantFrontal || dominantConf < E4Confidence || enemCav < E4EnemCav)
                latchE4 = false;

            return NarrativeEventKind.None;
        }
    }
}
