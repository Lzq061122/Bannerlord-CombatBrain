namespace CombatBrain.Survival
{
    // F7 求生本能（个体层）—— **已随 CB-PROMPT-0038 §五.5 退役**。
    //
    // 退役内容：原 SurvivalAction / SurvivalContext / SurvivalInstinct.Decide（濒死个体自行退向友军）
    //   —— 属「逐兵战术决策 + 逐兵移动写入」，其唯一消费者 SurvivalGlue.ApplyRetreat 已删除。
    // 保留内容（已迁出本文件）：编队级重整信号 CasualtyReformSignal 现落
    //   CombatBrain/Ai/CasualtyReformSignal.cs（命名空间仍为 CombatBrain.Survival，消费者是
    //   CombatBrain/Ai/SurvivalGlue.cs 的编队级事件累计）。
}
