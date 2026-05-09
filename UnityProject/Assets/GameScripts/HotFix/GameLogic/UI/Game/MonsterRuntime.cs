using System;
using System.Collections.Generic;
using GameConfig.card;
using GameConfig.monster;

namespace GameLogic
{
    /// <summary>
    /// 怪物运行时实例，维护单只怪物的战斗状态。
    /// 实现 IBattleActor 与玩家在战斗系统中保持对称：能量池、Buffs、手牌/抽牌堆/弃牌堆全部独立。
    /// </summary>
    public class MonsterRuntime : IBattleActor
    {
        /// <summary>
        /// 怪物配置引用。
        /// </summary>
        public Monster Config { get; set; }

        /// <summary>
        /// 当前血量。
        /// </summary>
        public int Hp { get; set; }

        /// <summary>
        /// 最大血量。
        /// </summary>
        public int MaxHp { get; set; }

        /// <summary>
        /// 当前护甲。
        /// </summary>
        public int Armor { get; set; }

        /// <summary>
        /// 当前能量。
        /// </summary>
        public int CurrentEnergy { get; set; }

        /// <summary>
        /// 最大能量。
        /// </summary>
        public int MaxEnergy { get; set; }

        /// <summary>
        /// 手牌上限。
        /// </summary>
        public int HandLimit { get; set; }

        /// <summary>
        /// Buff 列表（DoT、增益、减益等持续效果）。
        /// </summary>
        public IList<BuffRuntime> Buffs { get; } = new List<BuffRuntime>();

        /// <summary>
        /// 是否已死亡。
        /// </summary>
        public bool IsDead => Hp <= 0;

        /// <summary>
        /// 当前手牌。MonsterTurn 结束时被 MonsterCardSystem.DiscardAllHand 清空。
        /// </summary>
        public List<CardRuntime> Hand { get; } = new();

        /// <summary>
        /// 抽牌堆。由 MonsterCardSystem.InitDeck 从 TbMonsterDeck 中 Order=0 的兜底行展开生成。
        /// </summary>
        public List<CardRuntime> DrawPile { get; } = new();

        /// <summary>
        /// 弃牌堆。MonsterTurn 结束后所有手牌进此堆；抽牌堆耗尽时洗回抽牌堆。
        /// </summary>
        public List<CardRuntime> DiscardPile { get; } = new();

        /// <summary>
        /// 本回合即将打出的卡牌列表。Prepare 阶段由 MonsterAiBrain.SelectIntent 写入；MonsterTurn 阶段消费后清空。
        /// </summary>
        public List<Card> PendingCards { get; } = new();

        /// <summary>
        /// 剧本卡映射：键为 Order（>0），值为该回合必出的卡。由 MonsterCardSystem.InitDeck 填充。
        /// </summary>
        public Dictionary<int, Card> ScriptedCards { get; } = new();

        /// <summary>
        /// 已存活回合数；进入战斗时为 0，每完成一次 MonsterTurn 后 +1。
        /// </summary>
        public int TurnsAlive { get; set; }

        /// <summary>
        /// 受到伤害；优先扣除护甲，再扣除血量。Hp 不会低于 0。
        /// </summary>
        public void TakeDamage(int amount)
        {
            if (amount <= 0) return;

            int remaining = amount;
            if (Armor > 0)
            {
                int absorbed = Math.Min(Armor, remaining);
                Armor -= absorbed;
                remaining -= absorbed;
            }

            if (remaining > 0)
            {
                Hp = Math.Max(0, Hp - remaining);
            }
        }

        /// <summary>
        /// 增加护甲。
        /// </summary>
        public void AddArmor(int amount)
        {
            if (amount == 0) return;
            Armor = Math.Max(0, Armor + amount);
        }

        /// <summary>
        /// 获取能量。允许 CurrentEnergy 临时超过 MaxEnergy；下一次 BeginMonsterPrepare 会重置。
        /// </summary>
        public void GainEnergy(int amount)
        {
            if (amount == 0) return;
            CurrentEnergy = Math.Max(0, CurrentEnergy + amount);
        }

        /// <summary>
        /// 添加 Buff。
        /// </summary>
        public void AddBuff(BuffRuntime buff)
        {
            if (buff == null) return;
            Buffs.Add(buff);
        }
    }
}
