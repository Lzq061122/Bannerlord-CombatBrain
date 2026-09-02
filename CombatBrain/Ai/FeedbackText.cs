namespace CombatBrain.Ai
{
    using CombatBrain.Cavalry;
    using CombatBrain.Morale;
    using TaleWorlds.MountAndBlade;

    // 左下角反馈文案映射（t46 通俗化）：英文状态名→中文、阵营 StringId→中文名。
    // 纯静态只读映射，无状态、无游戏写入（反作弊闸不变）；文案约束 ≤20 字/条。
    // 阵营名沿用 t44 的 StringId 方案（避开未引用的 TaleWorlds.Localization/TextObject）。
    public static class FeedbackText
    {
        // 骑兵 FSM 状态中文名（枚举定义见 CavalryWaveFSM.cs L6-13）
        public static string Cavalry(CavalryState state)
        {
            switch (state)
            {
                case CavalryState.Scout: return "侦察";
                case CavalryState.Setup: return "列阵";
                case CavalryState.Charge: return "冲锋";
                case CavalryState.Recall: return "回撤";
                case CavalryState.Reform: return "重整";
                default: return state.ToString();
            }
        }

        // 士气撤退 FSM 阶段中文名（枚举定义见 PhasedRetreat.cs L5-11）
        public static string Morale(RetreatState state)
        {
            switch (state)
            {
                case RetreatState.Hold: return "坚守";
                case RetreatState.RequestRetreat: return "请求撤退";
                case RetreatState.Disengage: return "脱离交战";
                case RetreatState.Reform: return "重整队伍";
                default: return state.ToString();
            }
        }

        // 意图中文名（枚举定义见 Enums.cs L3-17）
        public static string Intent(IntentType intent)
        {
            switch (intent)
            {
                case IntentType.Attack: return "全线进攻";
                case IntentType.Defend: return "就地防守";
                case IntentType.Retreat: return "撤退";
                case IntentType.Flank: return "包抄侧翼";
                case IntentType.Harass: return "骚扰消耗";
                case IntentType.Reform: return "收拢重整";
                case IntentType.Pursue: return "追击残敌";
                case IntentType.ProtectArcher: return "保护射手";
                case IntentType.BraceFormation: return "架枪反骑";
                default: return intent.ToString();
            }
        }

        // 阵营中文名：通用（GeneralAgent）所属文化 StringId → 中文。
        // 读不到 → 我军/敌军兜底；未映射文化原样展示（仍比 Team 1 可读）。
        public static string Faction(Team team)
        {
            string sid = null;
            try
            {
                Agent general = team != null ? team.GeneralAgent : null;
                if (general != null && general.Character != null && general.Character.Culture != null)
                    sid = general.Character.Culture.StringId;
            }
            catch { }
            switch (sid)
            {
                case "empire": return "帝国";
                case "aserai": return "阿塞莱";
                case "battania": return "巴旦尼亚";
                case "khuzait": return "库赛特";
                case "sturgia": return "斯特吉亚";
                case "vlandia": return "瓦兰迪亚";
                case "looter": return "掠夺者";
            }
            if (sid != null) return sid;
            return team != null && team.IsPlayerTeam ? "我军" : "敌军";
        }
    }
}
