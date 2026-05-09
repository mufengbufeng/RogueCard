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

        /// <inheritdoc />
        public int Hp => _model.PlayerHp;

        /// <inheritdoc />
        public int MaxHp => _model.PlayerMaxHp;

        /// <inheritdoc />
        public int Armor => _model.PlayerArmor;

        /// <inheritdoc />
        public int CurrentEnergy => _model.CurrentEnergy;

        /// <inheritdoc />
        public int MaxEnergy => _model.MaxEnergy;

        /// <inheritdoc />
        public IList<BuffRuntime> Buffs => _buffs;

        /// <inheritdoc />
        public bool IsDead => _model.PlayerHp <= 0;

        /// <inheritdoc />
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

        /// <inheritdoc />
        public void AddArmor(int amount)
        {
            if (amount == 0) return;
            _model.ModifyPlayerArmor(amount);
        }

        /// <inheritdoc />
        public void GainEnergy(int amount)
        {
            if (amount == 0) return;
            _model.GainEnergy(amount);
        }

        /// <inheritdoc />
        public void AddBuff(BuffRuntime buff)
        {
            if (buff == null) return;
            _model.AddPlayerBuff(buff);
        }
    }
}
