namespace CombatBrain.Battle
{
    using CombatBrain.Intent;

    // ATC-1 修订版队级战局阶段机（BattlePhaseFSM）—— 纯逻辑（零 TaleWorlds 依赖，可离线测）。
    // 五态上下文：Deployment → Advance → Engage → Exploit → Retreat（规格 §3.1）。
    // 本卡只发事件/播报，不下任何命令（规格 §7 objective）。
    // 六条强制修订（§7，逐条落实）：
    //   1. 基线=649 PASS 只增不减（旧 B1 文档 573 为过时值，不引用）；
    //   2. OwnSurvival 必须来自 TeamState.InitialCount/当前公开聚合的真实存活比——输入即 ownSurvival
    //      （胶水层算 aliveSelf/InitialCount），本层绝不用 v.Moral 代替（注释声明来源契约）；
    //   3. Retreat 第一版为终态：ownSurvival < RetreatSurvival 即锁死 Retreat，无任何『存活率回升→Advance』
    //      回退转移（未来 Reinforcement/Reorganized 信号另立转移，本卡不建）；
    //   4. 所有驻留判断用 TimeInPhase（相位内时间），胶水层按 now - PhaseEnteredAt 传入，绝不用 BattleElapsed；
    //   5. Stalemate 不制造自循环伪转移：stalemate 输入不触发任何转移（保持当前相），同态 Changed=false
    //      且不发事件/播报（胶水层按 Changed 门控）；
    //   6. EnemyWeakening 只能作为 Engage→Exploit 的一个条件：需同时满足 enemySurvival < 阈值 或
    //      intent==Pursue，避免战斗初期短促掉人直接进 Exploit。
    // 最短驻留：每相 timeInPhase < MinDwellSeconds 不得转移（防抖 + 相位内时间语义）。
    // 反作弊闸：输入=己方存活聚合（AliveAgents/InitialCount 公开）、敌存活聚合、既有意图与僵局判定，
    //   零新增禁读。
    public enum BattlePhase
    {
        Deployment = 0,
        Advance = 1,
        Engage = 2,
        Exploit = 3,
        Retreat = 4
    }

    /// <summary>状态机输入（全部纯数值；语义见字段注释与强制修订对应）。</summary>
    public struct BattlePhaseInput
    {
        public float OwnSurvival;     // 己方真实存活比 aliveSelf/InitialCount（强制修订2：禁 v.Moral）
        public float EnemySurvival;   // 敌存活比 aliveEnemy/EnemyInitial（强制修订6）
        // Stalemate：僵局信号（强制修订5：Stalemate 不触发转移）。atc1 nits 收口：本字段为契约占位——
        //   当前实现通过「不消费 Stalemate 即不因僵局转移」自然满足修订5（Advance→Engage 无条件转移由
        //   EnemyContact 契约字段承载未来门控），故无读取点；作为输入契约字段保留并注明语义，禁止删除，
        //   若未来 Advance→Engage 引入 EnemyContact 门控时 Stalemate 可作为「不推进」信号接入。
        public bool Stalemate;
        public bool EnemyContact;     // 敌接触信号（atc1 nits 收口：可选输入契约字段）。当前默认 false 保持现状
                                      //   （Advance→Engage 无条件转移语义不变，见 Advance() 注释）；预留供胶水层
                                      //   未来以真实敌接触事件（意图切换/编队距敌阈值）驱动，消费前不得删除。
        public bool EnemyWeakening;   // 敌弱化信号（强制修订6：Engage→Exploit 必要条件之一）
        public IntentType Intent;     // 当前意图（Pursue 触发 Exploit，强制修订6）
        public float TimeInPhase;     // 相位内时间（强制修订4：绝不用 BattleElapsed）
    }

    /// <summary>状态机输出：Phase=当前相；Changed=本拍是否发生转移（同态=false，不发事件/播报）。</summary>
    public struct BattlePhaseResult
    {
        public BattlePhase Phase;
        public bool Changed;
    }

    public static class BattlePhaseFSM
    {
        // 阈值（设计源：规格 §7 + 既有态势向量口径；纯常量可离线断言）
        public const float MinDwellSeconds = 5f;   // 最短驻留：相位内 ≥5s 才允许转移（强制修订4）
        public const float AdvanceSurvival = 0.7f; // Deployment→Advance：己方存活 ≥70%（伤亡<30% 可推进）
        public const float RetreatSurvival = 0.4f; // 任意相→Retreat：己方存活 <40%（终态，强制修订3）
        public const float ExploitEnemySurvival = 0.4f; // Engage→Exploit：敌存活 <40% 或 Pursue（强制修订6）

        /// <summary>
        /// 单拍推进：返回下一相位与是否变更。同态 Changed=false（不发事件/播报，强制修订5）。
        /// Retreat 为终态：一旦进入不再转出（强制修订3）。
        /// </summary>
        public static BattlePhaseResult Advance(BattlePhase current, BattlePhaseInput input)
        {
            var r = new BattlePhaseResult { Phase = current, Changed = false };

            // 终态守卫：Retreat 锁死（强制修订3；未来 Reinforcement 信号另立转移）
            if (current == BattlePhase.Retreat) return r;

            // 任意相 → Retreat：己方存活跌破阈值（终态，先于其它转移判定）
            if (input.OwnSurvival < RetreatSurvival)
            {
                r.Phase = BattlePhase.Retreat;
                r.Changed = current != BattlePhase.Retreat;
                return r;
            }

            // 最短驻留守卫：相位内时间不足不转移（强制修订4；防抖）
            if (input.TimeInPhase < MinDwellSeconds) return r;

            switch (current)
            {
                case BattlePhase.Deployment:
                    // Deployment → Advance：己方存活 ≥70%（开战整备完成可推进）
                    if (input.OwnSurvival >= AdvanceSurvival)
                    {
                        r.Phase = BattlePhase.Advance;
                        r.Changed = true;
                    }
                    break;

                case BattlePhase.Advance:
                    // Advance → Engage：敌接战（存活比下降至 <100% 即已接触，或僵局解除后接战）
                    // 简化（现状语义，零变化）：任何非僵局且己方仍存活的推进相视为已接战——Engage 进入条件=
                    // 非 Retreat 守卫已通过 + 存活正常 + 非 Deployment。atc1 nits 收口：EnemyContact 输入契约
                    // 字段默认 false 不参与本转移（无条件转移保持）；胶水层未来以真实敌接触信号驱动时再接入。
                    r.Phase = BattlePhase.Engage;
                    r.Changed = true;
                    break;

                case BattlePhase.Engage:
                    // Engage → Exploit：敌弱化 且 (敌存活<40% 或 意图 Pursue)（强制修订6）
                    if (input.EnemyWeakening
                        && (input.EnemySurvival < ExploitEnemySurvival || input.Intent == IntentType.Pursue))
                    {
                        r.Phase = BattlePhase.Exploit;
                        r.Changed = true;
                    }
                    // Stalemate 不触发转移（保持 Engage，不发事件，强制修订5）——else 分支留空
                    break;

                case BattlePhase.Exploit:
                    // Exploit → Retreat：存活跌破阈值（上方统一守卫已处理）；无其它出边（追击至结束或终态）
                    break;
            }
            return r;
        }
    }
}
