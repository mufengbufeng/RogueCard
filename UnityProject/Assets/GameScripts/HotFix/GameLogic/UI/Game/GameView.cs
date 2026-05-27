using System.Threading;
using Cysharp.Threading.Tasks;
using EF.Debugger;
using EF.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YooAsset;

namespace GameLogic
{
    /// <summary>
    /// UGUI 局内界面视图，负责从 Prefab 绑定组件并装配局内战斗子视图。
    /// </summary>
    public class GameView : UIView
    {
        #region 自动生成
        [UHubBind("BgImage")] private Image _bgImage;
        [UHubBind("CardCostText")] private TextMeshProUGUI _cardCostText;
        [UHubBind("CardNameText")] private TextMeshProUGUI _cardNameText;
        [UHubBind("DropZoneText")] private TextMeshProUGUI _dropZoneText;
        [UHubBind("EndBtn")] private Button _endBtn;
        [UHubBind("InfoText")] private TextMeshProUGUI _infoText;
        [UHubBind("MonsterRect")] private RectTransform _monsterRect;
        [UHubBind("PlayerArmorText")] private TextMeshProUGUI _playerArmorText;
        [UHubBind("PlayerEnergyText")] private TextMeshProUGUI _playerEnergyText;
        [UHubBind("PlayerHpText")] private TextMeshProUGUI _playerHpText;
        [UHubBind("RewardConfirmBtn")] private Button _rewardConfirmBtn;
        [UHubBind("RewardTitleText")] private TextMeshProUGUI _rewardTitleText;
        [UHubBind("Text")] private TextMeshProUGUI _text;
        #endregion

        // 未在 ReferenceCollector 中登记的节点，通过 transform.Find 解析。
        private GameObject _battlePanel;
        private GameObject _rewardPanel;
        private Image _playerHpFill;
        private Image _playerEnergyFill;
        private RectTransform _playerBuffBar;
        private GameObject _buffIconTemplate;
        private GameObject _intentIconTemplate;
        private RectTransform _handContainer;
        private GameObject _handCardTemplate;
        private RectTransform _dropZone;
        private RectTransform _previewLayer;
        private TextMeshProUGUI _failToastText;
        private CanvasGroup _failToastGroup;

        // 怪物条目模板独立 Prefab，通过 ResourceManager 异步加载。
        private const string MonsterItemTemplateLocation = "GameMonsterItemTemplate";
        private AssetHandle _monsterItemTemplateHandle;
        private GameObject _monsterItemTemplate;

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
        /// 初始化 UHub 组件绑定，并解析未在 ReferenceCollector 中登记的子节点。
        /// </summary>
        protected override void OnInitialize()
        {
            base.OnInitialize();
            UHub.Initialize();
            ResolveTransformBindings();
        }

        /// <summary>
        /// 绑定按钮点击事件到对外暴露的事件。
        /// </summary>
        protected override void OnBindings()
        {
            base.OnBindings();
            if (_rewardConfirmBtn != null)
            {
                BindEvent(_rewardConfirmBtn.onClick, () => RewardConfirmClicked?.Invoke());
            }
            if (_endBtn != null)
            {
                BindEvent(_endBtn.onClick, () => EndTurnClicked?.Invoke());
            }
        }

        /// <summary>
        /// 异步加载局内所需的独立 Prefab（如怪物条目模板）。
        /// </summary>
        protected override async UniTask OnPrepareAsync(object userData, CancellationToken cancellationToken)
        {
            await base.OnPrepareAsync(userData, cancellationToken);
            await LoadMonsterItemTemplateAsync(cancellationToken);
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
        /// 释放子视图、外部资源句柄和事件。
        /// </summary>
        protected override void OnRelease()
        {
            DisposeBattleViews();
            ReleaseMonsterItemTemplate();
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
            CanvasGroup failToastGroup = _failToastGroup;
            if (failToastGroup == null && _failToastText != null)
            {
                failToastGroup = _failToastText.GetComponent<CanvasGroup>();
                if (failToastGroup == null)
                {
                    failToastGroup = _failToastText.gameObject.AddComponent<CanvasGroup>();
                }
            }

            Button cancelTargetButton = null;
            if (_dropZone != null)
            {
                cancelTargetButton = _dropZone.GetComponent<Button>();
            }

            return new BattlePanelBindings
            {
                MonsterContainer = _monsterRect,
                HandContainer = _handContainer,
                DropZone = _dropZone,
                PreviewLayer = _previewLayer,
                EndTurnButton = _endBtn,
                FailToast = new TextMeshProUGUIProxy { Text = _failToastText, Group = failToastGroup },
                CancelTargetButton = cancelTargetButton,
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

        private async UniTask LoadMonsterItemTemplateAsync(CancellationToken cancellationToken)
        {
            if (_monsterItemTemplate != null)
            {
                return;
            }

            var resource = GameLogicEntry.Resource;
            if (resource == null)
            {
                Log.Warning("[GameView] ResourceManager 不可用，跳过怪物模板加载");
                return;
            }

            try
            {
                _monsterItemTemplateHandle = await resource.LoadAssetAsync<GameObject>(MonsterItemTemplateLocation)
                    .AttachExternalCancellation(cancellationToken);
            }
            catch (System.OperationCanceledException)
            {
                throw;
            }
            catch (System.Exception ex)
            {
                Log.Error($"[GameView] 加载怪物条目模板失败：{ex.Message}");
                return;
            }

            if (_monsterItemTemplateHandle == null || _monsterItemTemplateHandle.AssetObject == null)
            {
                Log.Warning($"[GameView] 怪物条目模板资源缺失：{MonsterItemTemplateLocation}");
                return;
            }

            _monsterItemTemplate = _monsterItemTemplateHandle.AssetObject as GameObject;
        }

        private void ReleaseMonsterItemTemplate()
        {
            if (_monsterItemTemplateHandle != null)
            {
                GameLogicEntry.Resource?.Release(_monsterItemTemplateHandle);
                _monsterItemTemplateHandle = null;
            }
            _monsterItemTemplate = null;
        }

        private void ResolveTransformBindings()
        {
            _battlePanel = FindChildGameObject("BattlePanel");
            _rewardPanel = FindChildGameObject("RewardPanel");
            _playerHpFill = FindChildComponent<Image>("BattlePanel/PlayerStatusPanel/PlayerHpBar/PlayerHpFill");
            _playerEnergyFill = FindChildComponent<Image>("BattlePanel/PlayerStatusPanel/PlayerEnergyBar/PlayerEnergyFill");
            _playerBuffBar = FindChildComponent<RectTransform>("BattlePanel/PlayerStatusPanel/PlayerBuffBar");
            _buffIconTemplate = FindChildGameObject("BattlePanel/PlayerStatusPanel/BuffIconTemplate");
            _intentIconTemplate = FindChildGameObject("BattlePanel/PlayerStatusPanel/IntentIconTemplate");
            _handContainer = FindChildComponent<RectTransform>("BattlePanel/CardSc");
            _handCardTemplate = FindChildGameObject("BattlePanel/CardSc/HandCardTemplate");
            _dropZone = FindChildComponent<RectTransform>("BattlePanel/DropZone");
            _previewLayer = FindChildComponent<RectTransform>("BattlePanel/PreviewLayer");

            var failToastGo = FindChildGameObject("BattlePanel/FailToast");
            if (failToastGo != null)
            {
                _failToastText = failToastGo.GetComponent<TextMeshProUGUI>();
                _failToastGroup = failToastGo.GetComponent<CanvasGroup>();
            }
        }

        private GameObject FindChildGameObject(string path)
        {
            var child = transform.Find(path);
            return child != null ? child.gameObject : null;
        }

        private T FindChildComponent<T>(string path) where T : Component
        {
            var child = transform.Find(path);
            return child != null ? child.GetComponent<T>() : null;
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
