using System;
using System.Reflection;
using TaleWorlds.Core;
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
            // cps3-runtime-prep：CPS-0 观测 Enable 启动接线（t3 遗留调用点；gated by 集中开关，默认关=近零开销；
            //   patch 失败 fail-open 并写「观测不可用」日志——Enable 内部 §4.2.8）
            if (CombatBrain.Settings.Cps3RuntimeSettings.ObserverEnabled)
            {
                Coordination.SetOrderObserver.Enable();
            }
            CombatLog.Log("CombatBrain 子模块已加载");
        }

        // X2 手活属性模型注册已退役（CB-PROMPT-0038 §五.1）：
        //   原 OnGameStart → gameStarterObject.AddModel(new CombatBrainAgentStatCalculateModel()) 已删除。
        //   退役理由：CombatBrain 不修改单兵格挡/招架/攻击决定概率/射击误差/随机防守方向等玩法能力数值。
        //   链完整性：不再向 MissionGameModels 追加任何 AgentStatCalculateModel 子类 → 引擎取到的就是原版模型，
        //   其他 Mod 的模型链完全不被触碰（无空壳 override 改变语义）。

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
