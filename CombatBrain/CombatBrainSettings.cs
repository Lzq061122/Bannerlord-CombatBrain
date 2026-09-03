namespace CombatBrain
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Reflection.Emit;

    // CombatBrain 静态配置（t66）：MCM 可选集成 + 游戏内开关默认值。
    // 设计原则：
    //   * 编译期零 MCM 依赖（纯反射字符串探测，避免硬引用 MCMv5.dll）；
    //   * MCM 探测/注册任何一步失败 → 静默使用默认值（豁免开启，与 P0-④ 现状一致），不崩溃不报错；
    //   * 反作弊闸不变：豁免只是「mod 是否接管玩家队」的指挥权开关，不新增任何禁读信息。
    public static class CombatBrainSettings
    {
        // 玩家队豁免开关：true=现状（P0-④，5 个编队层 Glue + TickIntent 不接管玩家队）；
        // false=mod 正常接管/扫描玩家队（编队层下令、意图决策、敌队聚合均纳入玩家队）。
        // 默认 true=保持现状；MCM 装载后可在「通用」组里热切（RequireRestart=false）。
        public static bool PlayerTeamExemptionEnabled = true;

        // MCM 反射式可选集成（SubModule.OnSubModuleLoad 调一次）：
        //   探测 MCMv5 程序集与 AttributeSettings<T> 基类 → 反射 Emit 一个
        //   AttributeSettings<CBMcmSettings> 动态子类（属性 PlayerTeamExemptionEnabled，
        //   [SettingPropertyBool("玩家队豁免", RequireRestart=false)] + [SettingPropertyGroup("通用")]），
        //   get/set IL 直读直写本类静态字段（MCM UI 改动即时生效，无拷贝失步）；
        //   再多路尝试显式注册；动态程序集若不在 MCM 扫描范围 → 静默降级默认豁免。
        // 反射探测证据（MCMv5.dll 实测，Bannerlord.MBOptionScreen/bin/Win64_Shipping_Client）：
        //   * MCM.Abstractions.Settings.Base.AttributeSettings`1（离线加载依赖缺失，游戏内正常）；
        //   * MCM.Abstractions.Attributes.v2.SettingPropertyBoolAttribute(string)；
        //   * MCM.Abstractions.Attributes.SettingPropertyGroupAttribute(string)。
        // 失败语义：任一步异常/类型缺失 → 静默返回（默认豁免开启），契约允许（无 MCM 不崩溃不报错）。
        public static void TryAttachMcm()
        {
            try
            {
                Assembly mcm = null;
                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm != null && asm.GetName().Name == "MCMv5") { mcm = asm; break; }
                }
                if (mcm == null) return; // 无 MCM：静默默认

                Type attrBase = mcm.GetType("MCM.Abstractions.Settings.Base.AttributeSettings`1", false);
                Type boolAttr = mcm.GetType("MCM.Abstractions.Attributes.v2.SettingPropertyBoolAttribute", false);
                if (attrBase == null || boolAttr == null) return;

                AssemblyBuilder ab = AppDomain.CurrentDomain.DefineDynamicAssembly(
                    new AssemblyName("CombatBrainMcmSettings"), AssemblyBuilderAccess.Run);
                ModuleBuilder mb = ab.DefineDynamicModule("CombatBrainMcmSettings");
                TypeBuilder tb = mb.DefineType(
                    "CombatBrain.CBMcmSettings",
                    TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class);
                tb.SetParent(attrBase.MakeGenericType(tb)); // CRTP：AttributeSettings<CBMcmSettings>（先建 builder 再设基类型，规避 CS0165 自引用）

                // 属性 PlayerTeamExemptionEnabled：get/set IL 直读直写本类静态字段（无拷贝失步）
                FieldInfo realField = typeof(CombatBrainSettings).GetField(nameof(PlayerTeamExemptionEnabled));
                PropertyBuilder pb = tb.DefineProperty("PlayerTeamExemptionEnabled", PropertyAttributes.HasDefault, typeof(bool), null);
                MethodBuilder getter = tb.DefineMethod("get_PlayerTeamExemptionEnabled",
                    MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, typeof(bool), Type.EmptyTypes);
                ILGenerator gl = getter.GetILGenerator();
                gl.Emit(OpCodes.Ldsfld, realField);
                gl.Emit(OpCodes.Ret);
                MethodBuilder setter = tb.DefineMethod("set_PlayerTeamExemptionEnabled",
                    MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, null, new[] { typeof(bool) });
                ILGenerator sl = setter.GetILGenerator();
                sl.Emit(OpCodes.Ldarg_0);
                sl.Emit(OpCodes.Stsfld, realField);
                sl.Emit(OpCodes.Ret);
                pb.SetGetMethod(getter);
                pb.SetSetMethod(setter);

                // [SettingPropertyBool("玩家队豁免", RequireRestart = false)]（named 参数仅在该属性存在时附加）
                ConstructorInfo boolCtor = boolAttr.GetConstructor(new[] { typeof(string) });
                PropertyInfo restartProp = FindProperty(boolAttr, "RequireRestart");
                if (boolCtor != null)
                {
                    if (restartProp != null)
                        pb.SetCustomAttribute(new CustomAttributeBuilder(boolCtor, new object[] { "玩家队豁免" },
                            new PropertyInfo[] { restartProp }, new object[] { false }));
                    else
                        pb.SetCustomAttribute(new CustomAttributeBuilder(boolCtor, new object[] { "玩家队豁免" }));
                }

                // [SettingPropertyGroup("通用")]（类级）
                Type groupAttr = mcm.GetType("MCM.Abstractions.Attributes.SettingPropertyGroupAttribute", false);
                if (groupAttr != null)
                {
                    ConstructorInfo groupCtor = groupAttr.GetConstructor(new[] { typeof(string) });
                    if (groupCtor != null)
                        tb.SetCustomAttribute(new CustomAttributeBuilder(groupCtor, new object[] { "通用" }));
                }

                object settings = Activator.CreateInstance(tb.CreateType());
                TryExplicitRegister(mcm, settings);
            }
            catch
            {
                // 任何失败（MCM 版本差异/Emit 限制/注册面不存在）→ 静默默认（豁免开启）
            }
        }

        // 显式注册多路探测（尽力而为，全失败静默——动态类型若被 MCM 约定发现则此处冗余无害）
        private static void TryExplicitRegister(Assembly mcm, object settings)
        {
            // 路 1：MCM.Abstractions.Base.Global.GlobalSettings.Instance（静态门面，若存在）
            Type gs = mcm.GetType("MCM.Abstractions.Base.Global.GlobalSettings", false);
            if (gs != null)
            {
                PropertyInfo inst = gs.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                object container = inst != null ? inst.GetValue(null, null) : null;
                MethodInfo reg = container != null
                    ? container.GetType().GetMethod("RegisterSettings", new[] { settings.GetType().BaseType })
                    : null;
                if (reg == null && container != null)
                    reg = container.GetType().GetMethod("RegisterSettings", new[] { typeof(object) });
                if (reg != null) { reg.Invoke(container, new[] { settings }); return; }
            }

            // 路 2：任意 MCM 门面类的静态 RegisterSettings(<settings 兼容参数>)
            foreach (Type t in mcm.GetTypes())
            {
                foreach (MethodInfo m in t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (m.Name != "RegisterSettings") continue;
                    ParameterInfo[] ps = m.GetParameters();
                    if (ps.Length == 1 && ps[0].ParameterType.IsInstanceOfType(settings))
                    {
                        m.Invoke(null, new[] { settings });
                        return;
                    }
                }
            }
        }

        private static PropertyInfo FindProperty(Type type, string name)
        {
            Type cur = type;
            while (cur != null)
            {
                PropertyInfo p = cur.GetProperty(name);
                if (p != null) return p;
                cur = cur.BaseType;
            }
            return null;
        }
    }
}
