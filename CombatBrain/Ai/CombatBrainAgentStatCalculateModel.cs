using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace CombatBrain.Ai
{
    // X2 手活属性模型（AgentStatCalculateModel 子类化，零 Harmony）。
    // 蓝本：ImprovedCombatAIAgentStatCalculateModel.cs（_mod_research，238 行）+ research-rbm-ai.md §4.1。
    // 注册：SubModule.OnGameStart → gameStarterObject.AddModel(this)（MissionGameModels 链式取最后一个，覆盖原版模型；
    //   引擎自动把原版模型设为我们的 BaseModel，见 MBGameModel<T>.BaseModel @ TaleWorlds.Core L3-5，转发即保原版行为）。
    // 语义：只改能力参数（格挡/招架/射击精度/走位/攻击概率），不碰信息获取——反作弊闸不违规（派单 §2.2 明示）。
    // 范围：只对 IsHuman 且 IsAIControlled 的 agent 生效（玩家自己手控的角色不覆盖，避免玩家手感被改）。
    // 强度：CombatBrainSettings.HandSkillStrength ∈ [0,1]（0=原版现值，1=满效果，默认 1；后续可接 MCM）。
    // 与 t19 AttackSuppression 协调：攻击概率基准由本模型按强度给出；AttackSuppression.Suppress 会把该 agent
    //   加入本类静态抑制集合并置 0，模型 UpdateAgentStats 遇抑制索引跳过覆盖（保持 0），Restore 移除索引后恢复基准。
    //   （AttackSuppression.cs 已联动调用 MarkAttackSuppressed/UnmarkAttackSuppressed，见 Suppress/Restore/Remove。）
    public class CombatBrainAgentStatCalculateModel : AgentStatCalculateModel
    {
        // t19 协调：当前被 AttackSuppression 抑制的 agent（主线程访问，与 CombatBrain 全单线程模型一致，无需锁）
        private static readonly HashSet<int> _suppressedAttack = new HashSet<int>();

        public static void MarkAttackSuppressed(int agentIndex)
        {
            _suppressedAttack.Add(agentIndex);
        }

        public static void UnmarkAttackSuppressed(int agentIndex)
        {
            _suppressedAttack.Remove(agentIndex);
        }

        // atc4-state-lifecycle P1-4：跨 Mission 静态状态清理——AttackSuppression.ClearAll() 必须同时清空
        //   属性模型静态抑制集合（否则下一场战斗抑制索引滞留，UpdateAgentStats 误跳过攻击概率覆盖）
        public static void ClearAllSuppressed()
        {
            _suppressedAttack.Clear();
        }

        public override void InitializeAgentStats(Agent agent, Equipment spawnEquipment, AgentDrivenProperties agentDrivenProperties, AgentBuildData agentBuildData)
        {
            if (BaseModel != null) BaseModel.InitializeAgentStats(agent, spawnEquipment, agentDrivenProperties, agentBuildData);
        }

        public override void UpdateAgentStats(Agent agent, AgentDrivenProperties agentDrivenProperties)
        {
            if (BaseModel != null) BaseModel.UpdateAgentStats(agent, agentDrivenProperties);

            // X2 手活覆盖：仅 AI 人类单位（玩家手控角色不覆盖）；强度 0 = 原版现值
            if (!agent.IsHuman || !agent.IsAIControlled) return;
            float s = CombatBrainSettings.HandSkillStrength;
            if (s <= 0f) return;

            float skill = 0f;
            if (agent.Character != null)
            {
                // 近战技能取最高主战技能，弓技能单独取（射击精度）
                skill = GetEffectiveSkill(agent, DefaultSkills.OneHanded);
                int two = GetEffectiveSkill(agent, DefaultSkills.TwoHanded);
                int pole = GetEffectiveSkill(agent, DefaultSkills.Polearm);
                if (two > skill) skill = two;
                if (pole > skill) skill = pole;
            }
            float skill01 = HandSkillProfile.Skill01(skill);
            float diff = GetDifficultyModifier();
            float aiLevel = HandSkillProfile.AiLevel(skill01, diff, s);

            // 格挡/招架（RBM clamp(base×2,0.3,1) 口径，上限锁死不超原版最高难度）
            agentDrivenProperties.AIBlockOnDecideAbility = HandSkillProfile.BlockAbility(agentDrivenProperties.AIBlockOnDecideAbility, s);
            agentDrivenProperties.AIParryOnDecideAbility = HandSkillProfile.ParryAbility(agentDrivenProperties.AIParryOnDecideAbility, s);

            // 射击精度：弓误差按技能（0.015−0.007×skill01，RBM §4.1）
            if (agent.Character != null)
            {
                int bow = GetEffectiveSkill(agent, DefaultSkills.Bow);
                agentDrivenProperties.AiShooterError = HandSkillProfile.ShooterError(HandSkillProfile.Skill01(bow), s);
            }

            // 走位/闪避：防守方向随机化降低（更聪明地防方向误判），下限 0
            agentDrivenProperties.AiRandomizedDefendDirectionChance =
                HandSkillProfile.DefendDirectionChance(agentDrivenProperties.AiRandomizedDefendDirectionChance, s);

            // 攻击概率基准：与 t19 协调——被抑制的 agent 保持 0（不覆盖），其余按强度给基准
            if (!_suppressedAttack.Contains(agent.Index))
            {
                agentDrivenProperties.AIAttackOnDecideChance =
                    HandSkillProfile.AttackChance(agentDrivenProperties.AIAttackOnDecideChance, s);
            }
        }

        public override float GetDifficultyModifier()
        {
            return BaseModel != null ? BaseModel.GetDifficultyModifier() : 0.5f;
        }

        public override bool CanAgentRideMount(Agent agent, Agent targetMount)
        {
            return BaseModel != null && BaseModel.CanAgentRideMount(agent, targetMount);
        }

        public override float GetWeaponDamageMultiplier(Agent agent, WeaponComponentData weapon)
        {
            return BaseModel != null ? BaseModel.GetWeaponDamageMultiplier(agent, weapon) : 1f;
        }

        public override float GetEquipmentStealthBonus(Agent agent)
        {
            return BaseModel != null ? BaseModel.GetEquipmentStealthBonus(agent) : 0f;
        }

        public override float GetSneakAttackMultiplier(Agent agent, WeaponComponentData weapon)
        {
            return BaseModel != null ? BaseModel.GetSneakAttackMultiplier(agent, weapon) : 1f;
        }

        public override float GetKnockBackResistance(Agent agent)
        {
            return BaseModel != null ? BaseModel.GetKnockBackResistance(agent) : 1f;
        }

        public override float GetKnockDownResistance(Agent agent, StrikeType strikeType = StrikeType.Invalid)
        {
            return BaseModel != null ? BaseModel.GetKnockDownResistance(agent, strikeType) : 1f;
        }

        public override float GetDismountResistance(Agent agent)
        {
            return BaseModel != null ? BaseModel.GetDismountResistance(agent) : 1f;
        }

        public override float GetBreatheHoldMaxDuration(Agent agent, float baseBreatheHoldMaxDuration)
        {
            return BaseModel != null ? BaseModel.GetBreatheHoldMaxDuration(agent, baseBreatheHoldMaxDuration) : baseBreatheHoldMaxDuration;
        }
    }
}
