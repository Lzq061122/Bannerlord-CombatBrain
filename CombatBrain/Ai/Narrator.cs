namespace CombatBrain.Ai
{
    using System;

    // N1 叙事双视角标签层（Narrator）—— 战场叙事反馈系统 §2 的统一出口（纯逻辑 + 关系输入版）。
    // 本文件零 TaleWorlds 依赖（可离线测）：所有判定输入为「已提取的公开阵营关系布尔 + 阵营名」
    // （NarrationInfo，调用方从 Team.IsPlayerTeam @ Team.cs:40 / IsPlayerAlly @ Team.cs:52 /
    //   IsEnemyOf @ Team.cs:629 提取——反作弊闸：只读公开阵营关系，零游戏写入，不新增信息读取）。
    // 职责：
    //   ① 对象集过滤 ShouldNarrate（谁有权说话）：玩家本队永不播（含豁免关闭时——旁白说给玩家听，
    //      不说玩家自己）；观战模式或无敌队=全放行（阵营名）；指挥模式=只放行与玩家交战的 CB 队
    //      （友军 CB 队 ∪ 玩家敌对队）；playerTeam==null 回退观战规则。
    //   ② 主语/宾语标签（§2.3 表）：观战=阵营名；指挥&敌队=敌军/我军；指挥&友军CB队=我军/敌军；
    //      敌队缺失宁缺毋假。
    //   ③ 统一出口 Notify：对象集过滤 → {S}/{O} 代填 → 经注入的 send 委托转发
    //      （胶水层传 InGameFeedback.NotifyTactical——3s 去重 + 4 条/s 限频 + DebugMode 均不变；
    //      委托注入保持本文件零 TaleWorlds/零 InGameFeedback 引用，可离线测过滤与代填）。
    // 模式=CombatBrainSettings.NarrationSpectatorMode（默认 false=指挥模式）。
    public static class Narrator
    {
        /// <summary>关系输入（调用方从公开阵营关系提取；阵营名由 FeedbackText.Faction 提供）。</summary>
        public struct NarrationInfo
        {
            public bool IsPlayerTeam;      // 本队是玩家队
            public bool IsPlayerAlly;      // 本队是玩家友军
            public bool IsEnemyOfPlayer;   // 本队是玩家敌对队
            public bool HasPlayerTeam;     // 战场存在玩家队
            public string FactionName;     // 本队阵营名（观战模式用）
        }

        /// <summary>对象集过滤：谁有权说话（纯逻辑，可离线测）。</summary>
        public static bool ShouldNarrate(NarrationInfo info, bool spectatorMode)
        {
            if (info.IsPlayerTeam) return false;                   // 玩家本队永不播
            if (spectatorMode || !info.HasPlayerTeam) return true; // 观战 / 无玩家队回退：全放行
            return info.IsPlayerAlly || info.IsEnemyOfPlayer;      // 指挥：只播与玩家交战的 CB 队
        }

        /// <summary>主语标签（§2.3 表）：观战=阵营名；指挥&敌队=敌军；指挥&友军CB队=我军；第三方兜底阵营名。</summary>
        public static string Subject(NarrationInfo info, bool spectatorMode)
        {
            if (spectatorMode || !info.HasPlayerTeam) return info.FactionName; // 观战/回退：阵营名
            if (info.IsPlayerTeam) return "我军";                              // 玩家本队兜底（正常已过滤）
            if (info.IsEnemyOfPlayer) return "敌军";                           // 指挥&敌队
            if (info.IsPlayerAlly) return "我军";                              // 指挥&友军 CB 队
            return info.FactionName;                                           // 第三方兜底（宁缺毋假）
        }

        /// <summary>宾语标签（§2.3 表）：观战=O 阵营名；指挥&O 是玩家=我军；指挥&O 非玩家=敌军。</summary>
        public static string Opponent(bool opponentIsPlayerTeam, bool hasPlayerTeam,
            bool spectatorMode, string opponentFactionName)
        {
            if (spectatorMode || !hasPlayerTeam) return opponentFactionName; // 观战/回退：O 阵营名
            return opponentIsPlayerTeam ? "我军" : "敌军";                    // 指挥：按 O 是否玩家阵营
        }

        /// <summary>代填 {S}/{O}：按模式把模板占位符替换为主语/宾语标签（纯逻辑，可离线测）。</summary>
        public static string ComposeText(NarrationInfo info, string textTemplate, bool spectatorMode)
        {
            return textTemplate
                .Replace("{S}", Subject(info, spectatorMode))
                .Replace("{O}", Opponent(info.IsPlayerTeam, info.HasPlayerTeam, spectatorMode, info.FactionName));
        }

        /// <summary>
        /// 统一出口：对象集过滤 → {S}/{O} 代填 → 经 send 委托转发（胶水层传 InGameFeedback.NotifyTactical；
        /// 过滤未通过则不发）。模板写 {S}（主语）/ {O}（宾语）占位；不含占位符的模板原样转发。
        /// 不改 InGameFeedback 限频/去重/DebugMode（NotifyTactical 内部 3s 去重 + 4 条/s 上限）。
        /// </summary>
        public static void Notify(NarrationInfo info, string category, string textTemplate, uint color,
            Action<string, string, uint> send)
        {
            bool spectatorMode = CombatBrainSettings.NarrationSpectatorMode;
            if (!ShouldNarrate(info, spectatorMode)) return; // 对象集过滤（谁有权说话）
            if (send == null) return;
            send(category, ComposeText(info, textTemplate, spectatorMode), color);
        }
    }
}
