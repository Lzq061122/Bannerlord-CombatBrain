namespace CombatBrain.Events
{
    public sealed class CombatEvent
    {
        public CombatEventKind Kind;
        public float Time;
        public int Id;       // 主主体：team / formation / agent 索引
        public int Id2;      // 次主体
        public int IntValue;
        public float FloatValue;
        public float X;
        public float Y;
        public float Z;
    }
}