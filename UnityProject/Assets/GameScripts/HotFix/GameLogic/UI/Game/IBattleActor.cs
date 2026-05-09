using System.Collections.Generic;

namespace GameLogic
{
    /// <summary>
    /// 战斗参与者抽象接口，玩家和怪物均实现该接口供 CardEffectExecutor 统一调用。
    /// </summary>
    public interface IBattleActor
    {
        /// <summary>
        /// 当前血量。
        /// </summary>
        int Hp { get; }

        /// <summary>
        /// 最大血量。
        /// </summary>
        int MaxHp { get; }

        /// <summary>
        /// 当前护甲。
        /// </summary>
        int Armor { get; }

        /// <summary>
        /// 当前能量（同时也是出牌次数池）。
        /// </summary>
        int CurrentEnergy { get; }

        /// <summary>
        /// 最大能量（回合开始时恢复到该值）。
        /// </summary>
        int MaxEnergy { get; }

        /// <summary>
        /// Buff 列表（包含 DoT、增益、减益等所有持续效果）。
        /// </summary>
        IList<BuffRuntime> Buffs { get; }

        /// <summary>
        /// 是否已死亡（Hp 是否 ≤ 0）。
        /// </summary>
        bool IsDead { get; }

        /// <summary>
        /// 受到伤害；优先扣除护甲，再扣除血量。
        /// </summary>
        void TakeDamage(int amount);

        /// <summary>
        /// 增加护甲。
        /// </summary>
        void AddArmor(int amount);

        /// <summary>
        /// 获取能量。允许 CurrentEnergy 临时超过 MaxEnergy；下回合 Prepare 阶段会重置。
        /// </summary>
        void GainEnergy(int amount);

        /// <summary>
        /// 添加一条 Buff（DoT、增益、减益等）。
        /// </summary>
        void AddBuff(BuffRuntime buff);
    }
}
