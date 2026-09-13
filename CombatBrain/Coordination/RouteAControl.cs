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
        private bool _deliverySuspended;     // 交付超时释放后暂停重新取得所有权，直到局势变化号更新
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
            _deliverySuspended = false;
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
            // = 占位不开火。处置：超过交付上限仍无可交付回调 → 释放所有权（恢复原权重、解除执行器阻塞）并
            // 暂停重新取得，直到局势变化号更新（真正的战术变化）才重试一次；期间由原版 AI 继续指挥（fail-open）。
            // 本检查搭在既有每拍入口上（ApplyRouteASingle → Update），不新增调用点、不新增每帧成本。
            if (_deliverySuspended)
            {
                if (situationSignal == _signal) return false;
                _deliverySuspended = false;
            }
            else if (Target != null && now - _deliveredAt >= DeliveryDeadlineSeconds)
            {
                Release(RouteAExit.SelectionTimeout, now);
                _signal = situationSignal;
                _deliverySuspended = true;
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
            bool shouldWrite = _writtenRevision != Revision || _writtenActivation != _activation;
            if (shouldWrite && _externalRetryArmed)
            {
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
                    return;
                }
                // 重试已消耗后再次 replaced=true 覆盖 = 持续覆盖 → 立即 Release。
                Release(RouteAExit.ExternalOverridePersisted, now);
                return;
            }
            // replaced=false（ExternalWhileActive）：独立外部写入，明确不算「持续覆盖」——
            // 不 Release、不消耗重试；仅由证据层计数 ExternalWhileActive。
        }

        public void Release(RouteAExit reason, float now)
        {
            // 显式释放（资格门、热关闭、会话结束等）终结交付暂停：下一次 Update 允许重新尝试取得所有权。
            _deliverySuspended = false;
            IRouteAWeights previous = Target;
            if (previous == null) return;
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
