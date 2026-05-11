using System;
using EF.Debugger;
using UnityEngine.UIElements;

namespace GameLogic
{
    /// <summary>
    /// 战斗子界面顶层协调器：在 Region.ShowAsync("BattlePanel") 后由 GameView 实例化，
    /// 内部装配 MonsterListView + HandFanView + TurnControlView + TargetSelector，
    /// 订阅 HandFanView 三事件按 needsManualTarget 路由，订阅 Phase.Changed 强制取消选目标。
    /// 持有 6 个共享元素引用（preview-layer / drop-zone / monster-container / hand-fan / end-turn-btn / fail-toast）。
    /// </summary>
    public sealed class BattlePanelView : IDisposable
    {
        // === 共享元素（仅 BattlePanelView 生命周期内有效）===
        private VisualElement _content;
        private VisualElement _previewLayer;
        private VisualElement _dropZone;
        private VisualElement _monsterContainer;
        private VisualElement _handFan;
        private Button _endTurnBtn;
        private Label _failToast;

        // === 子模块 ===
        private MonsterListView _monsterListView;
        private HandFanView _handFanView;
        private TurnControlView _turnControlView;
        private TargetSelector _targetSelector;

        // === Context（含 Phase.Changed 订阅）===
        private IBattleContext _context;
        private Action<BattlePhase> _onPhaseChanged;

        private bool _disposed;

        /// <summary>
        /// 构造 BattlePanelView：查询 6 个共享元素 + 装配 4 个子模块 + 订阅 HandFanView 三事件 + Phase.Changed。
        /// </summary>
        /// <param name="content">BattlePanel.uxml 加载后的根 VisualElement（_mainRegion.CurrentContent）。</param>
        /// <param name="context">IBattleContext 切片（生产为 GameViewModel）。</param>
        /// <param name="monsterItemTpl">MonsterItem.uxml 模板。</param>
        /// <param name="cardItemTpl">CardItem.uxml 模板。</param>
        /// <param name="handFanOptions">扇形布局与拖拽手感参数。</param>
        public BattlePanelView(
            VisualElement content,
            IBattleContext context,
            VisualTreeAsset monsterItemTpl,
            VisualTreeAsset cardItemTpl,
            HandFanLayoutOptions handFanOptions)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _context = context ?? throw new ArgumentNullException(nameof(context));

            // 防御性检测：旧版 BattlePanel.uxml 残留 card-scroll
            var legacyScroll = content.Q<ScrollView>("card-scroll");
            if (legacyScroll != null)
            {
                Log.Error("[BattlePanelView] 检测到旧版 BattlePanel.uxml（仍有 card-scroll ScrollView）。" +
                          "请：① 停止 Play 模式 ② 在 Project 面板对 BattlePanel.uxml 右键 Reimport ③ 重新进入 Play。");
                legacyScroll.style.display = DisplayStyle.None;
                legacyScroll.pickingMode = PickingMode.Ignore;
            }

            // 查询 6 个共享元素
            _monsterContainer = content.Q("monster-container");
            _handFan = content.Q("hand-fan");
            _previewLayer = content.Q("preview-layer");
            _dropZone = content.Q("drop-zone");
            _endTurnBtn = content.Q<Button>("end-turn-btn");
            _failToast = content.Q<Label>("fail-toast");

            if (_handFan == null) Log.Error("[BattlePanelView] BattlePanel.uxml 缺少 name=\"hand-fan\" 容器");
            if (_previewLayer == null) Log.Error("[BattlePanelView] BattlePanel.uxml 缺少 name=\"preview-layer\" 容器");

            // 装配子模块（按设计 §1 顺序：MonsterListView → HandFanView → TurnControlView → TargetSelector）
            _monsterListView = new MonsterListView(_monsterContainer, _context, monsterItemTpl);
            _handFanView = new HandFanView(_handFan, _dropZone, _previewLayer, _content, _context, cardItemTpl, handFanOptions ?? new HandFanLayoutOptions());
            _turnControlView = new TurnControlView(_endTurnBtn, _failToast, _context);
            _targetSelector = new TargetSelector(_content, _monsterListView, _handFanView, _context);

            // 订阅 HandFanView 三事件
            _handFanView.CardDroppedOnZone += OnCardDroppedOnZone;
            _handFanView.CardClicked += OnCardClicked;
            _handFanView.CardDragCancelled += OnCardDragCancelled;

            // 订阅 Phase.Changed：怪物回合开始强制取消选目标
            // 通过 ITurnContext.Phase 消解多接口下的二义性
            _onPhaseChanged = OnPhaseChanged;
            ((ITurnContext)_context).Phase.Changed += _onPhaseChanged;
        }

        // ── HandFanView 事件路由 ──

        /// <summary>
        /// 在 drop-zone 内松手：needsManualTarget=true → TargetSelector.Enter；否则直接 UseCard。
        /// </summary>
        private void OnCardDroppedOnZone(int handIdx, bool needsManualTarget)
        {
            if (_disposed) return;
            if (needsManualTarget)
            {
                _targetSelector?.Enter(handIdx);
            }
            else
            {
                _context.UseCard(handIdx);
            }
        }

        /// <summary>单击某卡：预览态由 HandFanView 内部 CardPreviewController 处理，本视图无操作。</summary>
        private void OnCardClicked(int handIdx) { }

        /// <summary>中间地带松手 / capture lost：rebound 由 CardDragController 内部完成，本视图无操作。</summary>
        private void OnCardDragCancelled(int handIdx) { }

        /// <summary>Phase 离开 PlayerTurn 时强制取消 TargetSelector，避免怪物回合期间残留选目标 UI。</summary>
        private void OnPhaseChanged(BattlePhase phase)
        {
            if (_disposed) return;
            if (_targetSelector != null && _targetSelector.IsActive && phase != BattlePhase.PlayerTurn)
            {
                _targetSelector.Cancel();
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // 1. 解绑 Phase.Changed
            if (_context != null && _onPhaseChanged != null)
            {
                ((ITurnContext)_context).Phase.Changed -= _onPhaseChanged;
            }
            _onPhaseChanged = null;

            // 2. 解绑 HandFanView 三事件
            if (_handFanView != null)
            {
                _handFanView.CardDroppedOnZone -= OnCardDroppedOnZone;
                _handFanView.CardClicked -= OnCardClicked;
                _handFanView.CardDragCancelled -= OnCardDragCancelled;
            }

            // 3-6. 按反序释放：TargetSelector → TurnControlView → HandFanView → MonsterListView
            _targetSelector?.Dispose();
            _targetSelector = null;
            _turnControlView?.Dispose();
            _turnControlView = null;
            _handFanView?.Dispose();
            _handFanView = null;
            _monsterListView?.Dispose();
            _monsterListView = null;

            _context = null;
            _content = null;
            _previewLayer = null;
            _dropZone = null;
            _monsterContainer = null;
            _handFan = null;
            _endTurnBtn = null;
            _failToast = null;
        }
    }
}
