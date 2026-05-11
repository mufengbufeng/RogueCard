using System;
using System.Collections.Generic;
using EF.Debugger;
using UnityEngine.UIElements;

namespace GameLogic
{
    /// <summary>
    /// 玩家状态面板视图控制器。封装 info-bar 文本 + HP/护甲/能量进度条与文本 + 玩家 Buff 状态条。
    /// 订阅 IPlayerStatusContext 的 9 个 ReactiveProperty，任一变化触发对应刷新。
    /// 不持有完整 GameViewModel，编译期防越界访问 Hand / CardPlayFailed 等手牌交互字段。
    /// </summary>
    public class PlayerStatusView : IDisposable
    {
        private IPlayerStatusContext _context;

        // === UI 元素 ===
        private Label _infoLabel;
        private VisualElement _hpBarFill;
        private Label _hpText;
        private Label _armorText;
        private VisualElement _energyBarFill;
        private Label _energyText;
        private VisualElement _playerBuffBar;

        // === 缓存的订阅委托（用于对称解绑）===
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
        /// 构造玩家状态视图。在 GameView 根节点上 Q 出 7 个元素引用，订阅 9 个 ReactiveProperty 并立即同步首帧。
        /// </summary>
        /// <param name="gameViewRoot">GameView 根节点（GameUxml 模板根，包含 info-bar / player-status / player-buff-bar 等元素）。</param>
        /// <param name="context">实现 IPlayerStatusContext 的切片对象（生产为 GameViewModel）。</param>
        public PlayerStatusView(VisualElement gameViewRoot, IPlayerStatusContext context)
        {
            _context = context;
            if (gameViewRoot == null || _context == null)
            {
                Log.Warning("[PlayerStatusView] gameViewRoot 或 context 为 null，跳过初始化");
                return;
            }

            _infoLabel = gameViewRoot.Q<Label>("info-text");
            _hpBarFill = gameViewRoot.Q("hp-bar-fill");
            _hpText = gameViewRoot.Q<Label>("hp-text");
            _armorText = gameViewRoot.Q<Label>("armor-text");
            _energyBarFill = gameViewRoot.Q("energy-bar-fill");
            _energyText = gameViewRoot.Q<Label>("energy-text");
            _playerBuffBar = gameViewRoot.Q("player-buff-bar");

            // 订阅 ReactiveProperty.Changed —— 缓存委托引用以便对称解绑
            _onPhaseChanged = _ => RefreshInfo();
            _onPlayerHpChanged = _ => RefreshInfo();
            _onPlayerMaxHpChanged = _ => RefreshInfo();
            _onPlayerArmorChanged = _ => RefreshInfo();
            _onEnergyChanged = _ => RefreshInfo();
            _onMaxEnergyChanged = _ => RefreshInfo();
            _onLevelCompleteChanged = _ => RefreshInfo();
            _onPlayerDeadChanged = _ => RefreshInfo();
            _onPlayerBuffsChanged = _ => RefreshPlayerBuffBar();

            _context.Phase.Changed += _onPhaseChanged;
            _context.PlayerHp.Changed += _onPlayerHpChanged;
            _context.PlayerMaxHp.Changed += _onPlayerMaxHpChanged;
            _context.PlayerArmor.Changed += _onPlayerArmorChanged;
            _context.Energy.Changed += _onEnergyChanged;
            _context.MaxEnergy.Changed += _onMaxEnergyChanged;
            _context.IsLevelComplete.Changed += _onLevelCompleteChanged;
            _context.IsPlayerDead.Changed += _onPlayerDeadChanged;
            _context.PlayerBuffs.Changed += _onPlayerBuffsChanged;

            // 立即同步首帧（避免首次 Show 时 UI 空白）
            RefreshInfo();
            RefreshPlayerBuffBar();
        }

        /// <summary>
        /// 刷新 info-text、HP 进度条/文本、护甲文本、能量进度条/文本。
        /// info-text 优先级：IsLevelComplete > IsPlayerDead > Phase 中文映射。
        /// HP / 能量进度条按百分比设置 width；MaxHp 或 MaxEnergy 为 0 时不写入避免除零。
        /// </summary>
        private void RefreshInfo()
        {
            if (_disposed || _context == null) return;

            var phase = _context.Phase.Value;
            bool levelComplete = _context.IsLevelComplete.Value;
            bool playerDead = _context.IsPlayerDead.Value;
            int hp = _context.PlayerHp.Value;
            int maxHp = _context.PlayerMaxHp.Value;
            int armor = _context.PlayerArmor.Value;
            int energy = _context.Energy.Value;
            int maxEnergy = _context.MaxEnergy.Value;

            if (_infoLabel != null)
            {
                string phaseLabel = phase switch
                {
                    BattlePhase.Prepare => "准备阶段",
                    BattlePhase.PlayerTurn => "你的回合",
                    BattlePhase.MonsterTurn => "怪物回合",
                    BattlePhase.Check => "判定中",
                    BattlePhase.Reward => "选择奖励",
                    BattlePhase.Idle => "等待中",
                    _ => phase.ToString()
                };

                _infoLabel.text = levelComplete ? "关卡完成！" : playerDead ? "玩家死亡" : phaseLabel;
            }

            if (_hpBarFill != null && maxHp > 0)
            {
                float hpPercent = (float)hp / maxHp * 100;
                _hpBarFill.style.width = new StyleLength(new Length(hpPercent, LengthUnit.Percent));
            }

            if (_hpText != null) _hpText.text = $"{hp}/{maxHp}";
            if (_armorText != null) _armorText.text = armor > 0 ? armor.ToString() : "0";

            if (_energyBarFill != null && maxEnergy > 0)
            {
                float energyPercent = (float)energy / maxEnergy * 100;
                _energyBarFill.style.width = new StyleLength(new Length(energyPercent, LengthUnit.Percent));
            }

            if (_energyText != null) _energyText.text = $"{energy}/{maxEnergy}";
        }

        /// <summary>
        /// 刷新玩家 Buff 状态条。把 IReadOnlyList 包成 IList 给共享 BuffBarRenderer。
        /// </summary>
        private void RefreshPlayerBuffBar()
        {
            if (_disposed || _context == null) return;
            var buffs = _context.PlayerBuffs.Value;
            BuffBarRenderer.Render(_playerBuffBar, buffs as IList<BuffRuntime> ?? ToList(buffs));
        }

        /// <summary>
        /// 把 IReadOnlyList 拷贝为 IList，供 BuffBarRenderer 的 IList 入参使用。
        /// </summary>
        private static IList<BuffRuntime> ToList(IReadOnlyList<BuffRuntime> src)
        {
            if (src == null) return null;
            var list = new List<BuffRuntime>(src.Count);
            for (int i = 0; i < src.Count; i++) list.Add(src[i]);
            return list;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_context != null)
            {
                if (_onPhaseChanged != null) _context.Phase.Changed -= _onPhaseChanged;
                if (_onPlayerHpChanged != null) _context.PlayerHp.Changed -= _onPlayerHpChanged;
                if (_onPlayerMaxHpChanged != null) _context.PlayerMaxHp.Changed -= _onPlayerMaxHpChanged;
                if (_onPlayerArmorChanged != null) _context.PlayerArmor.Changed -= _onPlayerArmorChanged;
                if (_onEnergyChanged != null) _context.Energy.Changed -= _onEnergyChanged;
                if (_onMaxEnergyChanged != null) _context.MaxEnergy.Changed -= _onMaxEnergyChanged;
                if (_onLevelCompleteChanged != null) _context.IsLevelComplete.Changed -= _onLevelCompleteChanged;
                if (_onPlayerDeadChanged != null) _context.IsPlayerDead.Changed -= _onPlayerDeadChanged;
                if (_onPlayerBuffsChanged != null) _context.PlayerBuffs.Changed -= _onPlayerBuffsChanged;
            }

            _onPhaseChanged = null;
            _onPlayerHpChanged = null;
            _onPlayerMaxHpChanged = null;
            _onPlayerArmorChanged = null;
            _onEnergyChanged = null;
            _onMaxEnergyChanged = null;
            _onLevelCompleteChanged = null;
            _onPlayerDeadChanged = null;
            _onPlayerBuffsChanged = null;

            _context = null;
            _infoLabel = null;
            _hpBarFill = null;
            _hpText = null;
            _armorText = null;
            _energyBarFill = null;
            _energyText = null;
            _playerBuffBar = null;
        }
    }
}
