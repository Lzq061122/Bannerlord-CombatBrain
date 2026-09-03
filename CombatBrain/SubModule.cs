using TaleWorlds.MountAndBlade;
using CombatBrain.Ai;

namespace CombatBrain
{
    public class SubModule : MBSubModuleBase
    {
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            CombatBrainSettings.TryAttachMcm(); // t66：MCM 反射式可选集成（无 MCM 静默用默认豁免）
            CombatLog.Log("CombatBrain 子模块已加载");
        }

        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);
            if (mission == null) return;
            try
            {
                mission.AddMissionBehavior(new CombatAiMissionLogic());
            }
            catch (System.Exception ex)
            {
                CombatLog.Log("挂载战斗AI逻辑失败: " + ex.Message);
            }
        }
    }
}