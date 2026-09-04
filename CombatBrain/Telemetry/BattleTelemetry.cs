using System;
using System.Globalization;
using System.Text;

namespace CombatBrain.Telemetry
{
    // T38-01 战斗遥测计数器（战术 AI 增强派单文档批次 2 第一项）—— 纯逻辑零依赖。
    // 20 项战术事件：让后续每个 feature 的验收从「肉眼」变「看计数」。
    // 设计：17 个计数项 int 自增（热路径零分配：_counts 固定数组，Increment 仅下标自增）；
    //       3 个非计数项（BattleDuration 秒 / HorseArcherLossRate 比值 / IdleUnitTime 秒）由独立
    //       float 字段承载，经 Set/Add 写入（Increment 对这三项为无操作）。
    // 序列化：手写 JSON（零第三方依赖，参考 F10 CampaignHabits 模式），ToJson/Summary 均用
    //       InvariantCulture 保证小数点为 '.'（JSON 合法）。
    // 落盘：由胶水层 CombatAiMissionLogic 在战斗结束一次完成（OnMissionStateFinalized/OnClearScene 幂等）。
    // 事件源：胶水层订阅 CombatEventBus 分发到 Increment/Set/Add；本类不引用 Events 保持纯逻辑可离线测。
    public enum TelemetryKind
    {
        BattleDuration = 0,           // 战斗总时长（秒，float，Set）
        FormationBreakCount,          // 编队崩溃次数
        FormationReformCount,         // 编队重整次数
        ForcedChargeCount,            // 强制冲锋次数
        CavalryChargeCount,           // 骑兵冲锋次数
        CavalrySuccessfulChargeCount, // 骑兵冲锋成功次数
        CavalryDisengageCount,        // 骑兵脱离次数
        CavalryRallyCount,            // 骑兵重整次数
        HorseArcherFlankCount,        // 骑射侧翼次数
        HorseArcherFrontalAttackCount,// 骑射正面进攻次数
        HorseArcherLossRate,          // 骑射损失率（比值，float，Set）
        ArcherPositionChangeCount,    // 弓手换位次数
        ArcherThreatenedCount,        // 弓手受胁次数
        RetreatCount,                 // 撤退次数
        TerrainChangeCount,           // 地形变化次数
        StalemateCount,               // 僵持次数
        IdleUnitTime,                 // 闲置单位时间（秒，float，Add）
        SupportMissionCount,          // 支援任务次数
        CombinedAttackCount,          // 联合进攻次数
        PostMeleeReformCount          // 近战重整次数
    }

    public sealed class BattleTelemetry
    {
        public const int KindCount = 20;

        // 与 TelemetryKind 顺序一一对应（ToJson/Summary 使用）
        private static readonly string[] Names =
        {
            "BattleDuration", "FormationBreakCount", "FormationReformCount", "ForcedChargeCount",
            "CavalryChargeCount", "CavalrySuccessfulChargeCount", "CavalryDisengageCount", "CavalryRallyCount",
            "HorseArcherFlankCount", "HorseArcherFrontalAttackCount", "HorseArcherLossRate", "ArcherPositionChangeCount",
            "ArcherThreatenedCount", "RetreatCount", "TerrainChangeCount", "StalemateCount",
            "IdleUnitTime", "SupportMissionCount", "CombinedAttackCount", "PostMeleeReformCount"
        };

        // 计数项（int 自增，热路径零分配）。三个非计数项（BattleDuration/HorseArcherLossRate/IdleUnitTime）
        // 不存本数组（本数组对应槽位恒 0），由下方独立 float 字段承载。
        private readonly int[] _counts = new int[KindCount];
        private float _battleDuration;
        private float _horseArcherLossRate;
        private float _idleUnitTime;

        /// <summary>计数项自增；对三个非计数项（float 项）为无操作（用 Set/Add 写入）。</summary>
        public void Increment(TelemetryKind kind)
        {
            switch (kind)
            {
                case TelemetryKind.BattleDuration:
                case TelemetryKind.HorseArcherLossRate:
                case TelemetryKind.IdleUnitTime:
                    return; // 非计数项：Increment 无操作
            }
            int i = (int)kind;
            if (i < 0 || i >= KindCount) return;
            _counts[i]++;
        }

        public int Count(TelemetryKind kind)
        {
            int i = (int)kind;
            if (i < 0 || i >= KindCount) return 0;
            return _counts[i];
        }

        public void SetBattleDuration(float seconds) { _battleDuration = seconds; }
        public void SetHorseArcherLossRate(float rate) { _horseArcherLossRate = rate; }
        public void AddIdleUnitTime(float seconds) { _idleUnitTime += seconds; }

        public void Reset()
        {
            Array.Clear(_counts, 0, KindCount);
            _battleDuration = 0f;
            _horseArcherLossRate = 0f;
            _idleUnitTime = 0f;
        }

        public string ToJson()
        {
            var sb = new StringBuilder(512);
            sb.Append('{');
            for (int i = 0; i < KindCount; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('"').Append(Names[i]).Append("\":");
                AppendValue(sb, (TelemetryKind)i);
            }
            sb.Append('}');
            return sb.ToString();
        }

        public string Summary()
        {
            var sb = new StringBuilder(320);
            for (int i = 0; i < KindCount; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(Names[i]).Append('=');
                AppendValue(sb, (TelemetryKind)i);
            }
            return sb.ToString();
        }

        private void AppendValue(StringBuilder sb, TelemetryKind kind)
        {
            switch (kind)
            {
                case TelemetryKind.BattleDuration:
                    sb.Append(_battleDuration.ToString("0.##", CultureInfo.InvariantCulture));
                    break;
                case TelemetryKind.HorseArcherLossRate:
                    sb.Append(_horseArcherLossRate.ToString("0.###", CultureInfo.InvariantCulture));
                    break;
                case TelemetryKind.IdleUnitTime:
                    sb.Append(_idleUnitTime.ToString("0.##", CultureInfo.InvariantCulture));
                    break;
                default:
                    sb.Append(_counts[(int)kind]);
                    break;
            }
        }
    }
}
