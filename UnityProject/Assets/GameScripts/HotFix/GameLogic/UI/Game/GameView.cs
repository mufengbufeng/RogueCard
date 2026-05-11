using System;
using EF.Debugger;
using EF.UI;
using UnityEngine.UIElements;

namespace GameLogic
{
    /// <summary>
    /// 局内界面顶层 Screen，作为路由协调器：
    /// 装配常驻 PlayerStatusView；按 Phase 切换 Region 加载 BattlePanel / RewardPanel；
    /// BattlePanel 内的全部装配（怪物列表 / 手牌 / 选目标 / 回合控制）由 BattlePanelView 接管。
    /// 类名 `GameView` 决定按命名约定加载的资源：UXML=`GameUxml`、USS=`GameUss`。
    /// </summary>
    public class GameView : Screen<GameViewModel>
    {
        private enum GameRegionKind
        {
            None,
            Battle,
            Reward
        }

        private PlayerStatusView _playerStatusView;
        private BattlePanelView _battlePanelView;
        private Button _rewardConfirmBtn;

        private Region _mainRegion;
        private GameRegionKind _activeRegion = GameRegionKind.None;
        private int _regionSwitchVersion;

        private VisualTreeAsset _monsterItemVta;
        private VisualTreeAsset _cardItemVta;

        private readonly HandFanLayoutOptions _handFanLayoutOptions = new();

        /// <inheritdoc />
        protected override void OnSetup()
        {
            // 常驻区域：玩家状态面板（位于 GameUxml.uxml 顶部 info-bar / player-status / player-buff-bar）
            _playerStatusView = new PlayerStatusView(this, ViewModel);

            var slot = this.Q("main-region");
            if (slot == null)
            {
                Log.Error("[GameView] GameUxml.uxml 缺少 name=\"main-region\" 容器");
                return;
            }
            _mainRegion = new Region(slot, GameLogicEntry.Resource);

            ViewModel.Phase.Changed += OnPhaseChanged;

            Log.Info("[GameView] 局内界面绑定完成");
        }

        /// <inheritdoc />
        public override void OnShow()
        {
            LoadItemTemplates();
            OnPhaseChanged(ViewModel.Phase.Value);
            Log.Info("[GameView] 局内界面已显示");
        }

        private async void OnPhaseChanged(BattlePhase phase)
        {
            if (_mainRegion == null) return;

            GameRegionKind target = MapPhaseToRegion(phase);
            if (target == _activeRegion) return;
            _activeRegion = target;

            int switchVersion = ++_regionSwitchVersion;

            try
            {
                switch (target)
                {
                    case GameRegionKind.Battle:
                        await _mainRegion.ShowAsync("BattlePanel");
                        if (switchVersion != _regionSwitchVersion) return;
                        BindBattleContent();
                        break;
                    case GameRegionKind.Reward:
                        DisposeBattlePanel();
                        await _mainRegion.ShowAsync("RewardPanel");
                        if (switchVersion != _regionSwitchVersion) return;
                        BindRewardContent();
                        break;
                    default:
                        // Idle 等阶段保持当前内容
                        break;
                }
            }
            catch (Exception e)
            {
                Log.Error($"[GameView] 切换 Region 失败：{e.Message}");
            }
        }

        /// <summary>把详细 Phase 映射到 UI Region 路由：战斗中各阶段都用 BattlePanel；Reward 用 RewardPanel。</summary>
        private static GameRegionKind MapPhaseToRegion(BattlePhase phase) => phase switch
        {
            BattlePhase.Prepare => GameRegionKind.Battle,
            BattlePhase.PlayerTurn => GameRegionKind.Battle,
            BattlePhase.MonsterTurn => GameRegionKind.Battle,
            BattlePhase.Check => GameRegionKind.Battle,
            BattlePhase.Reward => GameRegionKind.Reward,
            _ => GameRegionKind.None
        };

        private void BindBattleContent()
        {
            var content = _mainRegion?.CurrentContent;
            if (content == null) return;
            _battlePanelView?.Dispose();
            _battlePanelView = new BattlePanelView(content, ViewModel, _monsterItemVta, _cardItemVta, _handFanLayoutOptions);
        }

        private void BindRewardContent()
        {
            var content = _mainRegion?.CurrentContent;
            if (content == null) return;
            _rewardConfirmBtn = content.Q<Button>("reward-confirm-btn");
            _rewardConfirmBtn?.RegisterCallback<ClickEvent>(_ => ViewModel.SelectReward());
        }

        private void DisposeBattlePanel()
        {
            _battlePanelView?.Dispose();
            _battlePanelView = null;
        }

        private void LoadItemTemplates()
        {
            if (_monsterItemVta != null && _cardItemVta != null) return;
            try
            {
                var rm = GameLogicEntry.Resource;
                if (rm == null) return;
                _monsterItemVta = LoadTemplate(rm, "MonsterItem");
                _cardItemVta = LoadTemplate(rm, "CardItem");
            }
            catch (Exception e)
            {
                Log.Warning($"[GameView] 加载模板失败：{e.Message}");
            }
        }

        private static VisualTreeAsset LoadTemplate(EF.Resource.IResourceManager rm, string location)
        {
            try
            {
                var handle = rm.LoadAssetSync<VisualTreeAsset>(location);
                return handle.AssetObject as VisualTreeAsset;
            }
            catch (Exception e)
            {
                Log.Warning($"[GameView] 加载模板失败 {location}：{e.Message}");
                return null;
            }
        }

        /// <inheritdoc />
        public override void OnDispose()
        {
            _regionSwitchVersion++;

            if (ViewModel != null)
            {
                ViewModel.Phase.Changed -= OnPhaseChanged;
            }

            DisposeBattlePanel();
            _playerStatusView?.Dispose();
            _playerStatusView = null;
            _mainRegion?.Clear();

            base.OnDispose();
        }
    }
}
