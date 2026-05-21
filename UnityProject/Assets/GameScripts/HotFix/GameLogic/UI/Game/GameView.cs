using EF.Debugger;
using EF.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic
{
    /// <summary>
    /// UGUI 局内界面视图，负责从 Prefab 绑定组件并装配局内战斗子视图。
    /// </summary>
    public class GameView : UIView
    {
        [UHubBind("BattlePanel")]
        private GameObject _battlePanel;

        [UHubBind("RewardPanel")]
        private GameObject _rewardPanel;

        [UHubBind("PlayerStatusPanel")]
        private RectTransform _playerStatusPanel;

        [UHubBind("InfoText")]
        private TextMeshProUGUI _infoText;

        [UHubBind("PlayerHpFill")]
        private Image _playerHpFill;

        [UHubBind("PlayerHpText")]
        private TextMeshProUGUI _playerHpText;

        [UHubBind("PlayerArmorText")]
        private TextMeshProUGUI _playerArmorText;

        [UHubBind("PlayerEnergyFill")]
        private Image _playerEnergyFill;

        [UHubBind("PlayerEnergyText")]
        private TextMeshProUGUI _playerEnergyText;

        [UHubBind("PlayerBuffBar")]
        private RectTransform _playerBuffBar;

        [UHubBind("MonsterRect")]
        private RectTransform _monsterContainer;

        [UHubBind("CardSc")]
        private RectTransform _handContainer;

        [UHubBind("DropZone")]
        private RectTransform _dropZone;

        [UHubBind("PreviewLayer")]
        private RectTransform _previewLayer;

        [UHubBind("EndBtn")]
        private Button _endTurnButton;

        [UHubBind("FailToast")]
        private TextMeshProUGUI _failToastText;

        [UHubBind("RewardConfirmBtn")]
        private Button _rewardConfirmButton;

        [UHubBind("HandCardTemplate")]
        private GameObject _handCardTemplate;

        [UHubBind("MonsterItemTemplate")]
        private GameObject _monsterItemTemplate;

        [UHubBind("BuffIconTemplate")]
        private GameObject _buffIconTemplate;

        [UHubBind("IntentIconTemplate")]
        private GameObject _intentIconTemplate;

        private PlayerStatusView _playerStatusView;
        private BattlePanelView _battlePanelView;
        private GameViewModel _viewModel;

        /// <summary>
        /// 奖励确认按钮点击事件。
        /// </summary>
        public event System.Action RewardConfirmClicked;

        /// <summary>
        /// 兼容测试与旧 Controller 边界的结束回合事件。
        /// </summary>
        public event System.Action EndTurnClicked;

        /// <summary>
        /// 兼容测试与旧 Controller 边界的手牌点击事件。
        /// </summary>
        public event System.Action<int> HandCardClicked;

        /// <summary>
        /// 兼容测试与旧 Controller 边界的怪物目标选择事件。
        /// </summary>
        public event System.Action<int, int> MonsterTargetSelected;

        /// <summary>
        /// 初始化 UHub 组件绑定。
        /// </summary>
        protected override void OnInitialize()
        {
            base.OnInitialize();
            UHub.Initialize();
        }

        /// <summary>
        /// 绑定奖励确认按钮。
        /// </summary>
        protected override void OnBindings()
        {
            base.OnBindings();
            if (_rewardConfirmButton != null)
            {
                BindEvent(_rewardConfirmButton.onClick, () => RewardConfirmClicked?.Invoke());
            }
        }

        /// <summary>
        /// 打开局内界面。
        /// </summary>
        protected override void OnOpen(object userData)
        {
            base.OnOpen(userData);
            Log.Info("[GameView] 局内 UGUI 界面已打开");
        }

        /// <summary>
        /// 刷新局内界面。
        /// </summary>
        public void Render(GameViewModel viewModel)
        {
            if (viewModel == null)
            {
                return;
            }

            if (!ReferenceEquals(_viewModel, viewModel))
            {
                DisposeBattleViews();
                _viewModel = viewModel;
                _playerStatusView = new PlayerStatusView(BuildPlayerStatusBindings(), _viewModel);
            }

            bool rewardPhase = viewModel.Phase.Value == BattlePhase.Reward || viewModel.IsLevelComplete.Value;
            SetPanelActive(_battlePanel, !rewardPhase);
            SetPanelActive(_rewardPanel, rewardPhase);

            if (rewardPhase)
            {
                DisposeBattlePanelOnly();
                return;
            }

            EnsureBattlePanel();
        }

        /// <summary>
        /// 显示出牌失败提示。
        /// </summary>
        public void ShowCardPlayFailed(string reason)
        {
            _battlePanelView?.ShowCardPlayFailed(reason);
        }

        /// <summary>
        /// 释放子视图和事件。
        /// </summary>
        protected override void OnRelease()
        {
            DisposeBattleViews();
            RewardConfirmClicked = null;
            EndTurnClicked = null;
            HandCardClicked = null;
            MonsterTargetSelected = null;
            base.OnRelease();
        }

        /// <summary>
        /// 测试专用：模拟结束回合点击。
        /// </summary>
        internal void NotifyEndTurnClickedForTests() => EndTurnClicked?.Invoke();

        /// <summary>
        /// 测试专用：模拟奖励确认点击。
        /// </summary>
        internal void NotifyRewardConfirmClickedForTests() => RewardConfirmClicked?.Invoke();

        /// <summary>
        /// 测试专用：模拟点击手牌。
        /// </summary>
        internal void NotifyHandCardClickedForTests(int handIndex) => HandCardClicked?.Invoke(handIndex);

        /// <summary>
        /// 测试专用：模拟选择怪物目标。
        /// </summary>
        internal void NotifyMonsterTargetSelectedForTests(int handIndex, int monsterIndex) =>
            MonsterTargetSelected?.Invoke(handIndex, monsterIndex);

        private void EnsureBattlePanel()
        {
            if (_battlePanelView != null || _viewModel == null)
            {
                return;
            }

            _battlePanelView = new BattlePanelView(BuildBattlePanelBindings(), _viewModel, new HandFanLayoutOptions());
        }

        private PlayerStatusBindings BuildPlayerStatusBindings()
        {
            return new PlayerStatusBindings
            {
                InfoText = _infoText,
                HpFill = _playerHpFill,
                HpText = _playerHpText,
                ArmorText = _playerArmorText,
                EnergyFill = _playerEnergyFill,
                EnergyText = _playerEnergyText,
                PlayerBuffBar = _playerBuffBar,
                BuffIconTemplate = _buffIconTemplate,
            };
        }

        private BattlePanelBindings BuildBattlePanelBindings()
        {
            CanvasGroup failToastGroup = null;
            if (_failToastText != null)
            {
                failToastGroup = _failToastText.GetComponent<CanvasGroup>();
                if (failToastGroup == null)
                {
                    failToastGroup = _failToastText.gameObject.AddComponent<CanvasGroup>();
                }
            }

            return new BattlePanelBindings
            {
                MonsterContainer = _monsterContainer,
                HandContainer = _handContainer,
                DropZone = _dropZone,
                PreviewLayer = _previewLayer,
                EndTurnButton = _endTurnButton,
                FailToast = new TextMeshProUGUIProxy { Text = _failToastText, Group = failToastGroup },
                CancelTargetButton = _dropZone != null ? _dropZone.GetComponent<Button>() : null,
                HandCardTemplate = _handCardTemplate,
                MonsterItemTemplate = _monsterItemTemplate,
                BuffIconTemplate = _buffIconTemplate,
                IntentIconTemplate = _intentIconTemplate,
            };
        }

        private void DisposeBattleViews()
        {
            DisposeBattlePanelOnly();
            _playerStatusView?.Dispose();
            _playerStatusView = null;
            _viewModel = null;
        }

        private void DisposeBattlePanelOnly()
        {
            _battlePanelView?.Dispose();
            _battlePanelView = null;
        }

        private static void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null && panel.activeSelf != active)
            {
                panel.SetActive(active);
            }
        }
    }
}
