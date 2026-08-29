using System;

namespace CombatBrain.Movement
{
    // F6 走位/距离控制/绕侧 —— 只在 L2+ 跑，跳帧决策。
    // 纯逻辑：不接触游戏对象，进游戏后由胶水层把 Agent 位置/武器喂进来即可。
    public enum MoveAction
    {
        Hold,
        Advance,
        StepBack,
        StrafeLeft,
        StrafeRight,
        RetreatToAllies
    }

    public struct MovementContext
    {
        public float OwnX, OwnY;
        public float TargetX, TargetY;
        public float OwnReach;      // 我方武器有效距离
        public float TargetReach;   // 对方武器有效距离
        public float FlankAware;    // 0..1（来自 FightProfile）
        public float OffHandSide;   // 对方非主手侧符号：>=0 朝一侧绕，<0 反向
        public bool IsSurrounded;   // 被围
        public float AllyX, AllyY;  // 最近友军（被围后撤方向）
    }

    public struct MovementDecision
    {
        public MoveAction Action;
        public float DesiredGap;
        public float MoveX;   // 归一化移动方向（0,0 = 原地）
        public float MoveY;
    }

    public static class MovementController
    {
        public const float Slack = 0.6f;
        public const float FlankThreshold = 0.4f;

        // 理想交战距离：站在对方刀尖最外沿，自己能够得着。
        public static float IdealGap(float ownReach, float targetReach)
        {
            return ownReach * 0.8f + targetReach * 0.5f;
        }

        public static MovementDecision Decide(MovementContext c)
        {
            var d = new MovementDecision();

            float dx = c.TargetX - c.OwnX;
            float dy = c.TargetY - c.OwnY;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);
            float gap = IdealGap(c.OwnReach, c.TargetReach);
            d.DesiredGap = gap;

            float nx = dist > 1e-4f ? dx / dist : 0f;
            float ny = dist > 1e-4f ? dy / dist : 0f;

            // 被围：主动向友军靠，借队友挡枪线。
            if (c.IsSurrounded)
            {
                d.Action = MoveAction.RetreatToAllies;
                float ax = c.AllyX - c.OwnX;
                float ay = c.AllyY - c.OwnY;
                float al = (float)Math.Sqrt(ax * ax + ay * ay);
                d.MoveX = al > 1e-4f ? ax / al : -nx;
                d.MoveY = al > 1e-4f ? ay / al : -ny;
                return d;
            }

            if (dist > gap + Slack)
            {
                d.Action = MoveAction.Advance;
                d.MoveX = nx;
                d.MoveY = ny;
            }
            else if (dist < gap - Slack)
            {
                d.Action = MoveAction.StepBack;
                d.MoveX = -nx;
                d.MoveY = -ny;
            }
            else
            {
                // 距离合适：高绕侧意识者朝对方非主手侧滑步，否则稳住。
                if (c.FlankAware >= FlankThreshold)
                {
                    if (c.OffHandSide >= 0f)
                    {
                        d.Action = MoveAction.StrafeLeft;
                        d.MoveX = -ny;
                        d.MoveY = nx;
                    }
                    else
                    {
                        d.Action = MoveAction.StrafeRight;
                        d.MoveX = ny;
                        d.MoveY = -nx;
                    }
                }
                else
                {
                    d.Action = MoveAction.Hold;
                    d.MoveX = 0f;
                    d.MoveY = 0f;
                }
            }

            return d;
        }
    }
}