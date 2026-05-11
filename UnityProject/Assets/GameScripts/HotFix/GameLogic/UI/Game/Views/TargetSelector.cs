using System;
using EF.Debugger;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameLogic
{
    /// <summary>
    /// 目标选择编排器。负责"出 SingleManual 卡 → 选目标"跨模块流程：
    /// 调 MonsterListView.EnterTargetMode 高亮存活怪物 + 注册 ESC / 空白点击监听；
    /// 怪物点击 → 调 ITargetContext.UseCardOnMonster + HandFanView.RequestGhostCleanup；
    /// ESC/空白/外部 Cancel → 调 HandFanView.RequestGhostRebound 协同回弹。
    /// </summary>
    public sealed class TargetSelector : IDisposable
    {
        private enum TargetSelectorState { Idle, Active }

        private VisualElement _rootElement;
        private IMonsterTargetSurface _monsterList;
        private IHandGhostSurface _handFan;
        private ITargetContext _context;

        private TargetSelectorState _state = TargetSelectorState.Idle;
        private int _selectedHandIdx = -1;

        private EventCallback<KeyDownEvent> _keyHandler;
        private EventCallback<PointerDownEvent> _backdropHandler;

        private bool _disposed;

        /// <summary>当前是否处于目标选择中。</summary>
        public bool IsActive => _state == TargetSelectorState.Active;

        /// <summary>构造目标选择编排器。</summary>
        /// <param name="rootElement">用于注册 ESC / 空白点击监听的根 VisualElement（typically GameView 自身）。</param>
        /// <param name="monsterList">怪物列表视图，调 EnterTargetMode/ExitTargetMode 切换高亮态。</param>
        /// <param name="handFan">手牌视图，调 RequestGhostCleanup/RequestGhostRebound 处理 ghost。</param>
        /// <param name="context">实现 ITargetContext 的切片对象。</param>
        public TargetSelector(VisualElement rootElement, IMonsterTargetSurface monsterList, IHandGhostSurface handFan, ITargetContext context)
        {
            _rootElement = rootElement ?? throw new ArgumentNullException(nameof(rootElement));
            _monsterList = monsterList ?? throw new ArgumentNullException(nameof(monsterList));
            _handFan = handFan ?? throw new ArgumentNullException(nameof(handFan));
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// 进入目标选择态：MonsterListView 高亮存活怪物 + 注册 ESC / 空白点击监听。
        /// 重复进入（IsActive==true）会被忽略并通过 Log.Warning 记录。
        /// </summary>
        public void Enter(int handIdx)
        {
            if (_disposed) return;
            if (_state == TargetSelectorState.Active)
            {
                Log.Warning($"[TargetSelector] Enter({handIdx}) 时已处于 Active 态，忽略");
                return;
            }

            _state = TargetSelectorState.Active;
            _selectedHandIdx = handIdx;

            // MonsterListView 进入 target 模式：每只存活怪物加 .target-selectable.active 类与点击回调
            _monsterList.EnterTargetMode(OnMonsterClicked);

            // 注册 ESC 监听
            _keyHandler = OnKeyDown;
            _rootElement.RegisterCallback(_keyHandler, TrickleDown.TrickleDown);
            _rootElement.focusable = true;
            _rootElement.Focus();

            // 注册空白点击监听（点击非怪物 / 非 drop-zone 区域为取消）
            _backdropHandler = OnBackdropPointerDown;
            _rootElement.RegisterCallback(_backdropHandler, TrickleDown.TrickleDown);
        }

        /// <summary>外部强制取消（如 Phase 中途变化）。等价于 ESC / 空白点击取消。</summary>
        public void Cancel()
        {
            if (_disposed || _state != TargetSelectorState.Active) return;
            ExitInternal(confirmed: false);
        }

        // ── 内部事件处理 ──

        private void OnMonsterClicked(int monsterIdx)
        {
            if (_disposed || _state != TargetSelectorState.Active) return;
            int handIdx = _selectedHandIdx;
            ExitInternal(confirmed: true);
            _context.UseCardOnMonster(handIdx, monsterIdx);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Escape) return;
            ExitInternal(confirmed: false);
            evt.StopPropagation();
        }

        private void OnBackdropPointerDown(PointerDownEvent evt)
        {
            // 仅响应非怪物的点击为取消（怪物自身点击会通过 MonsterListView 内部 ClickEvent 路径走 OnMonsterClicked）
            if (!(evt.target is VisualElement target)) return;

            // 检查是否点在某个怪物上 —— 若是则交给 MonsterListView 的 ClickEvent 处理，本回调不取消
            if (_monsterList?.Items != null)
            {
                foreach (var view in _monsterList.Items)
                {
                    var root = view?.Root;
                    if (root != null && IsSameOrAncestor(root, target)) return;
                }
            }

            ExitInternal(confirmed: false);
            evt.StopPropagation();
        }

        /// <summary>
        /// 退出目标选择态。confirmed 路径调 RequestGhostCleanup（怪物点击后），
        /// cancelled 路径调 RequestGhostRebound（ESC / 空白 / 外部 Cancel）。
        /// </summary>
        private void ExitInternal(bool confirmed)
        {
            int handIdx = _selectedHandIdx;

            // 解除 MonsterListView 目标态
            _monsterList?.ExitTargetMode();

            // 解除 ESC / 空白点击监听
            if (_keyHandler != null)
            {
                _rootElement?.UnregisterCallback(_keyHandler, TrickleDown.TrickleDown);
                _keyHandler = null;
            }
            if (_backdropHandler != null)
            {
                _rootElement?.UnregisterCallback(_backdropHandler, TrickleDown.TrickleDown);
                _backdropHandler = null;
            }

            // 状态归位
            _state = TargetSelectorState.Idle;
            _selectedHandIdx = -1;

            // ghost 处理：confirmed → cleanup（自然由 Hand.Changed 重建）；cancelled → rebound 协同动画
            if (confirmed)
            {
                _handFan?.RequestGhostCleanup();
            }
            else
            {
                _handFan?.RequestGhostRebound(handIdx);
            }
        }

        /// <summary>判断 target 是否等于 ancestor 或为其后代。</summary>
        private static bool IsSameOrAncestor(VisualElement ancestor, VisualElement target)
        {
            var current = target;
            while (current != null)
            {
                if (current == ancestor) return true;
                current = current.parent;
            }
            return false;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed) return;

            // 兜底：若仍 Active，等价于 Cancel（cancelled 路径，触发 RequestGhostRebound）
            if (_state == TargetSelectorState.Active)
            {
                ExitInternal(confirmed: false);
            }

            _disposed = true;
            _rootElement = null;
            _monsterList = null;
            _handFan = null;
            _context = null;
        }
    }
}
