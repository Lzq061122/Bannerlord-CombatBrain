namespace CombatBrain.Coordination
{
    using System.Collections.Generic;
    using CombatBrain;
    using TaleWorlds.MountAndBlade;

    // ATC-0 命令来源与 Why 观测（CommandProvenance）—— 纯逻辑核心（零 TaleWorlds 依赖，可离线测）。
    // 职责：记录 CombatBrain 各子系统对 Formation 下达的命令来源/类型/目标，回答「谁提出命令、
    //   同拍是否被覆盖、被拒后有无安全降级」的前置观测（ATC 规格 §1.3）。本卡不做任何行为变化：
    //   只记录，不包装/不改变命令、不调整执行顺序。
    // 反作弊闸：只读已有决策状态（哪个 Glue 调了哪个命令方法），零新增禁读。
    // 性能：固定容量 ring buffer，Record O(1) 破顶覆盖最旧（禁 List.RemoveRange，P0-⑥ 同款教训）。
    // CPS-0 追加（规格「战场命令控制面收口与原版AI权衡正式派单规格-2026-09-06」§4，2026-09-06）：
    //   * 来源语义修正（§4.3）：CommandSource 追加 FormationTactics/FacingCorrection 真实来源
    //     （FacingCorrection/FormationTactics 等由 CB 自己调用却记成 OriginalUnknown 的位置改为真实来源）；
    //     SameTickOverride 与 CrossTickReplacement 分开统计（OverrideKind + 双计数器）；
    //     OriginalUnknown 仅作历史兼容值，新数据不得继续写入含混来源。
    //   * 真实 setter 观测（§4.2）：SetOrderObserver + CommandSourceScope（ThreadStatic，禁 StackTrace）+ 只读
    //     Harmony Prefix（Formation.SetMovementOrder/SetFacingOrder/SetArrangementOrder），不修改参数、
    //     不跳过原方法、不改变返回结果；无 scope 记 ExternalUnknown（禁止命名为 NativeTeamAI）；
    //     诊断开关默认关闭近零开销；patch 失败 fail-open 并写启动日志。
    //   * 观测挂载部分需 TaleWorlds.MountAndBlade/HarmonyLib 编译期引用（CombatBrain.csproj 0Harmony.dll）；
    //     纯逻辑部分（Record/计数/scope/观测记录）仍零运行时游戏依赖，可离线测。
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
        ArmyCoordination = 8,  // ATC 系列（预留）
        FormationControlLease = 9, // ATC-5R 编队控制权租约（追加不重排既有值，§7.1）
        // CPS-0 追加（不重排既有值）：真实来源与观测来源（规格 CPS-0 §4.1/§4.3）
        FormationTactics = 10, // 编队阵型修正（t89 全面接管，FormationTactics 自行 SetArrangementOrder）
        FacingCorrection = 11, // 编队朝向修正（X3，FacingCorrection 自行 SetFacingOrder）
        PlayerOrManual = 12,   // 观测来源：玩家/手动命令（仅可靠公开信号可标，§4.2.4）
        ExternalUnknown = 13   // 观测来源：无 CB scope 的外部/原版/第三方写入（禁止命名为 NativeTeamAI，§4.1）
    }

    // CPS-0 §4.2：命令域（三个 setter 观测维度）
    public enum CommandDomain
    {
        None = 0,
        Movement = 1,    // SetMovementOrder
        Facing = 2,      // SetFacingOrder
        Arrangement = 3  // SetArrangementOrder
    }

    // CPS-0 §4.3.3：覆盖/替换分类——同拍覆盖（SameTickOverride）与跨拍替换（CrossTickReplacement）分开统计
    public enum CommandOverrideKind
    {
        None = 0,       // 无覆盖/无替换
        SameTick = 1,   // 同一逻辑拍内被后续命令覆盖（覆盖链）
        CrossTick = 2   // 更早逻辑拍的命令被本拍命令实质替换（Kind 或 Source 变化）
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
        public CommandOverrideKind OverrideKind; // CPS-0 §4.3.3：SameTick=同拍覆盖 / CrossTick=跨拍替换 / None=无

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
            OverrideKind = CommandOverrideKind.None;
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
        private readonly int[] _slotFormation;    // atc4-phase-provenance-fix P1-3：ring 槽位 → FormationId 反向映射
                                                  //   （-1=空槽；槽位被覆盖前清理指向该槽位的旧 Formation 映射，
                                                  //    防旧 Formation 的 _lastByFormation 指向其他编队的新记录）
        private float _lastTick = -1f;            // 当前逻辑拍（BeginLogicalTick 设置；-1=未开始）

        // CPS-0 §4.3.3：分开统计——同拍覆盖（SameTickOverride）与跨拍替换（CrossTickReplacement）
        public long SameTickOverrideCount;
        public long CrossTickReplacementCount;

        public CommandProvenance()
        {
            _slotFormation = new int[Capacity];
            for (int i = 0; i < _slotFormation.Length; i++) _slotFormation[i] = -1;
        }

        public int Count { get { return _count; } }

        /// <summary>逻辑拍起点（CombatAiMissionLogic.OnMissionTick 每帧调用；同拍内 Record 视为同拍覆盖链）。</summary>
        public void BeginLogicalTick(float now)
        {
            _lastTick = now;
            SetOrderObserver.NotifyTick(now); // CPS-0：同步 setter 观测的时间基准（§4.2.5 调用时间）
        }

        /// <summary>记录一条命令（O(1)，破顶覆盖最旧；同 Formation 同拍后续命令 → 上一条 WasOverridden）。</summary>
        public void Record(int formationId, CommandSource source, CommandKind kind,
            int reasonCode = 0, int targetId = -1, float tx = 0f, float ty = 0f)
        {
            float now = _lastTick >= 0f ? _lastTick : 0f;
            int prev = LastIndex(formationId); // P1-3：读取映射后验证槽位 FormationId 匹配（绕回后旧映射不误判覆盖链）
            if (prev >= 0)
            {
                FormationDecisionTrace last = _ring[prev];
                // 同一编队同一逻辑拍：上一条被本条覆盖（同拍覆盖链可复原）
                if (last.Time >= 0f && now - last.Time < SameTickEpsilon)
                {
                    last.WasOverridden = true;
                    last.OverriddenBy = source;
                    last.OverrideKind = CommandOverrideKind.SameTick; // CPS-0 §4.3.3
                    _ring[prev] = last;
                    SameTickOverrideCount++;
                }
                // CPS-0 §4.3.3：跨拍替换——上一条在更早逻辑拍且命令实质变化（Kind 或 Source 不同）→ 分开统计
                else if (last.Time >= 0f && now - last.Time >= SameTickEpsilon
                    && (last.Kind != kind || last.Source != source))
                {
                    last.OverrideKind = CommandOverrideKind.CrossTick;
                    _ring[prev] = last;
                    CrossTickReplacementCount++;
                }
            }

            // P1-3：槽位被覆盖前，清理指向该槽位的旧 Formation 映射（防旧编队指向其他编队的新记录）
            int oldOwner = _slotFormation[_head];
            if (oldOwner >= 0 && _lastByFormation.ContainsKey(oldOwner) && _lastByFormation[oldOwner] == _head)
            {
                _lastByFormation.Remove(oldOwner);
            }

            _ring[_head] = new FormationDecisionTrace(formationId, now, source, kind, reasonCode, targetId, tx, ty);
            _slotFormation[_head] = formationId;
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
            for (int i = 0; i < _slotFormation.Length; i++) _slotFormation[i] = -1;
            _lastTick = -1f;
            SameTickOverrideCount = 0;      // CPS-0 §4.3.3：Mission Clear 清空覆盖统计
            CrossTickReplacementCount = 0;
        }

        /// <summary>读第 i 条（0..Count-1；供日志/聚合/测试遍历，不改变状态）。</summary>
        public FormationDecisionTrace At(int i)
        {
            if (i < 0 || i >= _count) return new FormationDecisionTrace(-1, -1f, CommandSource.OriginalUnknown, CommandKind.None, 0, -1, 0f, 0f);
            int idx = (_head - _count + i + Capacity) % Capacity;
            if (idx < 0) idx += Capacity;
            return _ring[idx];
        }

        /// <summary>
        /// 最近一条（-1=空；供同拍覆盖链判定）。P1-3：读取映射后验证槽位中的 FormationId 匹配——
        ///   环形缓冲绕回后旧映射若未被清理，此处返回 -1（不指向其他编队的新记录）。
        /// </summary>
        public int LastIndex(int formationId)
        {
            int idx;
            if (!_lastByFormation.TryGetValue(formationId, out idx)) return -1;
            if (idx < 0 || idx >= Capacity) return -1;
            if (_ring[idx].FormationId != formationId) return -1; // 槽位验证（绕回误指防护）
            return idx;
        }

        // ---- ATC-0 输出层（t34 补齐；只读既有记录数据，零新增游戏读取）----

        // 5s 汇总只输出「有覆盖 / 命令变化」的 Formation（禁每拍每队刷日志，规格 §14）：
        // 收集在 [start, now) 窗口内有 WasOverridden 或 Kind 变化（同 Formation 相邻记录 Kind 不同）的 FormationId。
        public System.Collections.Generic.List<int> GetDirtyFormations(float start, float now)
        {
            var dirty = new System.Collections.Generic.List<int>();
            for (int i = 0; i < _count; i++)
            {
                FormationDecisionTrace t = At(i);
                if (t.FormationId < 0) continue;
                if (t.Time < start || t.Time >= now) continue; // 只查窗口内
                if (t.WasOverridden) { if (!dirty.Contains(t.FormationId)) dirty.Add(t.FormationId); continue; }
                // 命令变化：同 Formation 上一条（窗口内较早）Kind 不同
                for (int j = i + 1; j < _count; j++)
                {
                    FormationDecisionTrace u = At(j);
                    if (u.FormationId != t.FormationId) continue;
                    if (u.Time < start || u.Time >= now) continue;
                    if (u.Kind != t.Kind) { if (!dirty.Contains(t.FormationId)) dirty.Add(t.FormationId); }
                    break;
                }
            }
            return dirty;
        }

        /// <summary>
        /// §14 OrderTrace 覆盖链格式：该 Formation 全部记录（按时间升序）重建覆盖链，
        /// 输出 "Source:Kind → Source:Kind(overrode PrevSource) → ... → final=Kind"。
        /// 覆盖标注加在「覆盖者」上（本条覆盖了前一条时）。空/无该编队 → 空串。只读记录数据。
        /// </summary>
        public string BuildOrderTrace(int formationId)
        {
            int idx = LastIndex(formationId);
            if (idx < 0) return string.Empty;

            // 收集该编队全部记录（按时间升序），重建覆盖链
            var chain = new System.Collections.Generic.List<FormationDecisionTrace>();
            for (int i = 0; i < _count; i++)
            {
                FormationDecisionTrace t = At(i);
                if (t.FormationId == formationId) chain.Add(t);
            }
            if (chain.Count == 0) return string.Empty;

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < chain.Count; i++)
            {
                FormationDecisionTrace t = chain[i];
                if (i > 0) sb.Append(" → ");
                sb.Append(t.Source.ToString()).Append(":").Append(t.Kind.ToString());
                // 本条是覆盖者：前一条 WasOverridden 且 OverriddenBy == 本条 Source
                if (i > 0 && chain[i - 1].WasOverridden && chain[i - 1].OverriddenBy == t.Source)
                    sb.Append("(overrode ").Append(chain[i - 1].Source.ToString()).Append(")");
            }
            sb.Append(" → final=").Append(chain[chain.Count - 1].Kind.ToString());
            return sb.ToString();
        }

        /// <summary>按 CommandSource 聚合（命令数/覆盖数，全部 9 源固定顺序输出 0/o0 亦含），供战斗结束输出。只读记录数据。</summary>
        public string BuildSourceAggregate()
        {
            var sb = new System.Text.StringBuilder();
            int srcCount = System.Enum.GetValues(typeof(CommandSource)).Length;
            for (int s = 0; s < srcCount; s++)
            {
                int total = 0, overridden = 0;
                for (int i = 0; i < _count; i++)
                {
                    FormationDecisionTrace t = At(i);
                    if (t.Source != (CommandSource)s) continue;
                    total++;
                    if (t.WasOverridden) overridden++;
                }
                sb.Append((CommandSource)s).Append("=").Append(total)
                  .Append("/o").Append(overridden).Append(";");
            }
            return sb.ToString();
        }

        // CPS-0 §4.3.3：同拍覆盖与跨拍替换分开统计的输出摘要（供 5s 汇总/战斗结束输出，只读计数）
        public string BuildOverrideSummary()
        {
            return "SameTickOverride=" + SameTickOverrideCount
                + ";CrossTickReplacement=" + CrossTickReplacementCount;
        }
    }

    // CPS-0 §4.2.5：setter 级真实调用观测记录（与声明式 FormationDecisionTrace 分离；ring 固定容量）
    public struct SetOrderObservation
    {
        public int FormationId;        // Formation.Index
        public float Time;             // 逻辑拍时间（BeginLogicalTick 同步基准）
        public CommandDomain Domain;   // Movement / Facing / Arrangement
        public CommandSource Source;   // 真实来源：CB scope 模块 / PlayerOrManual / ExternalUnknown（无 scope）
        public CommandKind Kind;       // 命令种类（第一版为域级近似：Move/Facing/Arrangement）
        public int PreState;           // 调用前命令 Type（int；0=未知）
        public int PostState;          // 调用后命令 Type（int；0=未知；第一版 Prefix 观测填 0）
    }

    /// <summary>
    /// CPS-0 §4.2.2/§4.2.3：CombatBrain 调用来源 scope（ThreadStatic 栈式，禁每次抓 StackTrace）。
    /// 执行器/胶水在调用三个 SetOrder 前 Enter，using + try/finally 保证退出与异常清理（§10.1）；
    /// 无 scope 时观测记 ExternalUnknown（§4.2.4，禁止命名为 NativeTeamAI）。
    /// </summary>
    public sealed class CommandSourceScope : System.IDisposable
    {
        [System.ThreadStatic] private static CommandSourceScope _active;
        private readonly CommandSourceScope _previous;
        private readonly CommandSource _source;
        private bool _disposed;

        private CommandSourceScope(CommandSource source)
        {
            _source = source;
            _previous = _active;
        }

        /// <summary>进入来源 scope（调用三个 SetOrder 之前；返回对象须 using/Dispose 保证清理）。</summary>
        public static CommandSourceScope Enter(CommandSource source)
        {
            var scope = new CommandSourceScope(source);
            _active = scope;
            return scope;
        }

        /// <summary>当前来源（无 scope → ExternalUnknown，§4.2.4）。</summary>
        public static CommandSource Current
        {
            get { return _active != null ? _active._source : CommandSource.ExternalUnknown; }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _active = _previous; // 异常路径由 try/finally 保证执行（§10.1）
        }
    }

    /// <summary>
    /// CPS-0 §4.2：真实 setter 观测（Formation.SetMovementOrder/SetFacingOrder/SetArrangementOrder 只读 Prefix）。
    /// 诊断开关默认关闭 → patch 不应用 → 每次 SetOrder 零额外开销（§4.2.7 近零开销）；
    /// patch 版本签名不匹配 → fail-open 并写启动日志「观测不可用」（§4.2.8）。
    /// 纯数据记录部分（Observe/CountFor/At/Clear）零游戏运行时依赖，可离线测；
    /// Harmony 挂载在嵌套类 Patches（惰性加载，OfflineTests 不触碰）。
    /// 注：Enable() 的启动调用点由 cps0-runtime-baseline 卡接入（本卡实现机制 + fail-open）。
    /// </summary>
    public static class SetOrderObserver
    {
        public static bool Enabled;             // 诊断开关（默认 false=近零开销；MCM 暴露留 runtime-baseline 卡）
        public static float LastTick;           // 逻辑拍时间基准（CommandProvenance.BeginLogicalTick 同步）
        public const int Capacity = 128;        // 观测 ring 固定容量（破顶覆盖最旧，O(1)）

        // t34（cps3-observer-diagnostics）：Harmony patch 唯一标识（挂载日志/核验/测试用；原 Patches 私有提升公开）
        public const string HarmonyId = "com.combatbrain.setorderobserver";

        /// <summary>t34：挂载成功日志文本（唯一标识 + prefix 核验数；Apply 仅一次执行幂等不重复刷；可离线断言格式）。</summary>
        public static string BuildMountLog(int prefixCount)
        {
            return "[SetOrderObserver] 观测已挂载（Harmony patch OK, id=" + HarmonyId + ", prefixes=" + prefixCount + "）";
        }

        /// <summary>t36：HarmonyLib 运行时版本 + 0Harmony 加载来源诊断日志文本（与编译引用版本/模块对比；可离线断言格式）。</summary>
        public static string BuildVersionLog(string version, string location)
        {
            return "[SetOrderObserver] HarmonyLib 运行时版本=" + version
                + "；0Harmony 加载=" + location + "（csproj 引用 Bannerlord.Harmony 模块 0Harmony.dll）";
        }

        private static readonly SetOrderObservation[] _ring = new SetOrderObservation[Capacity];
        private static int _head;
        private static int _count;
        // 聚合计数：key = domain*16 + source → 次数（固定小表，无逐帧分配）
        private static readonly Dictionary<int, long> _counts = new Dictionary<int, long>();

        public static int Count { get { return _count; } }

        public static void NotifyTick(float now) { LastTick = now; }

        public static SetOrderObservation At(int i)
        {
            if (i < 0 || i >= _count) return default(SetOrderObservation);
            int idx = (_head - _count + i + Capacity) % Capacity;
            if (idx < 0) idx += Capacity;
            return _ring[idx];
        }

        public static long CountFor(CommandDomain domain, CommandSource source)
        {
            long v;
            return _counts.TryGetValue(Key(domain, source), out v) ? v : 0L;
        }

        private static int Key(CommandDomain d, CommandSource s) { return (int)d * 16 + (int)s; }

        public static void Clear()
        {
            _head = 0;
            _count = 0;
            _counts.Clear();
        }

        /// <summary>纯数据观测入口（Harmony Prefix 与离线测试共用；诊断关闭不记录；无 scope → ExternalUnknown）。</summary>
        public static void Observe(int formationId, CommandDomain domain, CommandKind kind,
            int preState, int postState)
        {
            if (!Enabled) return; // §4.2.7：诊断关闭不记录（近零开销）
            var obs = new SetOrderObservation
            {
                FormationId = formationId,
                Time = LastTick,
                Domain = domain,
                Source = CommandSourceScope.Current, // ThreadStatic：无 scope → ExternalUnknown（§4.2.4）
                Kind = kind,
                PreState = preState,
                PostState = postState
            };
            _ring[_head] = obs;
            _head = (_head + 1) % Capacity;
            if (_count < Capacity) _count++;
            int k = Key(domain, obs.Source);
            long c;
            _counts.TryGetValue(k, out c);
            _counts[k] = c + 1;
        }

        // t30 repair（cps3-runtime-prep 观测热切缺陷）：运行期观测同步判定（纯逻辑，可离线测）。
        // 原缺陷：Enable() 仅 SubModule 启动一次性调用（gated by ObserverEnabled 启动时默认 false），
        //   游戏内 MCM 开启后不生效（Harmony patch 未挂上，ExternalUnknown=0 实锤）。
        // 修复：CombatAiMissionLogic.Tick 首部每帧调 ShouldEnable/ShouldDisable——配置开且未挂载 → Enable()
        //   （幂等：Enable 内部 if(Enabled)return，重复调用不重复 Patch）；配置关且已挂载 → Disable()（对称）。
        /// <summary>是否需要启用观测（配置开 && 未挂载；幂等——已挂载返回 false 不重复 Patch）。</summary>
        public static bool ShouldEnable(bool configuredOn)
        {
            return configuredOn && !Enabled;
        }

        /// <summary>是否需要停用观测（配置关 && 已挂载；对称 Disable 幂等）。</summary>
        public static bool ShouldDisable(bool configuredOn)
        {
            return !configuredOn && Enabled;
        }

        /// <summary>启用观测（应用只读 Harmony patch）。版本签名不匹配 → 捕获 → fail-open + 启动日志（§4.2.8）。</summary>
        public static void Enable()
        {
            if (Enabled) return;
            try
            {
                Patches.Apply();
                Enabled = true;
            }
            catch (System.Exception ex)
            {
                Enabled = false; // fail-open：观测不可用不影响任何战斗行为
                CombatLog.Log("[SetOrderObserver] 观测不可用（Harmony patch 失败，fail-open）：" + ex.Message);
            }
        }

        public static void Disable()
        {
            if (!Enabled) return;
            try { Patches.Unapply(); }
            catch (System.Exception) { /* fail-open：幂等 */ }
            Enabled = false;
        }

        // Harmony 只读观测挂载（CPS-0 §4.2 第1条）：不修改参数、不跳过原方法、不改变返回结果。
        // t36 repair（0/3 挂载失败 BLOCKER）：PatchAll 程序集扫描静默失败（三 setter 零 patch 且无异常，
        //   captain 实测日志「观测挂载异常：仅 0/3 target 有 prefix」）——改显式 Patch 逐个打三 setter，
        //   不依赖 PatchAll 扫描；记录运行时 HarmonyLib 版本与 0Harmony 加载来源（csproj 引用 Bannerlord.Harmony
        //   模块，运行时可能绑定其它模块/版本的 0Harmony.dll——游戏 Modules 多个 0Harmony.dll）。
        internal static class Patches
        {
            private static HarmonyLib.Harmony _harmony;

            public static void Apply()
            {
                if (_harmony != null) return;
                var harmony = new HarmonyLib.Harmony(HarmonyId);
                try
                {
                    // 显式 Patch 三 setter（不依赖 PatchAll 程序集扫描——消除扫描静默失败路径）
                    harmony.Patch(MethodOf(nameof(Formation.SetMovementOrder)),
                        prefix: PrefixMethod(nameof(MovementPrefix)));
                    harmony.Patch(MethodOf(nameof(Formation.SetFacingOrder)),
                        prefix: PrefixMethod(nameof(FacingPrefix)));
                    harmony.Patch(MethodOf(nameof(Formation.SetArrangementOrder)),
                        prefix: PrefixMethod(nameof(ArrangementPrefix)));
                }
                catch (System.Exception)
                {
                    _harmony = null; // 允许下次重试；异常冒泡到 Enable catch → 「观测不可用（具体异常）」fail-open
                    throw;
                }
                _harmony = harmony;

                // t36：运行时 HarmonyLib 版本 + 0Harmony 加载来源诊断（与编译引用版本/模块对比）
                string version = "unknown";
                string location = "unknown";
                try
                {
                    var asm = typeof(HarmonyLib.Harmony).Assembly;
                    version = asm.GetName().Version != null ? asm.GetName().Version.ToString() : "unknown";
                    location = asm.Location;
                }
                catch (System.Exception) { }
                CombatLog.Log(BuildVersionLog(version, location));

                // t34 核验：三 target 各至少 1 个 patch；成功写唯一标识日志（幂等），不足写异常日志
                int prefixCount = CountVerifiedPrefixes();
                if (prefixCount == 3)
                {
                    CombatLog.Log(BuildMountLog(prefixCount));
                }
                else
                {
                    CombatLog.Log("[SetOrderObserver] 观测挂载异常：仅 " + prefixCount
                        + "/3 target 有 prefix（Harmony patch 可能未生效，查 Harmony 版本/模块加载）");
                }
            }

            public static void Unapply()
            {
                if (_harmony == null) return;
                _harmony.UnpatchAll(HarmonyId);
                _harmony = null;
            }

            // 显式 Patch 辅助：目标方法反射（公开方法，非私有字段反射，反作弊闸守）
            private static System.Reflection.MethodBase MethodOf(string methodName)
            {
                System.Reflection.MethodBase m = typeof(Formation).GetMethod(methodName);
                if (m == null) throw new System.InvalidOperationException("SetOrderObserver 目标方法缺失: " + methodName);
                return m;
            }

            // 显式 Patch 辅助：prefix 方法委托（本嵌套类私有静态方法）
            private static HarmonyLib.HarmonyMethod PrefixMethod(string methodName)
            {
                System.Reflection.MethodInfo m = typeof(Patches).GetMethod(methodName,
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                if (m == null) throw new System.InvalidOperationException("SetOrderObserver prefix 方法缺失: " + methodName);
                return new HarmonyLib.HarmonyMethod(m);
            }

            // 核验三 setter 各至少 1 个 patch（HarmonyLib.Harmony.GetPatchInfo 只读；公开方法反射非私有，反作弊闸守）
            private static int CountVerifiedPrefixes()
            {
                int n = 0;
                if (HasAnyPrefix(typeof(Formation).GetMethod(nameof(Formation.SetMovementOrder)))) n++;
                if (HasAnyPrefix(typeof(Formation).GetMethod(nameof(Formation.SetFacingOrder)))) n++;
                if (HasAnyPrefix(typeof(Formation).GetMethod(nameof(Formation.SetArrangementOrder)))) n++;
                return n;
            }

            private static bool HasAnyPrefix(System.Reflection.MethodBase original)
            {
                if (original == null) return false;
                // 0Harmony 版本核实：GetPatchInfo 返回 HarmonyLib.Patches（属性 Owners: ReadOnlyCollection<Patch>）——
                // 任一 patch 挂载即视为该 target 已观测（只读查询，非私有字段反射，反作弊闸守）
                HarmonyLib.Patches patches = HarmonyLib.Harmony.GetPatchInfo(original);
                return patches != null && patches.Owners != null && patches.Owners.Count > 0;
            }

            // t36：显式 Patch 目标（三 setter；诊断/测试共用方法名清单）
            public static readonly string[] PatchTargetMethodNames =
                { "SetMovementOrder", "SetFacingOrder", "SetArrangementOrder" };
            // t38 repair：三 setter 原方法参数名（Harmony 显式 Patch 按参数名绑定 Prefix——Prefix 参数名必须
            //   与原方法一致，否则抛「Parameter not found」fail-open）。官方反编译 v1.4.8：SetMovementOrder(MovementOrder input)/
            //   SetFacingOrder(FacingOrder order)/SetArrangementOrder(ArrangementOrder order)（Formation.cs:685/717/722）。
            //   防复发：本表与三个 Prefix 方法签名参数名一一对应，离线断言锁定。
            public static readonly string[] PatchTargetParameterNames =
                { "input", "order", "order" };

            private static void MovementPrefix(Formation __instance, MovementOrder input)
            {
                // 只读观测：不修改参数、不跳过原方法、不改变返回结果（§4.2 第1条）；
                // 命令种类用统一 OrderType（v1.4.8：MovementOrder.OrderType 公开属性），不读私有字段（反作弊闸）；
                // t38：参数名 input 与原方法 SetMovementOrder(MovementOrder input) 一致（Harmony 按参数名绑定）
                SetOrderObserver.Observe(__instance.Index, CommandDomain.Movement, CommandKind.Move,
                    (int)input.OrderType, 0);
            }

            private static void FacingPrefix(Formation __instance, FacingOrder order)
            {
                SetOrderObserver.Observe(__instance.Index, CommandDomain.Facing, CommandKind.Facing,
                    (int)order.OrderType, 0);
            }

            private static void ArrangementPrefix(Formation __instance, ArrangementOrder order)
            {
                SetOrderObserver.Observe(__instance.Index, CommandDomain.Arrangement, CommandKind.Arrangement,
                    (int)order.OrderType, 0);
            }
        }
    }
}
