using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic
{
    /// <summary>
    /// UGUI 回合控制视图，控制结束回合按钮和出牌失败提示。
    /// </summary>
    public sealed class TurnControlView : IDisposable
    {
        private const int ToastDurationMs = 1200;

        private readonly ITurnContext _context;
        private readonly Action<TurnControlView, long, int> _scheduleHideToast;
        private readonly Func<bool> _shouldSuppressEndTurn;
        private Button _endTurnButton;
        private TextMeshProUGUI _failToastText;
        private CanvasGroup _failToastGroup;
        private GameObject _failToastRoot;
        private Action<BattlePhase> _onPhaseChanged;
        private Action<string> _onCardPlayFailed;
        private long _toastVersion;
        private bool _disposed;

        /// <summary>
        /// 创建回合控制视图。
        /// </summary>
        public TurnControlView(Button endTurnButton, TextMeshProUGUI failToastText, CanvasGroup failToastGroup, ITurnContext context)
            : this(endTurnButton, failToastText, failToastGroup, context, ScheduleHideToastAsync, null)
        {
        }

        /// <summary>
        /// 创建回合控制视图，并允许外部在预览消费同次点击时跳过结束回合命令。
        /// </summary>
        internal TurnControlView(
            Button endTurnButton,
            TextMeshProUGUI failToastText,
            CanvasGroup failToastGroup,
            ITurnContext context,
            Func<bool> shouldSuppressEndTurn)
            : this(endTurnButton, failToastText, failToastGroup, context, ScheduleHideToastAsync, shouldSuppressEndTurn)
        {
        }

        /// <summary>
        /// 创建回合控制视图，并允许测试替换 toast 延迟调度。
        /// </summary>
        internal TurnControlView(
            Button endTurnButton,
            TextMeshProUGUI failToastText,
            CanvasGroup failToastGroup,
            ITurnContext context,
            Action<TurnControlView, long, int> scheduleHideToast,
            Func<bool> shouldSuppressEndTurn = null)
        {
            _endTurnButton = endTurnButton;
            _failToastText = failToastText;
            _failToastGroup = failToastGroup;
            _failToastRoot = failToastGroup != null ? failToastGroup.gameObject : failToastText != null ? failToastText.gameObject : null;
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _scheduleHideToast = scheduleHideToast ?? ScheduleHideToastAsync;
            _shouldSuppressEndTurn = shouldSuppressEndTurn;

            if (_endTurnButton != null)
            {
                _endTurnButton.onClick.AddListener(OnEndTurnClicked);
            }

            _onPhaseChanged = OnPhaseChanged;
            _onCardPlayFailed = OnCardPlayFailed;
            _context.Phase.Changed += _onPhaseChanged;
            _context.CardPlayFailed += _onCardPlayFailed;

            OnPhaseChanged(_context.Phase.Value);
            SetToastVisible(false);
        }

        /// <summary>
        /// 供测试或外部调度推进 toast 隐藏版本。
        /// </summary>
        public void HideToastIfVersionMatches(long version)
        {
            if (!_disposed && version == _toastVersion)
            {
                SetToastVisible(false);
            }
        }

        /// <summary>
        /// 外部直接显示出牌失败提示。
        /// </summary>
        public void ShowCardPlayFailed(string reason)
        {
            OnCardPlayFailed(reason);
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
            _toastVersion++;
            if (_endTurnButton != null)
            {
                _endTurnButton.onClick.RemoveListener(OnEndTurnClicked);
            }

            _context.Phase.Changed -= _onPhaseChanged;
            _context.CardPlayFailed -= _onCardPlayFailed;
            _endTurnButton = null;
            _failToastText = null;
            _failToastGroup = null;
            _failToastRoot = null;
        }

        private void OnEndTurnClicked()
        {
            if (_shouldSuppressEndTurn != null && _shouldSuppressEndTurn())
            {
                return;
            }

            _context.EndTurn();
        }

        private void OnPhaseChanged(BattlePhase phase)
        {
            if (!_disposed && _endTurnButton != null)
            {
                _endTurnButton.interactable = phase == BattlePhase.PlayerTurn;
            }
        }

        private void OnCardPlayFailed(string reason)
        {
            if (_disposed)
            {
                return;
            }

            UguiViewUtil.SetText(_failToastText, MapReasonToZh(reason));
            SetToastVisible(true);
            long version = ++_toastVersion;
            _scheduleHideToast(this, version, ToastDurationMs);
        }

        private void SetToastVisible(bool visible)
        {
            UguiViewUtil.SetVisible(_failToastRoot, _failToastGroup, visible);
        }

        private static void ScheduleHideToastAsync(TurnControlView view, long version, int delayMs)
        {
            view.HideToastAfterDelayAsync(version, delayMs).Forget();
        }

        private async UniTaskVoid HideToastAfterDelayAsync(long version, int delayMs)
        {
            await UniTask.Delay(delayMs);
            HideToastIfVersionMatches(version);
        }

        private static string MapReasonToZh(string reason)
        {
            return reason switch
            {
                "InsufficientEnergy" => "能量不足",
                "NotPlayerTurn" => "现在不是你的回合",
                "InvalidTarget" => "无效目标",
                "InvalidHandIndex" => "卡牌索引错误",
                _ => "出牌失败",
            };
        }
    }
}
