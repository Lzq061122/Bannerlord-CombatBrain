namespace CombatBrain.Ai
{
    using System.Collections.Generic;
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

        // t62 战术层态势摘要：9 意图各配一句预设短语（随战术名一同播报，让玩家知道「为什么打这个战术」）。
        // 纯静态映射只读；短语 ≤12 字，与战术名拼后整条 ≤20 字。
        public static string TacticBrief(IntentType intent)
        {
            switch (intent)
            {
                case IntentType.Attack: return "正面压上，全线接敌";
                case IntentType.Defend: return "就地固守，稳住阵线";
                case IntentType.Retreat: return "全线后撤，脱离接敌";
                case IntentType.Flank: return "敌侧翼暴露，骑兵绕后";
                case IntentType.Harass: return "骑兵游击，消耗敌射手";
                case IntentType.Reform: return "收拢队伍，恢复建制";
                case IntentType.Pursue: return "敌军已溃，追击残敌";
                case IntentType.ProtectArcher: return "敌射手威胁大，全军护射";
                case IntentType.BraceFormation: return "敌骑逼近，步兵架枪";
                default: return string.Empty;
            }
        }

        // 阵营中文名：文化 StringId → 中文（CultureId 士兵多数决，见下）。
        // 读不到 → 我军/敌军兜底；未映射文化原样展示（仍比 Team 1 可读）。
        public static string Faction(Team team)
        {
            string sid = CultureId(team);
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

        // t54 阵营判定（士兵文化多数决）：GeneralAgent 文化在自定义战斗不可靠——可为 null 或
        // 与士兵不同阵营（实测选帝国显示瓦兰迪亚、日志读空 阵营=-）。改为遍历各编队士兵统计
        // 文化票数取多数；无兵 → GeneralAgent 兜底 → null（调用方自行再兜底）。
        // 反作弊闸：文化=公开只读属性；调用均为事件驱动低频（FSM 跃迁/意图切换），O(兵数) 可接受。
        // API 核实（v1.4.8 反编译源 Formation.cs L1166）：GetUnitsWithoutDetachedOnes() 枚举全部士兵。
        public static string CultureId(Team team)
        {
            if (team == null) return null;
            string best = null;
            int bestVotes = 0;
            Dictionary<string, int> votes = new Dictionary<string, int>();
            try
            {
                foreach (Formation f in team.FormationsIncludingEmpty)
                {
                    if (f == null || f.CountOfUnits == 0) continue;
                    foreach (Agent a in f.GetUnitsWithoutDetachedOnes())
                    {
                        if (a == null || a.Character == null || a.Character.Culture == null) continue;
                        string sid = a.Character.Culture.StringId;
                        if (string.IsNullOrEmpty(sid)) continue;
                        int v;
                        votes.TryGetValue(sid, out v);
                        v++;
                        votes[sid] = v;
                        if (v > bestVotes)
                        {
                            bestVotes = v;
                            best = sid;
                        }
                    }
                }
            }
            catch { }
            if (best != null) return best;
            try
            {
                Agent general = team.GeneralAgent; // 兜底：无活兵时才用将军文化
                if (general != null && general.Character != null && general.Character.Culture != null)
                    return general.Character.Culture.StringId;
            }
            catch { }
            return null;
        }
    }
}
