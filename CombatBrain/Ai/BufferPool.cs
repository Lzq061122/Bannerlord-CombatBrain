using System;
using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace CombatBrain.Ai
{
    // T4 性能工具箱：MBListPool 式 [ThreadStatic] 24 槽对象池（参照 OF 报告 3.3，MBListPool.cs）。
    // 把热路径反复分配的列表缓冲改为逐线程池化复用（清空复用），单这一项省大量分配。
    //
    // 线程安全：槽数组为 [ThreadStatic]，每线程独立实例，天然无竞态（与 OF 相同，无需锁）。
    // 使用约定：Get*(slot) 拿到的缓冲归调用方当前逻辑段独占使用，下次 Get 会 Clear 前复用，
    // 严禁跨逻辑段持引用（如放进会跨 tick 存活的字典）。
    public static class BufferPool
    {
        public const int SlotCount = 24;

        [ThreadStatic] private static MBList<Agent>[] _mbAgent;
        [ThreadStatic] private static List<Vec2>[] _vec2;
        [ThreadStatic] private static List<Agent>[] _agent;

        /// <summary>获取 1 个 MBList&lt;Agent&gt;（供 Mission.GetNearbyEnemyAgents/GetNearbyAllyAgents 用）。</summary>
        public static MBList<Agent> GetMBList(int slot)
        {
            if (_mbAgent == null) _mbAgent = new MBList<Agent>[SlotCount];
            MBList<Agent> b = _mbAgent[slot];
            if (b == null) { b = new MBList<Agent>(); _mbAgent[slot] = b; }
            else { b.Clear(); }
            return b;
        }

        /// <summary>获取 1 个 List&lt;Vec2&gt;（编队阵位/中心列表聚合用）。</summary>
        public static List<Vec2> GetVec2List(int slot)
        {
            if (_vec2 == null) _vec2 = new List<Vec2>[SlotCount];
            List<Vec2> b = _vec2[slot];
            if (b == null) { b = new List<Vec2>(); _vec2[slot] = b; }
            else { b.Clear(); }
            return b;
        }

        /// <summary>获取 1 个 List&lt;Agent&gt;（个体缓冲聚合用）。</summary>
        public static List<Agent> GetAgentList(int slot)
        {
            if (_agent == null) _agent = new List<Agent>[SlotCount];
            List<Agent> b = _agent[slot];
            if (b == null) { b = new List<Agent>(); _agent[slot] = b; }
            else { b.Clear(); }
            return b;
        }
    }
}