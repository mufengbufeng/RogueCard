using System;
using EF.Debugger;
using GameConfig.card;
using UnityEngine;

namespace GameLogic
{
    /// <summary>
    /// 手牌拖拽四态状态机控制器。维护 CardInteractionState（Idle/Hovering/Previewing/Dragging/SelectingTarget）
    /// 与 DragMode（Detached/InsertSlot/OverDropZone）。所有 UI 副作用通过 IDragSurface 间接完成，便于 mock 测试。
    /// 状态转移规则见 spec gameview-card-drag-state-machine。
    /// </summary>
    public sealed class CardDragController : IDisposable
    {
        private readonly IDragSurface _surface;
        private readonly IHandContext _context;
        private readonly HandFanLayoutOptions _options;

        // ── 状态字段 ──
        private CardInteractionState _state = CardInteractionState.Idle;
        private DragMode _dragMode = DragMode.Detached;
        private int _activeVisualIndex = -1;
        private int _activeHandIndex = -1;
        private int _insertSlotIndex = -1;
        private Vector2 _pointerStartPos;
        private int _capturedPointerId = -1;
        private bool _disposed;

        /// <summary>当前状态机状态（只读，HandFanView 可查询是否处于 Dragging 等）。</summary>
        public CardInteractionState State => _state;

        /// <summary>当前 Drag 子态（仅 State == Dragging 时有效）。</summary>
        public DragMode DragMode => _dragMode;

        /// <summary>当前被拖卡在 Hand 列表中的索引（闭包语义，reorder 不变）。-1 表示无拖拽。</summary>
        public int ActiveHandIndex => _activeHandIndex;

        /// <summary>当前被拖卡的视觉索引（reorder 后会变）。</summary>
        public int ActiveVisualIndex => _activeVisualIndex;

        /// <summary>构造拖拽控制器。</summary>
        public CardDragController(IDragSurface surface, IHandContext context, HandFanLayoutOptions options)
        {
            _surface = surface ?? throw new ArgumentNullException(nameof(surface));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _options = options ?? new HandFanLayoutOptions();
        }

        // ── 入口事件 ──

        /// <summary>
        /// 处理某张卡的 PointerDown。仅 Phase==PlayerTurn 才接受；记录起点供 DragThreshold 比较，
        /// 并通过 IDragSurface 捕获 pointer。重复按下（已有 capture）忽略。
        /// </summary>
        public void OnPointerDown(int handIdx, int visualIdx, int pointerId, Vector2 pos)
        {
            if (_disposed) return;
            if (_context.Phase.Value != BattlePhase.PlayerTurn) return;
            if (_capturedPointerId >= 0) return;

            _pointerStartPos = pos;
            _capturedPointerId = pointerId;
            _activeHandIndex = handIdx;
            _activeVisualIndex = visualIdx;

            _surface.CapturePointer(visualIdx, pointerId);
        }

        /// <summary>
        /// 处理 PointerMove。未拖拽时检查 DragThreshold；超过则进入 Dragging。
        /// 拖拽中实时更新 ghost 位置与子态（InsertSlot / OverDropZone / Detached）。
        /// </summary>
        public void OnPointerMove(int pointerId, Vector2 pos)
        {
            if (_disposed) return;
            if (_capturedPointerId < 0 || _capturedPointerId != pointerId) return;

            if (_state != CardInteractionState.Dragging)
            {
                if (Vector2.Distance(pos, _pointerStartPos) > _options.DragThreshold)
                {
                    EnterDragging(pos);
                }
            }
            else
            {
                _surface.UpdateGhostPosition(pos);
                UpdateDragSubMode(pos);
            }
        }

        /// <summary>
        /// 处理 PointerUp。按当前态分发：
        /// - 未进入 Dragging（位移 ≤ DragThreshold） → Callbacks.CardClicked + 转 Idle
        /// - Dragging.OverDropZone → 销毁 ghost（除非 SingleManual）+ Callbacks.CardDroppedOnZone(handIdx, needsManualTarget) + 转 Idle
        /// - Dragging.InsertSlot → 调 ReorderCardItem + ExitDragging + 转 Idle
        /// - Dragging.Detached → StartReboundAnimation + Callbacks.CardDragCancelled
        /// </summary>
        public void OnPointerUp(int pointerId, Vector2 pos)
        {
            if (_disposed) return;
            if (_capturedPointerId < 0 || _capturedPointerId != pointerId) return;

            int handIdx = _activeHandIndex;
            int visualIdx = _activeVisualIndex;

            if (_state == CardInteractionState.Dragging)
            {
                switch (_dragMode)
                {
                    case DragMode.OverDropZone:
                    {
                        var card = GetHandCardAt(handIdx);
                        bool needsManualTarget = card != null
                            && card.Config != null
                            && card.Config.TargetMode == TargetMode.SingleManual;

                        if (needsManualTarget)
                        {
                            // 保留 ghost：所有权转移到上层 TargetSelector（change 3）
                            // 不调 ExitDragging（保留 ghost），但仍需复位 inline transitionDuration / opacity / picking
                            ReleasePointerCapture(visualIdx, pointerId);
                            _surface.SetDropZoneActive(false);
                            // 通知上层 + 状态机内部归位（ghost 由上层接管）
                            _surface.Callbacks?.CardDroppedOnZone(handIdx, true);
                            ResetStateAfterDrop();
                            break;
                        }

                        ExitDragging();
                        ReleasePointerCapture(visualIdx, pointerId);
                        _surface.Callbacks?.CardDroppedOnZone(handIdx, false);
                        ResetStateAfterDrop();
                        break;
                    }
                    case DragMode.InsertSlot:
                    {
                        int from = _activeVisualIndex;
                        int to = _insertSlotIndex;
                        _surface.ReorderCardItem(from, to);
                        ExitDragging();
                        ReleasePointerCapture(visualIdx, pointerId);
                        ResetStateAfterDrop();
                        break;
                    }
                    case DragMode.Detached:
                    default:
                    {
                        // 中间地带：协同回弹动画 + 通知上层 cancel
                        ReleasePointerCapture(visualIdx, pointerId);
                        StartReboundAnimation();
                        // CardDragCancelled 在 rebound 完成时间内仍是有效的"该次拖拽未出牌"语义
                        _surface.Callbacks?.CardDragCancelled(handIdx);
                        break;
                    }
                }
            }
            else
            {
                // 未进入 Dragging → 单击
                ReleasePointerCapture(visualIdx, pointerId);
                _activeHandIndex = -1;
                _activeVisualIndex = -1;
                _capturedPointerId = -1;
                _surface.Callbacks?.CardClicked(handIdx);
            }
        }

        /// <summary>
        /// PointerCapture 中途丢失（系统抢走指针、卡 detach 等）。强制清理：销毁 ghost / 占位卡，
        /// 还原所有卡的 opacity / pickingMode / inline transitionDuration，状态归 Idle。
        /// </summary>
        public void OnPointerCaptureOut(int pointerId)
        {
            if (_disposed) return;
            if (_capturedPointerId < 0 || _capturedPointerId != pointerId) return;

            Log.Warning("[CardDragController] PointerCapture 中途丢失，强制重置交互态");

            int handIdx = _activeHandIndex;

            if (_state == CardInteractionState.Dragging)
            {
                ExitDragging();
            }

            _capturedPointerId = -1;
            _activeHandIndex = -1;
            _activeVisualIndex = -1;
            _state = CardInteractionState.Idle;

            if (handIdx >= 0)
            {
                _surface.Callbacks?.CardDragCancelled(handIdx);
            }
        }

        /// <summary>
        /// 外部触发的协同回弹（由 HandFanView.RequestGhostRebound 调用，typically 来自 TargetSelector.Cancel）。
        /// 复用 StartReboundAnimation：立即销毁 ghost、被拖卡 opacity 恢复、其他卡 transition 0.15s 回到 N 张布局，
        /// ReboundDurationMs 后 ExitDragging 完成清理。
        /// 适用场景：SingleManual 卡 dropped on zone 后 ghost 仍在屏幕上，TargetSelector 取消 → 需触发协同回弹。
        /// 调用时控制器状态可能为 Idle（内部会暂时切到 Dragging 让回弹流程跑完）。
        /// </summary>
        /// <param name="visualIdx">被拖卡当前在 _cardItems 中的视觉索引（HandFanView 通过 handIdx 查得）。</param>
        public void BeginExternalRebound(int visualIdx)
        {
            if (_disposed) return;
            if (visualIdx < 0 || visualIdx >= _surface.CardCount)
            {
                Log.Warning($"[CardDragController] BeginExternalRebound 收到无效 visualIdx={visualIdx}，忽略");
                return;
            }

            // 暂时切到 Dragging 态让 StartReboundAnimation 内部的状态判断与 schedule 回调正确执行
            _state = CardInteractionState.Dragging;
            _activeVisualIndex = visualIdx;
            _dragMode = DragMode.Detached;
            _insertSlotIndex = -1;

            StartReboundAnimation();
        }

        /// <summary>
        /// 外部触发的 ghost 立即销毁（由 HandFanView.RequestGhostCleanup 调用，typically 来自 TargetSelector 确认怪物点击）。
        /// 不触发回弹动画——仅销毁 ghost；卡牌 visual state 由后续 Hand.Changed 触发 RefreshCards 自然重建。
        /// </summary>
        public void RequestGhostCleanup()
        {
            if (_disposed) return;
            _surface.DestroyGhost();
        }

        /// <summary>
        /// hand-fan 几何变化（首次 layout / resize）时重排所有卡。
        /// 拖拽中按当前 _dragMode 分发，否则按 N 张紧凑布局。
        /// </summary>
        public void OnGeometryChanged()
        {
            if (_disposed) return;

            if (_state == CardInteractionState.Dragging && _activeVisualIndex >= 0)
            {
                RecomputeFanLayout(_activeVisualIndex, _dragMode, _insertSlotIndex);
            }
            else
            {
                RecomputeFanLayout(-1, DragMode.Detached, -1);
            }
        }

        // ── Dragging 态进出 ──

        private void EnterDragging(Vector2 pointerPos)
        {
            _state = CardInteractionState.Dragging;
            _dragMode = DragMode.Detached;
            _insertSlotIndex = -1;

            int activeIdx = _activeVisualIndex;
            int n = _surface.CardCount;

            // 拖拽中其他卡的 transform 变更必须立即生效（无 transition），用 inline style 而非 USS class
            for (int i = 0; i < n; i++)
            {
                _surface.SetCardTransitionDuration(i, 0f);
            }

            // 被拖卡用 opacity 0 而非 visibility Hidden（避免 layout 重算 + 1 帧时序错位）
            if (activeIdx >= 0 && activeIdx < n)
            {
                _surface.SetCardOpacity(activeIdx, 0f);
                _surface.SetCardPickingMode(activeIdx, false);
            }

            // 创建 ghost
            _surface.CreateGhost(activeIdx, pointerPos);

            // 打开 drop-zone 高亮
            _surface.SetDropZoneActive(true);

            // 剩余 N-1 张按 N-1 紧凑布局
            RecomputeFanLayout(activeIdx, DragMode.Detached, -1);
        }

        private void ExitDragging()
        {
            _surface.DestroyGhost();
            _surface.DestroyInsertSlot();

            int n = _surface.CardCount;
            for (int i = 0; i < n; i++)
            {
                _surface.ResetCardOpacity(i);
                _surface.SetCardPickingMode(i, true);
                _surface.ClearCardTransitionDuration(i);
            }

            _dragMode = DragMode.Detached;
            _insertSlotIndex = -1;

            _surface.SetDropZoneActive(false);
        }

        /// <summary>
        /// 中间地带松手回弹。ghost 立即销毁、被拖卡 opacity 立即恢复、其他卡 transition 0.15s 平滑回 N 张布局，
        /// ReboundDurationMs 后调 ExitDragging 完成清理。
        /// </summary>
        private void StartReboundAnimation()
        {
            // 立即销毁 ghost：避免与扇形卡牌 rotate 视觉错位
            _surface.DestroyGhost();

            // 若还在 InsertSlot 子态，先清掉占位卡（不动 _cardItems 顺序）
            if (_dragMode == DragMode.InsertSlot)
            {
                _surface.DestroyInsertSlot();
                _dragMode = DragMode.Detached;
                _insertSlotIndex = -1;
            }

            int n = _surface.CardCount;
            int activeIdx = _activeVisualIndex;

            // 被拖卡立即恢复 opacity（此时 transitionDuration 仍为 0s，立即生效不 fade）
            if (activeIdx >= 0 && activeIdx < n)
            {
                _surface.SetCardOpacity(activeIdx, 1f);
                _surface.SetCardPickingMode(activeIdx, true);
            }

            // 启用回弹过渡：0.15s 平滑回 N 张布局
            for (int i = 0; i < n; i++)
            {
                _surface.SetCardTransitionDuration(i, 0.15f);
            }

            RecomputeFanLayout(-1, DragMode.Detached, -1);

            _surface.Schedule(() =>
            {
                if (_disposed) return;
                ExitDragging();
                ResetStateAfterDrop();
                Log.Info("[CardDragController] 卡牌拖拽回弹完成");
            }, _options.ReboundDurationMs);
        }

        /// <summary>松手清理后状态机归 Idle 并清空索引/捕获字段（不动 ghost / 占位卡，那由 ExitDragging 处理）。</summary>
        private void ResetStateAfterDrop()
        {
            _state = CardInteractionState.Idle;
            _activeVisualIndex = -1;
            _activeHandIndex = -1;
            _capturedPointerId = -1;
        }

        private void ReleasePointerCapture(int visualIdx, int pointerId)
        {
            if (visualIdx < 0) return;
            _surface.ReleasePointer(visualIdx, pointerId);
        }

        // ── DragMode 子态切换 ──

        /// <summary>按指针位置判定子态，按需进入 / 退出 InsertSlot 模式。优先级 OverDropZone > InsertSlot > Detached。</summary>
        private void UpdateDragSubMode(Vector2 pointerPos)
        {
            DragMode newMode = DetermineDragMode(pointerPos);
            DragMode oldMode = _dragMode;

            if (newMode == DragMode.InsertSlot)
            {
                int slot = ComputeInsertSlot(pointerPos);
                if (oldMode != DragMode.InsertSlot)
                {
                    EnterInsertSlotMode(slot);
                }
                else
                {
                    UpdateInsertSlot(slot);
                }
            }
            else
            {
                if (oldMode == DragMode.InsertSlot)
                {
                    ExitInsertSlotMode();
                }
                _dragMode = newMode;
            }
        }

        /// <summary>按 worldBound 命中决定子态。</summary>
        private DragMode DetermineDragMode(Vector2 pointerPos)
        {
            if (_surface.DropZoneAvailable && _surface.DropZoneWorldBound.Contains(pointerPos))
                return DragMode.OverDropZone;
            if (_surface.HandFanWorldBound.Contains(pointerPos))
                return DragMode.InsertSlot;
            return DragMode.Detached;
        }

        private void EnterInsertSlotMode(int insertSlot)
        {
            _dragMode = DragMode.InsertSlot;
            _insertSlotIndex = insertSlot;
            _surface.CreateInsertSlot(_activeVisualIndex);
            RecomputeFanLayout(_activeVisualIndex, DragMode.InsertSlot, _insertSlotIndex);
        }

        private void UpdateInsertSlot(int newInsertSlot)
        {
            if (newInsertSlot == _insertSlotIndex) return;
            _insertSlotIndex = newInsertSlot;
            RecomputeFanLayout(_activeVisualIndex, DragMode.InsertSlot, _insertSlotIndex);
        }

        private void ExitInsertSlotMode()
        {
            _surface.DestroyInsertSlot();
            _dragMode = DragMode.Detached;
            _insertSlotIndex = -1;
            RecomputeFanLayout(_activeVisualIndex, DragMode.Detached, -1);
        }

        /// <summary>
        /// 取剩余 N-1 张卡的 worldBound + 视觉索引列表，调 FanLayoutCalc.ComputeInsertSlot。
        /// 总卡数 ≤ 1 直接返回 0。
        /// </summary>
        private int ComputeInsertSlot(Vector2 pointerPos)
        {
            int n = _surface.CardCount;
            if (n <= 1) return 0;
            if (_activeVisualIndex < 0 || _activeVisualIndex >= n) return 0;

            var bounds = new Rect[n - 1];
            var indices = new int[n - 1];
            int j = 0;
            for (int i = 0; i < n; i++)
            {
                if (i == _activeVisualIndex) continue;
                bounds[j] = _surface.GetCardWorldBound(i);
                indices[j] = i;
                j++;
            }
            return FanLayoutCalc.ComputeInsertSlot(pointerPos, bounds, indices);
        }

        // ── 布局计算 ──

        /// <summary>
        /// 按当前态分发槽位给所有卡 + 占位卡，计算扇形 transform 后通过 IDragSurface 应用。
        /// activeIdx=-1 表示无拖拽（全部 N 张紧凑）；mode==InsertSlot 时跳过 insertSlot 槽并应用占位卡 transform。
        /// </summary>
        private void RecomputeFanLayout(int activeIdx, DragMode mode, int insertSlot)
        {
            int n = _surface.CardCount;
            if (n == 0) return;

            float fanW = _surface.HandFanWidth;
            float fanH = _surface.HandFanHeight;

            // 无拖拽：全部按 N 张紧凑布局
            if (activeIdx < 0)
            {
                for (int i = 0; i < n; i++)
                {
                    var slot = FanLayoutCalc.ComputeSlot(i, n, fanW, fanH, _options);
                    _surface.ApplyFanTransform(i, slot);
                }
                return;
            }

            int slotCount = mode == DragMode.InsertSlot ? n : Mathf.Max(1, n - 1);
            int linearPos = 0;
            for (int i = 0; i < n; i++)
            {
                if (i == activeIdx) continue;

                int slotIdx;
                if (mode == DragMode.InsertSlot)
                {
                    slotIdx = linearPos < insertSlot ? linearPos : linearPos + 1;
                }
                else
                {
                    slotIdx = linearPos;
                }

                var slot = FanLayoutCalc.ComputeSlot(slotIdx, slotCount, fanW, fanH, _options);
                _surface.ApplyFanTransform(i, slot);
                linearPos++;
            }

            if (mode == DragMode.InsertSlot && insertSlot >= 0)
            {
                var insertSlotAssignment = FanLayoutCalc.ComputeSlot(insertSlot, slotCount, fanW, fanH, _options);
                _surface.ApplyInsertSlotTransform(insertSlotAssignment);
            }
        }

        private CardRuntime GetHandCardAt(int handIdx)
        {
            var hand = _context?.Hand?.Value;
            if (hand == null) return null;
            if (handIdx < 0 || handIdx >= hand.Count) return null;
            return hand[handIdx];
        }

        // ── 释放 ──

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_state == CardInteractionState.Dragging)
            {
                ExitDragging();
            }
            _state = CardInteractionState.Idle;
            _activeHandIndex = -1;
            _activeVisualIndex = -1;
            _capturedPointerId = -1;
        }
    }
}
