using System;
using System.Text;

namespace CombatBrain.Telemetry
{
    // T39-01 战术异常检测（6 类反模式）—— 纯逻辑零依赖（依赖 T38 计数器 + T1 完整度）。
    // 语义：在 BattleTelemetry 之上做 6 个判定并计数，让「新版本异常数下降」可量化。
    // 输入全部为「可观察聚合」的原始数值（编队中心距/完整度/存活数/地形高度等），由胶水层
    //   （CombatAiMissionLogic）0.5s 低频采集喂入——本类不含任何游戏 API、不遍历个体（铁律禁全图/禁读个体）。
    // 阈值集中定义（源文档 §T39-01），注释来源章节。
    public enum AnomalyKind
    {
        CavalryWander = 0,              // ① 骑兵无任务游荡（距主战场>120m 且无目标，持续>10s）
        HorseArcherFrontalSuicide = 1,  // ② 骑射手正面送死（连续>=3 次正面攻击）
        ArcherFrontLine = 2,            // ③ 射手跑到前排（射手距敌 < 步兵距敌-10m，持续>5s）
        DisadvantagedForcedCharge = 3,  // ④ 劣势强制冲锋（战场劣势<-30 且完整度<50 却强制冲锋）
        HighGroundAbandon = 4,          // ⑤ 无理由放弃高地（地形优势>=20m 却离开且无新目标）
        LongStalemate = 5               // ⑥ 长期僵局（>120s 且伤亡<5% 且战线移动<20m）
    }

    // 异常检测输入快照（纯数值聚合，胶水层采集）。-1 = 无该兵种编队（哨兵值）。
    public struct AnomalyInput
    {
        public float Now;                    // 战斗累计秒
        public float CavalryDistFromMain;    // 骑兵编队距主战场中心（米，-1=无骑兵）
        public bool CavalryHasObjective;     // 骑兵是否有当前目标（近似：意图非 Reform/Retreat）
        public int HorseArcherFrontalCount;  // 骑射正面攻击累计（来自 BattleTelemetry）
        public int HorseArcherFlankCount;    // 骑射侧翼攻击累计（来自 BattleTelemetry）
        public float ArcherDistToEnemy;      // 射手编队距敌（米，-1=无射手）
        public float InfantryDistToEnemy;    // 步兵编队距敌（米，-1=无步兵）
        public float BattleAdvantage;        // 战场优势（-100~100，正=优势）
        public float FormationIntegrity;     // 编队完整度（0~100，t65）
        public bool ForcedCharge;            // 本拍是否发生强制冲锋（事件，未接线前=false）
        public float TerrainAdvantage;       // 地形高度优势（米，己方-敌方，>=20 视为高地）
        public bool HasNewObjective;         // 是否有新的战术目标（高地放弃判定用）
        public float CasualtyRate;           // 伤亡率（0~1）
        public float FrontlineMovement;      // 战线移动距离（米）
    }

    public sealed class AnomalyDetector
    {
        public const int KindCount = 6;

        private static readonly string[] Names =
        {
            "AnomalyCavalryWander", "AnomalyHorseArcherFrontalSuicide", "AnomalyArcherFrontLine",
            "AnomalyDisadvantagedForcedCharge", "AnomalyHighGroundAbandon", "AnomalyLongStalemate"
        };

        // ---- 阈值集中定义（源文档《骑马与砍杀 II》战术 AI Mod.md §T39-01）----
        public const float CavalryWanderDistance = 120f;   // ① DistanceFromMainBattle > 120m
        public const float CavalryWanderDuration = 10f;    // ① 持续 > 10s
        public const int HorseArcherFrontalThreshold = 3;  // ② 连续 >=3 次正面攻击
        public const float ArcherFrontGap = 10f;           // ③ ArcherDist < InfantryDist - 10m
        public const float ArcherFrontDuration = 5f;       // ③ 持续 > 5s
        public const float DisadvantageThreshold = -30f;   // ④ BattleAdvantage < -30
        public const float IntegrityThreshold = 50f;       // ④ FormationIntegrity < 50
        public const float HighGroundThreshold = 20f;      // ⑤ TerrainAdvantage >= 20
        public const float StalemateDuration = 120f;       // ⑥ CombatDuration > 120s
        public const float StalemateCasualtyRate = 0.05f;  // ⑥ CasualtyRate < 5%
        public const float StalemateFrontlineMovement = 20f; // ⑥ FrontlineMovement < 20m

        private readonly int[] _counts = new int[KindCount];

        // 持续时间跟踪（-1 = 条件未激活）：①③
        private float _cavWanderStart = -1f;
        private float _archerFrontStart = -1f;

        // ② 连续正面攻击跟踪
        private int _prevFrontal;
        private int _prevFlank;
        private int _consecutiveFrontal;

        // ④ 强制冲锋边沿检测
        private bool _prevForcedCharge;

        // ⑤ 高地状态
        private bool _wasOnHighGround;

        // ⑥ 僵局单次触发守卫（条件解除后重新武装）
        private bool _stalemateFired;

        public int Count(AnomalyKind kind)
        {
            int i = (int)kind;
            if (i < 0 || i >= KindCount) return 0;
            return _counts[i];
        }

        public void Reset()
        {
            Array.Clear(_counts, 0, KindCount);
            _cavWanderStart = _archerFrontStart = -1f;
            _prevFrontal = _prevFlank = 0;
            _consecutiveFrontal = 0;
            _prevForcedCharge = false;
            _wasOnHighGround = false;
            _stalemateFired = false;
        }

        public void Tick(AnomalyInput i)
        {
            TickCavalryWander(i);
            TickHorseArcherFrontal(i);
            TickArcherFrontLine(i);
            TickDisadvantagedForcedCharge(i);
            TickHighGroundAbandon(i);
            TickLongStalemate(i);
        }

        // ① 骑兵无任务游荡：距主战场>120m 且无目标，持续>10s
        private void TickCavalryWander(AnomalyInput i)
        {
            bool cond = i.CavalryDistFromMain > CavalryWanderDistance && !i.CavalryHasObjective;
            if (cond)
            {
                if (_cavWanderStart < 0f) _cavWanderStart = i.Now;
                else if (i.Now - _cavWanderStart > CavalryWanderDuration)
                {
                    _counts[(int)AnomalyKind.CavalryWander]++;
                    _cavWanderStart = -1f; // 触发后重置，条件解除并重新进入才可再计
                }
            }
            else _cavWanderStart = -1f;
        }

        // ② 骑射手正面送死：连续 >=3 次正面攻击（发生侧翼攻击即打断连续）
        private void TickHorseArcherFrontal(AnomalyInput i)
        {
            if (i.HorseArcherFlankCount > _prevFlank) _consecutiveFrontal = 0; // 侧翼打断连续正面
            if (i.HorseArcherFrontalCount > _prevFrontal)
            {
                _consecutiveFrontal += i.HorseArcherFrontalCount - _prevFrontal;
                if (_consecutiveFrontal >= HorseArcherFrontalThreshold)
                {
                    _counts[(int)AnomalyKind.HorseArcherFrontalSuicide]++;
                    _consecutiveFrontal = 0; // 触发后重置防重复
                }
            }
            _prevFrontal = i.HorseArcherFrontalCount;
            _prevFlank = i.HorseArcherFlankCount;
        }

        // ③ 射手跑到前排：射手距敌 < 步兵距敌 - 10m，持续>5s
        private void TickArcherFrontLine(AnomalyInput i)
        {
            bool cond = i.ArcherDistToEnemy >= 0f && i.InfantryDistToEnemy >= 0f
                && i.ArcherDistToEnemy < i.InfantryDistToEnemy - ArcherFrontGap;
            if (cond)
            {
                if (_archerFrontStart < 0f) _archerFrontStart = i.Now;
                else if (i.Now - _archerFrontStart > ArcherFrontDuration)
                {
                    _counts[(int)AnomalyKind.ArcherFrontLine]++;
                    _archerFrontStart = -1f;
                }
            }
            else _archerFrontStart = -1f;
        }

        // ④ 劣势强制冲锋：战场劣势(<-30) 且完整度(<50) 却发生强制冲锋（边沿触发）
        private void TickDisadvantagedForcedCharge(AnomalyInput i)
        {
            bool edge = i.ForcedCharge && !_prevForcedCharge;
            if (edge && i.BattleAdvantage < DisadvantageThreshold && i.FormationIntegrity < IntegrityThreshold)
                _counts[(int)AnomalyKind.DisadvantagedForcedCharge]++;
            _prevForcedCharge = i.ForcedCharge;
        }

        // ⑤ 无理由放弃高地：地形优势>=20m 后离开且无新目标
        private void TickHighGroundAbandon(AnomalyInput i)
        {
            if (i.TerrainAdvantage >= HighGroundThreshold)
            {
                _wasOnHighGround = true;
            }
            else if (_wasOnHighGround)
            {
                if (!i.HasNewObjective) _counts[(int)AnomalyKind.HighGroundAbandon]++;
                _wasOnHighGround = false; // 无论是否有目标，离开高地后重置
            }
        }

        // ⑥ 长期僵局：>120s 且伤亡<5% 且战线移动<20m（单次触发，条件解除后重新武装）
        private void TickLongStalemate(AnomalyInput i)
        {
            bool cond = i.Now > StalemateDuration
                && i.CasualtyRate < StalemateCasualtyRate
                && i.FrontlineMovement < StalemateFrontlineMovement;
            if (cond && !_stalemateFired)
            {
                _counts[(int)AnomalyKind.LongStalemate]++;
                _stalemateFired = true;
            }
            else if (!cond)
            {
                _stalemateFired = false;
            }
        }

        public string ToJson()
        {
            var sb = new StringBuilder(160);
            sb.Append('{');
            for (int i = 0; i < KindCount; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('"').Append(Names[i]).Append("\":").Append(_counts[i]);
            }
            sb.Append('}');
            return sb.ToString();
        }

        public string Summary()
        {
            var sb = new StringBuilder(160);
            for (int i = 0; i < KindCount; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(Names[i]).Append('=').Append(_counts[i]);
            }
            return sb.ToString();
        }
    }
}
