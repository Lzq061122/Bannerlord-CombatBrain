using System;
using System.Reflection;
using TaleWorlds.MountAndBlade;
using CombatBrain.Ai;

namespace CombatBrain
{
    public class SubModule : MBSubModuleBase
    {
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            TryAttachMcm(); // t73：MCM 可选集成（无 MCM 静默用默认豁免）
            CombatLog.Log("CombatBrain 子模块已加载");
        }

        // t73：MCM 可选集成。先探测 AppDomain 是否已加载 MCMv5 程序集；
        // 存在才触碰 CombatBrainMcmSettings.Instance（触发 GlobalSettings<T> 单例初始化/注册，被 MCM 扫描显示）；
        // 不存在则完全不触碰含 MCM 引用的类型（CLR 懒加载，未 JIT 不抛 FileNotFound）→ 无 MCM 静默默认豁免。
        // 任何失败（MCM 版本差异/注册异常）→ 静默默认豁免，不崩溃不报错。
        private static void TryAttachMcm()
        {
            try
            {
                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm != null && asm.GetName().Name == "MCMv5")
                    {
                        // 触碰静态 Instance，触发 AttributeGlobalSettings<T> 单例初始化/注册
                        _ = Mcm.CombatBrainMcmSettings.Instance;
                        return;
                    }
                }
            }
            catch
            {
                // 静默降级：默认豁免开启（与 P0-④ 现状一致）
            }
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
