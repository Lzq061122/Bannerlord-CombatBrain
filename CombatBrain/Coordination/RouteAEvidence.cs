namespace CombatBrain.Coordination
{
    using System;
    using System.Globalization;

    public enum RouteAEventKind { Intent, BehaviorSelected, OrderApplied, UnverifiedApply, SetterBegin,
        SetterCompleted, SetterSkipped, ExternalReplaced, ExternalWhileActive, ExternalAfterExit,
        EdgeRetry, Exit, Restore, SuspensionRetry }

    public struct RouteAEvent
    {
        public long Session, Seq, SetterSeq, ObserverSeq, RelatedSeq, Revision;
        public int Identity, Team, Formation, OrderType, Signal;
        public float Time;
        public float X, Y;
        public CommandDomain Domain;
        public CommandSource Source;
        public Cps2RouteAIntent Intent;
        public RouteAEventKind Kind;
        public RouteAExit Reason;
    }

    public struct RouteASetterToken
    {
        public long Session, Seq, ObserverSeq;
        public int Identity, OrderType;
        public float Time;
        public bool Direct;
        public CommandSource Source;
    }

    // 同步消费真实 setter，不扫描会被覆盖的 CPS-0 ring。固定容量；溢出明确标记证据失效。
    public sealed class RouteAEvidence
    {
        private sealed class Stats
        {
            public WeakReference Object;
            public int Team, Formation;
            public long Intents, Selected, Applied, Replaced, ExternalWhileActive, EdgeRetries,
                Unverified, LastApplied, ExitSeq, Revision, SuspensionRetries;
            public int Changes, Flaps;
            public float X, Y;
            public bool Active, Pending;
            // CB-REPAIR-0004 r2：用具名 bool 而非 FirstExternalAt == 0f 作「尚无外部写」哨兵——首拍
            //（now == 0f）的首次外部写入会被 0f 哨兵当成未初始化，第二次写入再覆盖起点，覆盖时长被吞掉。
            public bool HasExternal;
            public float FirstExternalAt, LastExternalAt, ExternalDuration;
            // 同一理由：LastSelectedAt 以 -Infinity 初值区分「本 session 从未被选中」与「在 now == 0f 被选中」。
            public float LastSelectedAt;
            public Cps2RouteAIntent Intent;
        }
        public const int IdentityCapacity = 64;
        public const int EventCapacity = 2048;
        private readonly Stats[] _stats = new Stats[IdentityCapacity];
        private readonly RouteAEvent[] _events = new RouteAEvent[EventCapacity];
        private int _identities, _head, _count;
        private long _seq;
        private long _setterSeq;
        private object _expected;
        private long _directSeq = -1;
        private int _directOrderType = -1;
        private bool _directCompleted;
        public long Session { get; private set; }
        public long Dropped { get; private set; }
        private bool _capture;
        public bool Capture
        {
            get { return _capture; }
            set
            {
                if (_capture && !value && _identities > 0)
                {
                    Dropped++; // 中断观测后本 session 不再是完整验收样本。
                    for (int i = 0; i < _identities; i++) _stats[i].Pending = false;
                }
                _capture = value;
            }
        }
        public int Count { get { return _count; } }
        public int Identities { get { return _identities; } }
        public bool DirectCompleted { get { return _directCompleted; } }
        public Action<int, bool, float> ExternalWriteObserved;

        public void BeginSession()
        {
            Session++;
            Array.Clear(_stats, 0, _stats.Length);
            Array.Clear(_events, 0, _events.Length);
            _identities = _head = _count = 0;
            _seq = _setterSeq = Dropped = 0;
            _expected = null;
            _directSeq = -1;
            _directCompleted = false;
        }

        private int Find(object identity)
        {
            for (int i = 0; i < _identities; i++)
                if (ReferenceEquals(_stats[i].Object.Target, identity)) return i + 1;
            return 0;
        }
        public int Register(object identity, int team, int formation)
        {
            int id = Find(identity);
            if (id != 0) return id;
            if (identity == null || _identities == IdentityCapacity) { Dropped++; return 0; }
            _stats[_identities] = new Stats { Object = new WeakReference(identity), Team = team, Formation = formation,
                LastSelectedAt = float.NegativeInfinity };
            return ++_identities;
        }
        private Stats Get(int id) { return id > 0 && id <= _identities ? _stats[id - 1] : null; }
        public void Activate(int id) { Stats s = Get(id); if (s != null) { s.Active = true; s.Pending = false; } }
        public void Intent(int id, Cps2RouteAIntent intent, long revision, float now, int signal, float x, float y)
        {
            Stats s = Get(id); if (s == null) return;
            s.Intent = intent; s.Revision = revision; s.Intents++;
            s.X = x; s.Y = y;
            Record(id, RouteAEventKind.Intent, now, revision: revision, signal: signal);
        }
        public void Selected(int id, long revision, float now)
        {
            Stats s = Get(id); if (s == null) return;
            s.Selected++;
            // CB-REPAIR-0004：占位不开火的判定量——armed 期间再无选中回调时，该时刻远早于 ExternalReplaced。
            s.LastSelectedAt = now;
            Record(id, RouteAEventKind.BehaviorSelected, now, revision: revision);
        }
        public void IntentChanged(int id, bool flap)
        { Stats s = Get(id); if (s != null) { s.Changes++; if (flap) s.Flaps++; } }
        public void ExpectSetter(object identity)
        { _expected = identity; _directSeq = -1; _directCompleted = false; }
        public void CancelExpectedSetter() { _expected = null; }

        public RouteASetterToken SetterBeginning(object identity, CommandDomain domain, CommandSource source,
            long setterSeq, int orderType, float now)
        {
            if (domain != CommandDomain.Movement || identity == null) return default(RouteASetterToken);
            long routeSetterSeq = ++_setterSeq; // 不依赖 CPS-0 Enable/Clear 时会重置的序号。
            int id = Find(identity);
            if (id == 0) return default(RouteASetterToken);
            bool direct = ReferenceEquals(_expected, identity) && source == CommandSource.CombatBrainRouteA;
            if (direct) { _expected = null; _directSeq = routeSetterSeq; _directOrderType = orderType; }
            // 同一 scope 内重入的 setter 不是 Adapter 的直接调用，不能冒充 RouteA。
            if (!direct && source == CommandSource.CombatBrainRouteA) source = CommandSource.ExternalUnknown;
            Record(id, RouteAEventKind.SetterBegin, now, routeSetterSeq, source: source, orderType: orderType, observer: setterSeq);
            return new RouteASetterToken { Session = Session, Identity = id, Seq = routeSetterSeq, ObserverSeq = setterSeq,
                Time = now, Source = source, Direct = direct, OrderType = orderType };
        }

        // 只读 Postfix：Prefix 不能证明被其它 Harmony Prefix 跳过的原方法已经执行。
        public void SetterCompleted(RouteASetterToken token, bool originalRan, float now)
        {
            if (token.Identity == 0 || token.Session != Session) return;
            Stats s = Get(token.Identity); if (s == null) return;
            Record(token.Identity, originalRan ? RouteAEventKind.SetterCompleted : RouteAEventKind.SetterSkipped,
                now, token.Seq, source: token.Source, orderType: token.OrderType, observer: token.ObserverSeq);
            if (!originalRan) return;
            if (token.Direct) { _directCompleted = token.Seq == _directSeq; return; }
            if (s.Active)
            {
                bool replaced = s.Pending;
                if (replaced) { s.Replaced++; s.Pending = false; }
                else s.ExternalWhileActive++;
                if (!s.HasExternal) { s.HasExternal = true; s.FirstExternalAt = now; }
                s.LastExternalAt = now;
                Record(token.Identity, replaced ? RouteAEventKind.ExternalReplaced : RouteAEventKind.ExternalWhileActive,
                    now, token.Seq, s.LastApplied, source: token.Source, orderType: token.OrderType, observer: token.ObserverSeq);
                Action<int, bool, float> notify = ExternalWriteObserved;
                if (notify != null) notify(token.Identity, replaced, now);
            }
            else if (s.ExitSeq != 0)
                Record(token.Identity, RouteAEventKind.ExternalAfterExit, now, token.Seq, s.ExitSeq,
                    source: token.Source, orderType: token.OrderType, observer: token.ObserverSeq);
        }

        public void Applied(int id, long revision, float now, bool verified, float x, float y)
        {
            Stats s = Get(id); if (s == null) return;
            s.X = x; s.Y = y;
            if (verified)
            {
                s.Applied++;
                s.LastApplied = Record(id, RouteAEventKind.OrderApplied, now, _directSeq, revision: revision,
                    source: CommandSource.CombatBrainRouteA, orderType: _directOrderType);
                s.Pending = true;
            }
            else
            {
                s.Unverified++;
                s.Pending = false;
                Record(id, RouteAEventKind.UnverifiedApply, now, revision: revision,
                    source: CommandSource.CombatBrainRouteA);
            }
        }
        public void Exit(int id, RouteAExit reason, float now)
        {
            Stats s = Get(id); if (s == null) return;
            s.Active = false; s.Pending = false;
            // F-1 口径统一：以最后一次外部覆盖时间结算，不用退出时刻——无关原因退出不夸大 Observed 时长。
            if (s.HasExternal) s.ExternalDuration = Math.Max(0f, s.LastExternalAt - s.FirstExternalAt);
            s.ExitSeq = Record(id, RouteAEventKind.Exit, now, reason: reason);
        }
        public void EdgeRetry(int id, float now)
        {
            // CB-REPAIR-0004 r2 语义：计数的是「授权次数」（覆盖后授权的那一次边沿重试被消费），不是「落地次数」；
            // 落地与否看随后的 OrderApplied / UnverifiedApply（见 RouteAControl.TrySelect 同处注释）。
            Stats s = Get(id); if (s == null) return;
            s.EdgeRetries++;
            Record(id, RouteAEventKind.EdgeRetry, now, revision: s.Revision);
        }
        // CB-REPAIR-0004 r3：交付暂停期内「授予一次重新武装」的可观测原因（具名离散事件：真实战术意图变化
        // + 冷却 + 次数上限）。与紧随其后的 Intent 事件成对阅读，即可解释该次尝试的意图与 revision。
        public void SuspensionRetry(int id, long revision, float now)
        {
            Stats s = Get(id); if (s == null) return;
            s.SuspensionRetries++;
            Record(id, RouteAEventKind.SuspensionRetry, now, revision: revision);
        }
        public void Restored(int id, RouteAExit reason, float now, int failures)
        { Record(id, RouteAEventKind.Restore, now, reason: reason, signal: failures); }
        public long AppliedFor(int id) { Stats s = Get(id); return s == null ? 0 : s.Applied; }
        public long ReplacedFor(int id) { Stats s = Get(id); return s == null ? 0 : s.Replaced; }
        public long StableFor(int id) { return AppliedFor(id) - ReplacedFor(id); }

        private long Record(int id, RouteAEventKind kind, float now, long setter = -1, long related = 0,
            long revision = 0, CommandSource source = CommandSource.ExternalUnknown, int orderType = -1,
            RouteAExit reason = RouteAExit.None, int signal = 0, long observer = -1)
        {
            Stats s = Get(id);
            long seq = ++_seq;
            if (!Capture || s == null) return seq;
            if (_count == EventCapacity) { Dropped++; return seq; }
            _events[(_head + _count++) % EventCapacity] = new RouteAEvent { Session = Session, Seq = seq,
                Identity = id, Team = s.Team, Formation = s.Formation, Domain = CommandDomain.Movement,
                Time = now, Kind = kind, SetterSeq = setter, RelatedSeq = related, Revision = revision,
                Intent = s.Intent, Source = source, OrderType = orderType, Reason = reason, Signal = signal,
                ObserverSeq = observer, X = s.X, Y = s.Y };
            return seq;
        }
        public bool TryRead(out RouteAEvent item)
        {
            if (_count == 0) { item = default(RouteAEvent); return false; }
            item = _events[_head]; _head = (_head + 1) % EventCapacity; _count--; return true;
        }
        public string BuildSummary(int id)
        {
            Stats s = Get(id); if (s == null) return "";
            // edgeRetries 语义 = 授权次数（非落地次数）；lastSelectedAt 的 never 只表示本 session 从未被选中，
            // 首拍（now == 0f）被选中会如实打印 0.000s（见 Stats.LastSelectedAt 的 -Infinity 初值）。
            return string.Format(CultureInfo.InvariantCulture,
                "session={0} identity={1} team={2} f={3} domain=Movement intent={4} selected={5} applied={6} replaced={7} externalWhileActive={8} edgeRetries={9} externalDuration={10:F3}s stableSoFar={11} unverified={12} dropped={13} intentChange={14} weightFlap={15} lastSelectedAt={16} suspensionRetries={17}",
                Session, id, s.Team, s.Formation, s.Intents, s.Selected, s.Applied, s.Replaced,
                s.ExternalWhileActive, s.EdgeRetries,
                s.Active && s.HasExternal ? Math.Max(0f, s.LastExternalAt - s.FirstExternalAt) : s.ExternalDuration,
                s.Applied - s.Replaced, s.Unverified, Dropped, s.Changes, s.Flaps,
                float.IsNegativeInfinity(s.LastSelectedAt) ? "never"
                    : s.LastSelectedAt.ToString("F3", CultureInfo.InvariantCulture) + "s",
                s.SuspensionRetries);
        }
    }
}
