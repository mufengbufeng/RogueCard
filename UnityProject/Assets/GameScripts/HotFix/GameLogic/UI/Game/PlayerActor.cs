using System;
using System.Collections.Generic;

namespace GameLogic
{
    /// <summary>
    /// 玩家战斗参与者实现，包装 GameModel 玩家字段为 IBattleActor。
    /// 所有写入都通过 GameModel.Modify* 系列方法走 PropertyChanged 通知，避免绕过 ViewModel。
    /// </summary>
    public class PlayerActor : IBattleActor
    {
        private readonly GameModel _model;
        private readonly List<BuffRuntime> _buffs;

        /// <summary>
        /// 创建玩家战斗参与者。
        /// </summary>
        public PlayerActor(GameModel model)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _buffs = model.PlayerBuffs;
        }

        /// <summary>
        /// 玩家当前血量。
        /// </summary>
        public int Hp => _model.PlayerHp;

        /// <summary>
        /// 玩家最大血量。
        /// </summary>
        public int MaxHp => _model.PlayerMaxHp;

        /// <summary>
        /// 玩家当前护甲。
        /// </summary>
        public int Armor => _model.PlayerArmor;

        /// <summary>
        /// 玩家当前能量。
        /// </summary>
        public int CurrentEnergy => _model.CurrentEnergy;

        /// <summary>
        /// 玩家最大能量。
        /// </summary>
        public int MaxEnergy => _model.MaxEnergy;

        /// <summary>
        /// 玩家身上的 Buff 列表。
        /// </summary>
        public IList<BuffRuntime> Buffs => _buffs;

        /// <summary>
        /// 玩家是否已死亡。
        /// </summary>
        public bool IsDead => _model.PlayerHp <= 0;

        /// <summary>
        /// 让玩家受到伤害，优先扣除护甲，再扣除血量。
        /// </summary>
        public void TakeDamage(int amount)
        {
            if (amount <= 0) return;

            int remaining = amount;
            int armor = _model.PlayerArmor;
            if (armor > 0)
            {
                int absorbed = Math.Min(armor, remaining);
                _model.ModifyPlayerArmor(-absorbed);
                remaining -= absorbed;
            }

            if (remaining > 0)
            {
                _model.ModifyPlayerHp(-remaining);
            }
        }

        /// <summary>
        /// 修改玩家护甲。
        /// </summary>
        public void AddArmor(int amount)
        {
            if (amount == 0) return;
            _model.ModifyPlayerArmor(amount);
        }

        /// <summary>
        /// 获取本回合能量，允许临时超过最大能量。
        /// </summary>
        public void GainEnergy(int amount)
        {
            if (amount == 0) return;
            _model.GainEnergy(amount);
        }

        /// <summary>
        /// 为玩家添加一条 Buff。
        /// </summary>
        public void AddBuff(BuffRuntime buff)
        {
            if (buff == null) return;
            _model.AddPlayerBuff(buff);
        }
    }
}
