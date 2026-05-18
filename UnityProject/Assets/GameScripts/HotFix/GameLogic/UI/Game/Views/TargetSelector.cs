using System;
using EF.Debugger;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic
{
    /// <summary>
    /// UGUI 手动目标选择协调器，负责怪物项高亮、确认和取消回弹。
    /// </summary>
    public sealed class TargetSelector : IDisposable
    {
        private readonly MonsterListView _monsterListView;
        private readonly HandFanView _handFanView;
        private readonly ITargetContext _context;
        private readonly Button _cancelButton;
        private Action<BattlePhase> _onPhaseChanged;
        private int _selectedHandIdx = -1;
        private bool _disposed;

        /// <summary>
        /// 当前是否处于目标选择态。
        /// </summary>
        public bool IsActive { get; private set; }

        /// <summary>
        /// 创建目标选择器。
        /// </summary>
        public TargetSelector(MonsterListView monsterListView, HandFanView handFanView, ITargetContext context, Button cancelButton)
        {
            _monsterListView = monsterListView ?? throw new ArgumentNullException(nameof(monsterListView));
            _handFanView = handFanView ?? throw new ArgumentNullException(nameof(handFanView));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _cancelButton = cancelButton;

            if (_cancelButton != null)
            {
                _cancelButton.onClick.AddListener(Cancel);
            }

            _onPhaseChanged = OnPhaseChanged;
            _context.Phase.Changed += _onPhaseChanged;
        }

        /// <summary>
        /// 进入目标选择态。
        /// </summary>
        public void Enter(int handIdx)
        {
            if (_disposed)
            {
                return;
            }

            if (IsActive)
            {
                Log.Warning($"[TargetSelector] 已经处于目标选择态，忽略 Enter({handIdx})。");
                return;
            }

            IsActive = true;
            _selectedHandIdx = handIdx;
            _monsterListView.EnterTargetMode(OnMonsterClicked);
        }

        /// <summary>
        /// 取消目标选择并触发 ghost 回弹。
        /// </summary>
        public void Cancel()
        {
            if (_disposed || !IsActive)
            {
                return;
            }

            int handIdx = _selectedHandIdx;
            Exit();
            _handFanView.RequestGhostRebound(handIdx);
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

            if (IsActive)
            {
                Cancel();
            }

            _disposed = true;
            if (_cancelButton != null)
            {
                _cancelButton.onClick.RemoveListener(Cancel);
            }

            _context.Phase.Changed -= _onPhaseChanged;
        }

        private void OnMonsterClicked(int monsterIndex)
        {
            if (_disposed || !IsActive)
            {
                return;
            }

            int handIdx = _selectedHandIdx;
            Exit();
            _context.UseCardOnMonster(handIdx, monsterIndex);
            _handFanView.RequestGhostCleanup();
        }

        private void OnPhaseChanged(BattlePhase phase)
        {
            if (IsActive && phase != BattlePhase.PlayerTurn)
            {
                Cancel();
            }
        }

        private void Exit()
        {
            _monsterListView.ExitTargetMode();
            IsActive = false;
            _selectedHandIdx = -1;
        }
    }
}
