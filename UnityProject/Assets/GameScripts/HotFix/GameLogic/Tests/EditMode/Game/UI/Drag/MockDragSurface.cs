using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameLogic.Tests
{
    /// <summary>
    /// 测试用 IDragSurface 实现：记录所有方法调用日志，并暴露可断言的辅助属性。
    /// 通过 SetCardWorldBound / SetDropZoneWorldBound / SetHandFanWorldBound / SetCardCount 配置场景。
    /// 调度（Schedule）保留在 _scheduledActions 列表中，测试通过 FlushScheduled() 立即触发。
    /// </summary>
    public class MockDragSurface : IDragSurface
    {
        // ── 配置（测试在 SetUp / 用例中设置）──
        public int ConfiguredCardCount = 0;
        public Rect[] CardBounds = Array.Empty<Rect>();
        public Rect ConfiguredDropZoneBound = new Rect(500f, 500f, 100f, 100f);
        public Rect ConfiguredHandFanBound = new Rect(0f, 400f, 800f, 200f);
        public bool ConfiguredDropZoneAvailable = true;
        public float ConfiguredHandFanWidth = 800f;
        public float ConfiguredHandFanHeight = 280f;

        public IDragHostCallbacks Callbacks { get; }

        public MockDragSurface(IDragHostCallbacks callbacks)
        {
            Callbacks = callbacks;
        }

        // ── 调用日志 ──
        public readonly List<(int cardIdx, FanSlotAssignment slot)> ApplyFanTransformLog = new();
        public readonly List<(int cardIdx, float opacity)> OpacityCallLog = new();
        public readonly List<int> ResetOpacityCallLog = new();
        public readonly List<(int cardIdx, bool pickable)> PickingModeCallLog = new();
        public readonly List<(int cardIdx, float seconds)> TransitionDurationCallLog = new();
        public readonly List<int> ClearTransitionDurationCallLog = new();
        public readonly List<(int from, int to)> ReorderCallLog = new();
        public int SyncSiblingOrderCallCount;
        public readonly List<bool> DropZoneActiveCallLog = new();

        public readonly List<(int srcIdx, Vector2 pos)> CreateGhostCallLog = new();
        public readonly List<Vector2> UpdateGhostPositionCallLog = new();
        public int DestroyGhostCallCount;

        public readonly List<int> CreateInsertSlotCallLog = new();
        public int DestroyInsertSlotCallCount;
        public readonly List<FanSlotAssignment> ApplyInsertSlotTransformLog = new();

        public readonly List<(int cardIdx, int pointerId)> CapturePointerCallLog = new();
        public readonly List<(int cardIdx, int pointerId)> ReleasePointerCallLog = new();

        // 调度任务（测试通过 FlushScheduled 立即执行）
        private readonly List<(Action action, long delayMs)> _scheduledActions = new();
        public IReadOnlyList<(Action action, long delayMs)> ScheduledActions => _scheduledActions;

        // 视觉态（用于 ghost 状态查询）
        public bool GhostExists { get; private set; }
        public bool InsertSlotExists { get; private set; }

        // ── IDragSurface 实现 ──

        public int CardCount => ConfiguredCardCount;

        public Rect GetCardWorldBound(int cardIdx)
        {
            if (cardIdx < 0 || cardIdx >= CardBounds.Length) return Rect.zero;
            return CardBounds[cardIdx];
        }

        public void ApplyFanTransform(int cardIdx, FanSlotAssignment slot)
        {
            ApplyFanTransformLog.Add((cardIdx, slot));
        }

        public void SetCardOpacity(int cardIdx, float opacity)
        {
            OpacityCallLog.Add((cardIdx, opacity));
        }

        public void ResetCardOpacity(int cardIdx)
        {
            ResetOpacityCallLog.Add(cardIdx);
        }

        public void SetCardPickingMode(int cardIdx, bool pickable)
        {
            PickingModeCallLog.Add((cardIdx, pickable));
        }

        public void SetCardTransitionDuration(int cardIdx, float seconds)
        {
            TransitionDurationCallLog.Add((cardIdx, seconds));
        }

        public void ClearCardTransitionDuration(int cardIdx)
        {
            ClearTransitionDurationCallLog.Add(cardIdx);
        }

        public void ReorderCardItem(int from, int to)
        {
            ReorderCallLog.Add((from, to));
        }

        public void SyncSiblingOrder()
        {
            SyncSiblingOrderCallCount++;
        }

        public Rect DropZoneWorldBound => ConfiguredDropZoneBound;
        public Rect HandFanWorldBound => ConfiguredHandFanBound;
        public bool DropZoneAvailable => ConfiguredDropZoneAvailable;
        public float HandFanWidth => ConfiguredHandFanWidth;
        public float HandFanHeight => ConfiguredHandFanHeight;

        public void SetDropZoneActive(bool active)
        {
            DropZoneActiveCallLog.Add(active);
        }

        public void CreateGhost(int sourceCardIdx, Vector2 pos)
        {
            CreateGhostCallLog.Add((sourceCardIdx, pos));
            GhostExists = true;
        }

        public void UpdateGhostPosition(Vector2 pos)
        {
            UpdateGhostPositionCallLog.Add(pos);
        }

        public void DestroyGhost()
        {
            DestroyGhostCallCount++;
            GhostExists = false;
        }

        public void CreateInsertSlot(int sourceCardIdx)
        {
            CreateInsertSlotCallLog.Add(sourceCardIdx);
            InsertSlotExists = true;
        }

        public void DestroyInsertSlot()
        {
            DestroyInsertSlotCallCount++;
            InsertSlotExists = false;
        }

        public void ApplyInsertSlotTransform(FanSlotAssignment slot)
        {
            ApplyInsertSlotTransformLog.Add(slot);
        }

        public void Schedule(Action action, long delayMs)
        {
            _scheduledActions.Add((action, delayMs));
        }

        public void CapturePointer(int cardIdx, int pointerId)
        {
            CapturePointerCallLog.Add((cardIdx, pointerId));
        }

        public void ReleasePointer(int cardIdx, int pointerId)
        {
            ReleasePointerCallLog.Add((cardIdx, pointerId));
        }

        // ── 测试辅助 ──

        /// <summary>触发所有已调度任务（按入队顺序）。</summary>
        public void FlushScheduled()
        {
            var snapshot = new List<(Action action, long delayMs)>(_scheduledActions);
            _scheduledActions.Clear();
            foreach (var entry in snapshot)
            {
                entry.action?.Invoke();
            }
        }
    }

    /// <summary>测试用 IDragHostCallbacks，记录三种回调的调用序列。</summary>
    public class CapturingDragHostCallbacks : IDragHostCallbacks
    {
        public readonly List<int> CardClickedLog = new();
        public readonly List<(int handIdx, bool needsManualTarget)> CardDroppedOnZoneLog = new();
        public readonly List<int> CardDragCancelledLog = new();

        public void CardClicked(int handIdx) => CardClickedLog.Add(handIdx);
        public void CardDroppedOnZone(int handIdx, bool needsManualTarget) =>
            CardDroppedOnZoneLog.Add((handIdx, needsManualTarget));
        public void CardDragCancelled(int handIdx) => CardDragCancelledLog.Add(handIdx);
    }
}
