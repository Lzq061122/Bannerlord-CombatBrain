namespace CombatBrain.Coordination
{
    using System.Collections.Generic;

    // ATC-0 命令来源与 Why 观测（CommandProvenance）—— 纯逻辑（零 TaleWorlds 依赖，可离线测）。
    // 职责：记录 CombatBrain 各子系统对 Formation 下达的命令来源/类型/目标，回答「谁提出命令、
    //   同拍是否被覆盖、被拒后有无安全降级」的前置观测（ATC 规格 §1.3）。本卡不做任何行为变化：
    //   只记录，不包装/不改变命令、不调整执行顺序。
    // 反作弊闸：只读已有决策状态（哪个 Glue 调了哪个命令方法），零新增禁读。
    // 性能：固定容量 ring buffer，Record O(1) 破顶覆盖最旧（禁 List.RemoveRange，P0-⑥ 同款教训）。
    public enum CommandSource
    {
        OriginalUnknown = 0,   // 原版/未归属
        RangedPosition = 1,    // T3-01 射手阵地
        UnitPreservation = 2,  // T8-01 骑射保存有生力量
        Cavalry = 3,           // F8 骑兵波次
        Skirmish = 4,          // T5 骑射游击
        Morale = 5,            // F11 士气撤退
        Protect = 6,           // F3 保护核心
        Brace = 7,             // F4 架枪反骑
        ArmyCoordination = 8   // ATC 系列（预留）
    }

    public enum CommandKind
    {
        None = 0,
        Hold = 1,         // Stop（站定）
        Move = 2,         // MoveTo（移动目标点）
        Advance = 3,      // Advance（交回原版推进）
        Charge = 4,       // Charge/ChargeToTarget（冲锋）
        Retreat = 5,      // Retreat（撤退）
        FallBack = 6,     // FallBack（后撤）
        Arrangement = 7,  // SetArrangementOrder（阵型切换）
        Facing = 8,       // SetFacingOrder（朝向）
        FrameRelease = 9  // SetFormationFrameDisabled（解阵框）
    }

    /// <summary>编队命令决策痕迹（一条命令记录；同拍后续命令会覆盖上一条的 WasOverridden/OverriddenBy）。</summary>
    public struct FormationDecisionTrace
    {
        public int FormationId;        // Formation.Index（编队索引）
        public float Time;             // 逻辑拍时间（战斗累计秒）
        public CommandSource Source;   // 提出命令的子系统
        public CommandKind Kind;       // 命令类型
        public int ReasonCode;         // 原因码（预留：0=无，后续卡填充）
        public int TargetId;           // 目标编队/实体 id（-1=无）
        public float TargetX;          // 目标位置 X（无目标=0）
        public float TargetY;          // 目标位置 Y（无目标=0）
        public bool WasOverridden;     // 同拍被后续命令覆盖
        public CommandSource OverriddenBy; // 覆盖者（WasOverridden=true 时有效）

        public FormationDecisionTrace(int formationId, float time, CommandSource source, CommandKind kind,
            int reasonCode, int targetId, float tx, float ty)
        {
            FormationId = formationId;
            Time = time;
            Source = source;
            Kind = kind;
            ReasonCode = reasonCode;
            TargetId = targetId;
            TargetX = tx;
            TargetY = ty;
            WasOverridden = false;
            OverriddenBy = CommandSource.OriginalUnknown;
        }
    }

    public sealed class CommandProvenance
    {
        // ATC-0 静态单例：胶水层在各命令点旁 Instance.Record(...)（不包装命令，行为零变化）；
        // BeginLogicalTick 由 CombatAiMissionLogic.OnMissionTick 每帧调用（同拍覆盖链判定基准）。
        public static readonly CommandProvenance Instance = new CommandProvenance();

        public const int Capacity = 256;          // 固定容量 ring（500 人战场编队数个位数 × 每拍多命令，够用）
        public const float SameTickEpsilon = 0.01f; // 同逻辑拍判定容差（秒）

        private readonly FormationDecisionTrace[] _ring = new FormationDecisionTrace[Capacity];
        private int _head;                        // 下一条写入位置（破顶覆盖最旧）
        private int _count;                       // 已写入条数（≤Capacity）
        private readonly Dictionary<int, int> _lastByFormation = new Dictionary<int, int>(); // 编队 → ring 中最近 trace 下标
        private float _lastTick = -1f;            // 当前逻辑拍（BeginLogicalTick 设置；-1=未开始）

        public int Count { get { return _count; } }

        /// <summary>逻辑拍起点（CombatAiMissionLogic.OnMissionTick 每帧调用；同拍内 Record 视为同拍覆盖链）。</summary>
        public void BeginLogicalTick(float now)
        {
            _lastTick = now;
        }

        /// <summary>记录一条命令（O(1)，破顶覆盖最旧；同 Formation 同拍后续命令 → 上一条 WasOverridden）。</summary>
        public void Record(int formationId, CommandSource source, CommandKind kind,
            int reasonCode = 0, int targetId = -1, float tx = 0f, float ty = 0f)
        {
            float now = _lastTick >= 0f ? _lastTick : 0f;
            int prev;
            if (_lastByFormation.TryGetValue(formationId, out prev))
            {
                FormationDecisionTrace last = _ring[prev];
                // 同一编队同一逻辑拍：上一条被本条覆盖（同拍覆盖链可复原）
                if (last.Time >= 0f && now - last.Time < SameTickEpsilon)
                {
                    last.WasOverridden = true;
                    last.OverriddenBy = source;
                    _ring[prev] = last;
                }
            }

            _ring[_head] = new FormationDecisionTrace(formationId, now, source, kind, reasonCode, targetId, tx, ty);
            _lastByFormation[formationId] = _head;
            _head = (_head + 1) % Capacity;
            if (_count < Capacity) _count++;
        }

        /// <summary>编队删除清理（OnAgentRemoved/编队解散时调用，防字典滞留）。</summary>
        public void RemoveFormation(int formationId)
        {
            _lastByFormation.Remove(formationId);
        }

        /// <summary>全部清空（战斗重置）。</summary>
        public void Clear()
        {
            _head = 0;
            _count = 0;
            _lastByFormation.Clear();
            _lastTick = -1f;
        }

        /// <summary>读第 i 条（0..Count-1；供日志/聚合/测试遍历，不改变状态）。</summary>
        public FormationDecisionTrace At(int i)
        {
            if (i < 0 || i >= _count) return new FormationDecisionTrace(-1, -1f, CommandSource.OriginalUnknown, CommandKind.None, 0, -1, 0f, 0f);
            int idx = (_head - _count + i + Capacity) % Capacity;
            if (idx < 0) idx += Capacity;
            return _ring[idx];
        }

        /// <summary>最近一条（-1=空；供同拍覆盖链判定）。</summary>
        public int LastIndex(int formationId)
        {
            int idx;
            return _lastByFormation.TryGetValue(formationId, out idx) ? idx : -1;
        }
    }
}
