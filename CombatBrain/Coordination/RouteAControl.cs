namespace CombatBrain.Coordination
{
    using System;

    // Route A 的单槽对象注册表。对象引用而非 Index/哈希决定身份；同一实例用于生产和离线回归。
    public interface IRouteAWeights
    {
        object Identity { get; }
        object Behavior(int index);
        float GetWeight(int index);
        void SetWeight(int index, float value);
    }

    // CB-REPAIR-0005（只读诊断）：覆盖发生后「引擎是否仍选中 Route A 行为」只有游戏侧适配器能读。
    // 实现方只读，不得写任何游戏状态；读取抛异常由 RouteAControl 单点捕获并跳过（诊断不得影响控制流）。
    public interface IRouteASilenceProbe
    {
        int ActiveBehaviorIndex();    // 1..4 = 胜出者仍是 Route A 的第 i 个行为；0 = 不是；-1 = 不可读
        string ActiveBehaviorType();  // 胜出者类型名（ActiveBehaviorIndex() == 0 时有意义）
        bool AiObjectSame();          // FormationAI 对象是否仍是武装时捕获的那个
        bool AiControlled();
        int Units();
    }

    // 一行 [RouteASilence] 的自描述样本；时间口径由日志出口负责换算（不在这里做 -Infinity 算术）。
    public struct RouteASilenceSample
    {
        public long Session;
        public int Identity, Team, Formation, ProbeSeq, ActiveIndex, Units;
        public float Time, LastSelectedAt, ReplacedAt;
        public float Weight0, Weight1, Weight2, Weight3;
        public bool Final, AiObjectSame, AiControlled;
        public string ActiveType;
    }

    public enum RouteAExit
    {
        None, Disabled, ModeChanged, MissionEnding, Replaced, EmptyOrInvalid,
        PlayerOwned, LostAIControl, NotFieldBattle, NotInfantry, InvalidCommand,
        NavigationFailed, InitializationFailed, AdapterFailed, EvidenceCapacity, ForcedTick,
        ExternalOverridePersisted, SelectionTimeout
    }

    public struct RouteAEligibility
    {
        public bool Enabled, FieldBattle, Present, Alive, AiControlled, PlayerOwned, Infantry, ForcedTick;
        public RouteAExit Rejection
        {
            get
            {
                if (!Enabled) return RouteAExit.Disabled;
                if (ForcedTick) return RouteAExit.ForcedTick;
                if (!FieldBattle) return RouteAExit.NotFieldBattle;
                if (!Present || !Alive) return RouteAExit.EmptyOrInvalid;
                if (PlayerOwned) return RouteAExit.PlayerOwned;
                if (!AiControlled) return RouteAExit.LostAIControl;
                if (!Infantry) return RouteAExit.NotInfantry;
                return RouteAExit.None;
            }
        }
    }

    public sealed class RouteAControl
    {
        // CB-REPAIR-0004：所有权的可交付上限。armed 的单次边沿重试只能由引擎的选中回调（TrySelect）触发，
        // 而引擎只对 FormationAI 胜出的 ActiveBehavior 按固定 0.5s 节拍回调（FormationAI 计时器，
        // OfflineTests/RouteAStaticChecks.ps1 锁定其契约）；健康路径的覆盖重落地在 5s 内完成。
        // 10s = 20 拍无任何选中回调即判定「不可交付」——必须释放所有权，不得继续占位挡住 CB 执行器。
        public const float DeliveryDeadlineSeconds = 10f;

        // CB-REPAIR-0004 r3：交付超时后的「重新武装」只认具名、离散、可解释、有界的条件——真实战术意图变化
        // （Cps2RouteAIntent，由 Formation Mission/相位映射而来）+ 冷却 + 同一暂停期内次数上限。
        // 原始 situationSignal（位置/兵数/12 维向量/Army 浮点的连续变化）一律不得作为解锁依据：
        // Astra 在 60Hz、每拍 X+=0.001、零选中的条件下复现「10.000s 释放 → 10.0167s 重取」，
        // 占空比约 99.83%，普通执行器只拿到单帧窗口（P1 FAIL 根因）。
        // 冷却与一次交付期限等长：每次授予的尝试失败后，普通执行器至少获得一整个期限的连续写入窗口。
        public const float ReArmCooldownSeconds = 10f;
        // 同一暂停期内最多授予的重试次数：意图反复抖动（Hold↔Advance…）也不能把它变成周期占位。
        public const int MaxSuspendedReattempts = 2;

        // CB-REPAIR-0005（只读诊断）：覆盖后交付窗口内的断流采样上限与间隔。仅覆盖后才产生，关观测时零开销。
        public const int SilenceProbeLimit = 10;
        public const float SilenceProbeIntervalSeconds = 1f;

        public IRouteAWeights Target { get; private set; }
        public Cps2RouteAIntent Intent { get; private set; }
        public int IdentityId { get; private set; }
        public long Revision { get; private set; }
        public int RestoreFailures { get; private set; }
        public readonly RouteAEvidence Evidence = new RouteAEvidence();
        private readonly float[] _original = new float[4];
        private object _mission;
        private long _activation, _writtenRevision = -1, _writtenActivation = -1;
        private float _x, _y;
        private int _signal;
        private Cps2RouteAIntent _previousIntent;
        private float _firstChangeAt;
        private int _chainSignal;
        private bool _hasChange;
        private bool _externalRetryArmed, _externalRetryUsed;
        private float _deliveredAt;          // 最近一次「引擎可交付」时刻：武装激活或真实选中回调
        private bool _deliverySuspended;     // 交付超时释放后让出所有权，直到出现合格离散事件
        private Cps2RouteAIntent _suspendedIntent; // 暂停时锁存的战术意图：只有它「变化」才算合格事件
        private float _suspendedAt;          // 本次暂停起点：重新武装的冷却基准
        private int _suspendedReattempts;    // 本暂停期内已授予的重试次数（上限 MaxSuspendedReattempts）
        // CB-REPAIR-0005：覆盖时刻 + 探针节拍/计数。只在 _externalRetryArmed 期间使用。
        private float _silenceAt, _silenceNextAt;
        private int _silenceProbes;
        public int IntentChanges { get; private set; }
        public int Flaps { get; private set; }

        public RouteAControl()
        {
            Evidence.ExternalWriteObserved = OnExternalWriteObserved;
        }

        public void BeginSession(object mission, float now)
        {
            if (ReferenceEquals(_mission, mission)) return;
            Release(RouteAExit.MissionEnding, now);
            _mission = mission;
            Evidence.BeginSession();
            RestoreFailures = 0;
            IntentChanges = Flaps = 0;
            Revision = _activation = 0;
            _signal = _chainSignal = 0;
            _writtenRevision = _writtenActivation = -1;
            _externalRetryArmed = false;
            _externalRetryUsed = false;
            _deliveredAt = now;
            // r2 裁决：MissionEnding 不依赖 Release 解锁——暂停期间 Target 已为 null，Release 是 no-op，
            // 因此这里直接归零，保证下一场战斗从干净状态开始（新会话 = 新局面）。
            _deliverySuspended = false;
            _suspendedIntent = Cps2RouteAIntent.None;
            _suspendedAt = now;
            _suspendedReattempts = 0;
            _silenceAt = _silenceNextAt = now;
            _silenceProbes = 0;
        }

        public bool Owns(object identity) { return Target != null && ReferenceEquals(Target.Identity, identity); }
        public static bool BlocksExecutor(bool owned, CommandDomain domain)
        { return owned && domain == CommandDomain.Movement; }

        // 必须在新候选枚举前调用；失踪/清空对象同样到达 Restore。
        public void Reconcile(RouteAEligibility eligibility, float now)
        {
            if (Target != null && eligibility.Rejection != RouteAExit.None)
                Release(eligibility.Rejection, now);
        }

        public bool Update(IRouteAWeights target, RouteAEligibility eligibility, Cps2RouteAIntent intent,
            bool commandValid, float x, float y, int situationSignal, int team, int formation, float now)
        {
            if (Target != null && (target == null || !ReferenceEquals(Target.Identity, target.Identity)))
                Release(RouteAExit.Replaced, now);
            RouteAExit reject = eligibility.Rejection;
            if (reject == RouteAExit.None && (!commandValid || intent == Cps2RouteAIntent.None))
                reject = intent == Cps2RouteAIntent.Rejoin ? RouteAExit.NavigationFailed : RouteAExit.InvalidCommand;
            if (target == null || reject != RouteAExit.None)
            {
                Release(reject, now);
                return false;
            }
            // ── CB-REPAIR-0004（运行期 run=b0e2e0a4 / session 4）：外部覆盖触发 armed 单次边沿重试后，
            // 引擎在 101s（764 任务秒）内一次都没回调 TrySelect（0 次 BehaviorSelected）→ 重试永不执行，
            // 而 Target 仍被持有 → BlocksExecutor 持续挡住 CB 自身 Movement 执行（FormationCommandExecutor:117）
            // = 占位不开火。处置：超过交付上限仍无可交付回调 → 释放所有权（恢复原权重、解除执行器阻塞）。
            // 本检查搭在既有每拍入口上（ApplyRouteASingle → Update），不新增调用点、不新增每帧成本。
            bool grantedReattempt = false;
            if (_deliverySuspended)
            {
                // r3：让出所有权后，只有「具名、离散、可解释、有界」的事件才允许重新武装：
                //   ① 真实战术意图变化（intent != 暂停时锁存的 _suspendedIntent）；
                //   ② 已过冷却 ReArmCooldownSeconds（保证普通执行器至少拿到一整段连续窗口）；
                //   ③ 本暂停期内已授予次数 < MaxSuspendedReattempts（防意图抖动变成周期占位）。
                // 三条任一不满足 → 继续让位。原始 situationSignal 不参与判定（连续微变不得解锁）。
                if (intent == _suspendedIntent || now - _suspendedAt < ReArmCooldownSeconds
                    || _suspendedReattempts >= MaxSuspendedReattempts)
                {
                    _signal = situationSignal; // 仅维护抖动链账本新鲜度，不作解锁依据
                    return false;
                }
                _deliverySuspended = false;
                _suspendedReattempts++;        // 合格事件最多一次新交付尝试；失败后由下面的超时分支重新暂停
                grantedReattempt = true;
            }
            else if (Target != null && now - _deliveredAt >= DeliveryDeadlineSeconds)
            {
                // r2 口径（Captain 裁决 + 陷阱说明）：交付上限自身的这次 Release 也是「真实释放」
                //（previous != null），按 r2 规则会清掉暂停标记。这里采用**先清后设**顺序：先 Release
                //（内部在真实释放分支清 _deliverySuspended）再置 true，因此暂停不会被自己的释放清掉。
                // 若将来把置位提前、或在 Release 内按 reason 排除 SelectionTimeout，必须同步修改此处，
                // 否则暂停立即失效 → Update 下一拍就重新取得 → 周期占位回归。
                // CB-REPAIR-0005：释放瞬间再采一拍，用于看到「交付上限到期时谁在胜出」。
                if (_externalRetryArmed && Evidence.Capture) SilenceProbe(now, true);
                Release(RouteAExit.SelectionTimeout, now);
                _signal = situationSignal;
                _deliverySuspended = true;
                _suspendedIntent = intent;     // 锁存离散意图：只有它的真实变化才算合格事件
                _suspendedAt = now;
                return false;
            }
            if (Target == null)
            {
                int id = Evidence.Register(target.Identity, team, formation);
                if (id == 0) return false; // 有界证据表满时 fail-open，不能静默丢身份。
                // 先捕获完整原值，再修改任何权重；初始化失败不留部分改写。
                for (int i = 0; i < 4; i++) _original[i] = target.GetWeight(i);
                Target = target;
                IdentityId = id;
                Intent = Cps2RouteAIntent.None;
                _hasChange = false;
                _writtenRevision = _writtenActivation = -1;
                _externalRetryArmed = false;
                _externalRetryUsed = false;
                // 每次取得所有权都重开一个交付窗口（不能继承上一次武装/释放前的时间戳）。
                _deliveredAt = now;
                Evidence.Activate(id);
                // r3 可观测原因：暂停期内授权的那次重新武装，与紧随其后的 Intent 事件成对阅读
                //（后者带该次尝试的真实意图与 revision）。
                if (grantedReattempt) Evidence.SuspensionRetry(id, Revision, now);
            }
            // CB-REPAIR-0005：覆盖后引擎未恢复选中期间，每 ≥1s 采一拍（上限 SilenceProbeLimit）。
            if (_externalRetryArmed && Evidence.Capture && now >= _silenceNextAt && _silenceProbes < SilenceProbeLimit)
            {
                _silenceNextAt = now + SilenceProbeIntervalSeconds;
                SilenceProbe(now, false);
            }
            bool changed = intent != Intent || Math.Abs(x - _x) >= 5f || Math.Abs(y - _y) >= 5f;
            if (changed)
            {
                if (Intent != Cps2RouteAIntent.None && intent != Intent)
                {
                    IntentChanges++;
                    bool flap = _hasChange && intent == _previousIntent && now - _firstChangeAt <= 2f
                        && situationSignal == _chainSignal && _signal == situationSignal;
                    if (flap) Flaps++;
                    Evidence.IntentChanged(IdentityId, flap);
                    _previousIntent = Intent;
                    _firstChangeAt = now;
                    _chainSignal = _signal;
                    _hasChange = true;
                }
                Revision++;
                Intent = intent;
                _x = x; _y = y;
                Evidence.Intent(IdentityId, intent, Revision, now, situationSignal, x, y);
            }
            // 任何采样时刻的态势变化都会破坏“无态势变化”链，包含意图未变化的中间拍。
            if (_signal != situationSignal) _hasChange = false;
            _signal = situationSignal;
            try
            {
                int chosen = (int)intent - 1;
                for (int i = 0; i < 4; i++)
                {
                    float desired = i == chosen ? 10f : 0f;
                    if (target.GetWeight(i) != desired) target.SetWeight(i, desired);
                }
            }
            catch (Exception) { Release(RouteAExit.InitializationFailed, now); return false; }
            return true;
        }

        public void Activated(object behavior)
        {
            if (Target != null && ReferenceEquals(Target.Behavior((int)Intent - 1), behavior)) _activation++;
        }

        // 只允许真实胜出回调调用。预计算不接此入口。重复 Tick 不重发相同意图/目标。
        public bool TrySelect(object identity, object behavior, object activeBehavior,
            RouteAEligibility eligibility, bool commandValid, float now)
        {
            if (!Owns(identity)) return false;
            if (eligibility.Rejection != RouteAExit.None || !commandValid)
            {
                Release(eligibility.Rejection != RouteAExit.None ? eligibility.Rejection : RouteAExit.InvalidCommand, now);
                return false;
            }
            if (!ReferenceEquals(behavior, activeBehavior)
                || !ReferenceEquals(behavior, Target.Behavior((int)Intent - 1)))
            { Release(RouteAExit.InvalidCommand, now); return false; }
            Evidence.Selected(IdentityId, Revision, now);
            _deliveredAt = now; // 引擎可交付：刷新交付上限时钟；armed 重试正是依赖此回调执行。
            // r3：引擎确实还在回调 → 占位风险消失，重置暂停期重试预算（下一轮超时从零开始计数）。
            _suspendedReattempts = 0;
            bool shouldWrite = _writtenRevision != Revision || _writtenActivation != _activation;
            if (shouldWrite && _externalRetryArmed)
            {
                // CB-REPAIR-0004 r2 语义：EdgeRetry 记「授权次数」而非「落地次数」——此处只消费覆盖后授权的
                // 单次重试机会；真正落地由紧随其后的 Adapter 写入决定（成功 → OrderApplied，失败 → AdapterFailed）。
                // 因此 edgeRetries=1 且 applied 未增 ≠ 落地成功，判定落地必须看 OrderApplied/UnverifiedApply。
                _externalRetryArmed = false;
                Evidence.EdgeRetry(IdentityId, now);
            }
            return shouldWrite;
        }

        public void Applied(float now, bool verified, float? x = null, float? y = null)
        {
            if (Target == null) return;
            _writtenRevision = Revision; _writtenActivation = _activation;
            Evidence.Applied(IdentityId, Revision, now, verified, x ?? _x, y ?? _y);
        }

        private void OnExternalWriteObserved(int id, bool replaced, float now)
        {
            if (Target == null || id != IdentityId) return;
            if (replaced)
            {
                if (!_externalRetryUsed)
                {
                    // One edge-triggered retry only. Invalidate the dedupe receipt without changing
                    // tactical revision/activation; the next real selected callback may write once.
                    _externalRetryArmed = true;
                    _externalRetryUsed = true;
                    _writtenRevision = _writtenActivation = -1;
                    // CB-REPAIR-0005：断流探针以本时刻为起点（覆盖时刻由外部 setter 观测给出）。
                    _silenceAt = now;
                    _silenceNextAt = now + SilenceProbeIntervalSeconds;
                    _silenceProbes = 0;
                    return;
                }
                // 重试已消耗后再次 replaced=true 覆盖 = 持续覆盖 → 立即 Release。
                Release(RouteAExit.ExternalOverridePersisted, now);
                return;
            }
            // replaced=false（ExternalWhileActive）：独立外部写入，明确不算「持续覆盖」——
            // 不 Release、不消耗重试；仅由证据层计数 ExternalWhileActive。
        }

        // CB-REPAIR-0005 r1（Astra P1）：整个诊断边界都视为不可信——探针读取、样本组装、
        // SilenceObserved/日志出口抛出的异常一律不得逃逸到 Route A 控制流。此前输出回调在 catch 之外：
        // 终拍异常会打断「先探针、后 Release(SelectionTimeout)」的次序，超时释放被跳过、外层按异常
        // 释放为 InitializationFailed → 下一拍可立即重取（破坏暂停语义）。
        // 本 catch 只包诊断语句，内部不含任何控制流语句（权重/暂停/预算/释放都在别处），
        // 因此不会吞掉真实控制逻辑错误。终拍仍在释放前取时刻，保留可解释的时间口径。
        private void SilenceProbe(float now, bool final)
        {
            try
            {
                float w0 = Target.GetWeight(0); float w1 = Target.GetWeight(1);
                float w2 = Target.GetWeight(2); float w3 = Target.GetWeight(3);
                int activeIndex = -1, units = -1;
                string activeType = "unreadable";
                bool aiSame = false, aiCtl = false;
                IRouteASilenceProbe probe = Target as IRouteASilenceProbe;
                if (probe != null)
                {
                    activeIndex = probe.ActiveBehaviorIndex();
                    activeType = probe.ActiveBehaviorType();
                    aiSame = probe.AiObjectSame();
                    aiCtl = probe.AiControlled();
                    units = probe.Units();
                }
                _silenceProbes++;
                Evidence.Silence(IdentityId, new RouteASilenceSample
                {
                    Time = now, Final = final, ProbeSeq = _silenceProbes, ReplacedAt = _silenceAt,
                    LastSelectedAt = Evidence.LastSelectedAtFor(IdentityId),
                    ActiveIndex = activeIndex, ActiveType = activeType,
                    AiObjectSame = aiSame, AiControlled = aiCtl, Units = units,
                    Weight0 = w0, Weight1 = w1, Weight2 = w2, Weight3 = w3
                });
            }
            catch (Exception) { }
        }

        public void Release(RouteAExit reason, float now)
        {
            IRouteAWeights previous = Target;
            // CB-REPAIR-0004 r2：只有「真实释放」才结束交付暂停。此前放在方法入口，会让候选扇描失败拍的空释放
            // （ApplyRouteASingle 无候选 → Release(EmptyOrInvalid)，此时 Target 已为 null）立即解锁，
            // 暂停形同虚设、占位以周期形式回归。指定 reason（Disabled/ModeChanged/MissionEnding）的主动解锁
            // 需 Captain 裁决后再加；Mission 轮换由 BeginSession 重置（见该方法）。
            if (previous == null) return;
            _deliverySuspended = false;
            // r3：交付超时自身的释放仍属同一暂停期（重试预算继续累计，防周期占位）；其它真实释放意味着
            // 局面已被外部事件改变（热关闭/资格门/替换等）→ 暂停期结束，重试预算归零。
            if (reason != RouteAExit.SelectionTimeout) _suspendedReattempts = 0;
            int id = IdentityId;
            Target = null; // 在任何恢复回调之前撤销权限，并记录 Exit。
            Evidence.Exit(id, reason, now);
            for (int i = 0; i < 4; i++)
            {
                try { previous.SetWeight(i, _original[i]); }
                catch (Exception) { RestoreFailures++; } // 已销毁对象安全移除，其他权重仍尝试恢复。
            }
            Evidence.Restored(id, reason, now, RestoreFailures);
            Intent = Cps2RouteAIntent.None;
            IdentityId = 0;
            _hasChange = false;
            _externalRetryArmed = false;
            _externalRetryUsed = false;
        }

        public void EndSession(float now)
        {
            Release(RouteAExit.MissionEnding, now);
            _mission = null;
        }
    }

    // 态势变更号来自值比较，不来自有碰撞的哈希/粗略兵力档位；零逐拍分配。
    public struct RouteASituation
    {
        public CombatBrain.Intent.SituationVector Vector;
        public ArmyContextSnapshot Army;
        public CombatBrain.Battle.BattlePhase Phase;
        public int Count;
        public float X, Y;
        public bool Contact;
        public bool SameAs(RouteASituation other)
        {
            if (Phase != other.Phase || Count != other.Count || X != other.X || Y != other.Y || Contact != other.Contact) return false;
            for (int i = 0; i < CombatBrain.Intent.SituationVector.DimensionCount; i++)
                if (Vector[i] != other.Vector[i]) return false;
            return Army.MainBodyX == other.Army.MainBodyX && Army.MainBodyY == other.Army.MainBodyY
                && Army.MainBodyStrength == other.Army.MainBodyStrength && Army.MainBodyConfidence == other.Army.MainBodyConfidence
                && Army.HasOperationalObjective == other.Army.HasOperationalObjective
                && Army.ObjectiveFormationId == other.Army.ObjectiveFormationId
                && Army.ObjectiveX == other.Army.ObjectiveX && Army.ObjectiveY == other.Army.ObjectiveY
                && Army.ObjectiveStrength == other.Army.ObjectiveStrength;
        }
    }
    public sealed class RouteASituationStamp
    {
        private object _identity;
        private RouteASituation _last;
        private int _revision;
        public int Update(object identity, RouteASituation value)
        {
            if (!ReferenceEquals(_identity, identity) || !_last.SameAs(value)) _revision++;
            _identity = identity; _last = value; return _revision;
        }
        public void Reset() { _identity = null; _last = default(RouteASituation); _revision = 0; }
    }
}
