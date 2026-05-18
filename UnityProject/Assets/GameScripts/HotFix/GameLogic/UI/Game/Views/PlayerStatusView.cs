using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic
{
    /// <summary>
    /// 玩家状态 UGUI 子视图，负责阶段文本、血量、护甲、能量和玩家 Buff。
    /// </summary>
    public sealed class PlayerStatusView : IDisposable
    {
        private readonly IPlayerStatusContext _context;
        private PlayerStatusBindings _bindings;

        private Action<BattlePhase> _onPhaseChanged;
        private Action<int> _onPlayerHpChanged;
        private Action<int> _onPlayerMaxHpChanged;
        private Action<int> _onPlayerArmorChanged;
        private Action<int> _onEnergyChanged;
        private Action<int> _onMaxEnergyChanged;
        private Action<bool> _onLevelCompleteChanged;
        private Action<bool> _onPlayerDeadChanged;
        private Action<IReadOnlyList<BuffRuntime>> _onPlayerBuffsChanged;

        private bool _disposed;

        /// <summary>
        /// 创建玩家状态视图并立即完成首帧刷新。
        /// </summary>
        public PlayerStatusView(PlayerStatusBindings bindings, IPlayerStatusContext context)
        {
            _bindings = bindings;
            _context = context ?? throw new ArgumentNullException(nameof(context));

            _onPhaseChanged = _ => RefreshInfo();
            _onPlayerHpChanged = _ => RefreshInfo();
            _onPlayerMaxHpChanged = _ => RefreshInfo();
            _onPlayerArmorChanged = _ => RefreshInfo();
            _onEnergyChanged = _ => RefreshInfo();
            _onMaxEnergyChanged = _ => RefreshInfo();
            _onLevelCompleteChanged = _ => RefreshInfo();
            _onPlayerDeadChanged = _ => RefreshInfo();
            _onPlayerBuffsChanged = _ => RefreshBuffs();

            _context.Phase.Changed += _onPhaseChanged;
            _context.PlayerHp.Changed += _onPlayerHpChanged;
            _context.PlayerMaxHp.Changed += _onPlayerMaxHpChanged;
            _context.PlayerArmor.Changed += _onPlayerArmorChanged;
            _context.Energy.Changed += _onEnergyChanged;
            _context.MaxEnergy.Changed += _onMaxEnergyChanged;
            _context.IsLevelComplete.Changed += _onLevelCompleteChanged;
            _context.IsPlayerDead.Changed += _onPlayerDeadChanged;
            _context.PlayerBuffs.Changed += _onPlayerBuffsChanged;

            RefreshInfo();
            RefreshBuffs();
        }

        /// <summary>
        /// 刷新阶段文本与数值状态。
        /// </summary>
        public void RefreshInfo()
        {
            if (_disposed)
            {
                return;
            }

            string phaseLabel = GetPhaseLabel(_context.Phase.Value);
            if (_context.IsLevelComplete.Value)
            {
                phaseLabel = "关卡完成！";
            }
            else if (_context.IsPlayerDead.Value)
            {
                phaseLabel = "玩家死亡";
            }

            UguiViewUtil.SetText(_bindings.InfoText, phaseLabel);
            UguiViewUtil.SetText(_bindings.HpText, $"{_context.PlayerHp.Value}/{_context.PlayerMaxHp.Value}");
            UguiViewUtil.SetText(_bindings.ArmorText, _context.PlayerArmor.Value > 0 ? _context.PlayerArmor.Value.ToString() : "0");
            UguiViewUtil.SetText(_bindings.EnergyText, $"{_context.Energy.Value}/{_context.MaxEnergy.Value}");

            float? hpRatio = UguiViewUtil.SafeRatio(_context.PlayerHp.Value, _context.PlayerMaxHp.Value);
            if (hpRatio.HasValue)
            {
                UguiViewUtil.SetFillAmount(_bindings.HpFill, hpRatio.Value);
            }

            float? energyRatio = UguiViewUtil.SafeRatio(_context.Energy.Value, _context.MaxEnergy.Value);
            if (energyRatio.HasValue)
            {
                UguiViewUtil.SetFillAmount(_bindings.EnergyFill, energyRatio.Value);
            }
        }

        /// <summary>
        /// 释放事件订阅。
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _context.Phase.Changed -= _onPhaseChanged;
            _context.PlayerHp.Changed -= _onPlayerHpChanged;
            _context.PlayerMaxHp.Changed -= _onPlayerMaxHpChanged;
            _context.PlayerArmor.Changed -= _onPlayerArmorChanged;
            _context.Energy.Changed -= _onEnergyChanged;
            _context.MaxEnergy.Changed -= _onMaxEnergyChanged;
            _context.IsLevelComplete.Changed -= _onLevelCompleteChanged;
            _context.IsPlayerDead.Changed -= _onPlayerDeadChanged;
            _context.PlayerBuffs.Changed -= _onPlayerBuffsChanged;
            _bindings = default;
        }

        private void RefreshBuffs()
        {
            if (_disposed)
            {
                return;
            }

            BuffIconRenderer.Render(_bindings.PlayerBuffBar, _bindings.BuffIconTemplate, _context.PlayerBuffs.Value);
        }

        private static string GetPhaseLabel(BattlePhase phase)
        {
            return phase switch
            {
                BattlePhase.Prepare => "准备阶段",
                BattlePhase.PlayerTurn => "你的回合",
                BattlePhase.MonsterTurn => "怪物回合",
                BattlePhase.Check => "判定中",
                BattlePhase.Reward => "选择奖励",
                BattlePhase.Idle => "等待中",
                _ => phase.ToString(),
            };
        }
    }

    /// <summary>
    /// 玩家状态视图所需 UGUI 绑定。
    /// </summary>
    public struct PlayerStatusBindings
    {
        public TextMeshProUGUI InfoText;
        public Image HpFill;
        public TextMeshProUGUI HpText;
        public TextMeshProUGUI ArmorText;
        public Image EnergyFill;
        public TextMeshProUGUI EnergyText;
        public RectTransform PlayerBuffBar;
        public GameObject BuffIconTemplate;
    }
}
