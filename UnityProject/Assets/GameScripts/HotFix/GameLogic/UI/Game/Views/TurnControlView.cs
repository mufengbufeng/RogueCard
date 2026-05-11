using System;
using EF.Debugger;
using UnityEngine.UIElements;

namespace GameLogic
{
    /// <summary>
    /// 回合控制视图：封装 end-turn-btn 启用控制（按 Phase）+ 出牌失败 toast 显示（含中文映射 + 1.2s 自动隐藏 +
    /// 版本号"新失败覆盖旧失败"机制）。订阅 ITurnContext.Phase 与 CardPlayFailed。
    /// </summary>
    public sealed class TurnControlView : IDisposable
    {
        private Button _endTurnBtn;
        private Label _failToast;
        private ITurnContext _context;

        // 缓存订阅委托引用，便于对称解绑
        private EventCallback<ClickEvent> _onEndTurnClicked;
        private Action<BattlePhase> _onPhaseChanged;
        private Action<string> _onCardPlayFailed;

        // 版本号：每次 fail toast 显示自增；schedule 检查版本一致才隐藏（实现"新失败覆盖旧失败"）
        private long _toastVersion;
        private bool _disposed;

        /// <summary>构造回合控制视图。</summary>
        public TurnControlView(Button endTurnBtn, Label failToast, ITurnContext context)
        {
            _endTurnBtn = endTurnBtn;
            _failToast = failToast;
            _context = context ?? throw new ArgumentNullException(nameof(context));

            // 注册 end-turn 点击转发
            if (_endTurnBtn != null)
            {
                _onEndTurnClicked = _ => _context.EndTurn();
                _endTurnBtn.RegisterCallback(_onEndTurnClicked);
            }

            // 订阅 Phase / CardPlayFailed
            _onPhaseChanged = OnPhaseChanged;
            _onCardPlayFailed = OnCardPlayFailed;
            _context.Phase.Changed += _onPhaseChanged;
            _context.CardPlayFailed += _onCardPlayFailed;

            // 立即同步首帧（避免首次 Show 时按钮启用状态错位）
            OnPhaseChanged(_context.Phase.Value);
        }

        /// <summary>按 Phase 启用 / 禁用结束回合按钮。</summary>
        private void OnPhaseChanged(BattlePhase phase)
        {
            if (_disposed || _endTurnBtn == null) return;
            _endTurnBtn.SetEnabled(phase == BattlePhase.PlayerTurn);
        }

        /// <summary>
        /// 出牌失败 → 显示红色 toast，按 reason 映射中文，1.2 秒后自动隐藏。
        /// 用版本号实现"新失败覆盖旧失败"：每次显示自增，定时器只在版本一致时才隐藏。
        /// </summary>
        private void OnCardPlayFailed(string reason)
        {
            if (_disposed || _failToast == null) return;

            string text = MapReasonToZh(reason);
            _failToast.text = text;
            _failToast.AddToClassList("fail-toast--visible");

            long ver = ++_toastVersion;
            _failToast.schedule.Execute(() =>
            {
                if (_disposed) return;
                if (ver == _toastVersion && _failToast != null)
                {
                    _failToast.RemoveFromClassList("fail-toast--visible");
                }
            }).StartingIn(1200);
        }

        /// <summary>把后端失败原因字符串映射为中文文案。</summary>
        private static string MapReasonToZh(string reason) => reason switch
        {
            "InsufficientEnergy" => "能量不足",
            "NotPlayerTurn" => "现在不是你的回合",
            "InvalidTarget" => "无效目标",
            "InvalidHandIndex" => "卡牌索引错误",
            _ => "出牌失败",
        };

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // 自增版本号，让任何已调度的 schedule 在触发时检查不通过 → 不操作 _failToast
            _toastVersion++;

            if (_endTurnBtn != null && _onEndTurnClicked != null)
            {
                _endTurnBtn.UnregisterCallback(_onEndTurnClicked);
            }
            _onEndTurnClicked = null;

            if (_context != null)
            {
                if (_onPhaseChanged != null) _context.Phase.Changed -= _onPhaseChanged;
                if (_onCardPlayFailed != null) _context.CardPlayFailed -= _onCardPlayFailed;
            }
            _onPhaseChanged = null;
            _onCardPlayFailed = null;

            _context = null;
            _endTurnBtn = null;
            _failToast = null;
        }
    }
}
