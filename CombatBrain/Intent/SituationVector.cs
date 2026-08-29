namespace CombatBrain.Intent
{
    // 态势向量 8 维（全部归一化到 [0,1]）。
    // 索引顺序与 Labels 对齐：0 Power, 1 Line, 2 Cav, 3 Height, 4 Moral, 5 Arc, 6 PenB, 7 Dist
    public struct SituationVector
    {
        public const int DimensionCount = 8;

        public float Power;
        public float Line;
        public float Cav;
        public float Height;
        public float Moral;
        public float Arc;
        public float PenB;
        public float Dist;

        public float this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0: return Power;
                    case 1: return Line;
                    case 2: return Cav;
                    case 3: return Height;
                    case 4: return Moral;
                    case 5: return Arc;
                    case 6: return PenB;
                    case 7: return Dist;
                }
                return 0f;
            }
            set
            {
                switch (index)
                {
                    case 0: Power = value; break;
                    case 1: Line = value; break;
                    case 2: Cav = value; break;
                    case 3: Height = value; break;
                    case 4: Moral = value; break;
                    case 5: Arc = value; break;
                    case 6: PenB = value; break;
                    case 7: Dist = value; break;
                }
            }
        }

        public static readonly string[] Labels =
        {
            "Power", "Line", "Cav", "Height", "Moral", "Arc", "PenB", "Dist"
        };

        public override string ToString()
        {
            return string.Format(
                "Power={0:F2} Line={1:F2} Cav={2:F2} Height={3:F2} Moral={4:F2} Arc={5:F2} PenB={6:F2} Dist={7:F2}",
                Power, Line, Cav, Height, Moral, Arc, PenB, Dist);
        }
    }
}