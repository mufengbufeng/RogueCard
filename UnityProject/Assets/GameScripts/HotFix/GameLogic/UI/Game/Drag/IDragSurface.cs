using System;
using UnityEngine;

namespace GameLogic
{
    /// <summary>
    /// 拖拽控制器与 UI 副作用的边界。CardDragController 通过此接口操作 ghost、占位卡、卡牌 transform、
    /// pointer capture、worldBound 命中等所有 UI 行为，便于在测试中用 MockDragSurface 记录调用序列。
    /// 生产实现位于 HandFanView 内部，封装 _handFan / _dropZone / _previewLayer / _cardItems 引用。
    /// </summary>
    public interface IDragSurface
    {
        // ── 卡牌集合操作 ──

        /// <summary>当前手牌视觉项数。</summary>
        int CardCount { get; }

        /// <summary>取第 cardIdx 张卡的 worldBound（命中检测、insertSlot 计算用）。</summary>
        Rect GetCardWorldBound(int cardIdx);

        /// <summary>把扇形布局结果应用到第 cardIdx 张卡的 inline style（left/top/translate/rotate）。</summary>
        void ApplyFanTransform(int cardIdx, FanSlotAssignment slot);

        /// <summary>设置第 cardIdx 张卡的 opacity（0=隐藏、1=完全可见、null=恢复 USS 默认）。</summary>
        void SetCardOpacity(int cardIdx, float opacity);

        /// <summary>
        /// 重置第 cardIdx 张卡的 opacity 为 USS 默认值（StyleKeyword.Null 等价）。
        /// 单独提供方法以便 mock 区分"重置"与"显式设 0/1"调用。
        /// </summary>
        void ResetCardOpacity(int cardIdx);

        /// <summary>设置第 cardIdx 张卡的 pickingMode（true=Position 可点、false=Ignore 不抢点击）。</summary>
        void SetCardPickingMode(int cardIdx, bool pickable);

        /// <summary>设置第 cardIdx 张卡的 inline transitionDuration（秒）。0=立即生效，0.15=回弹动画。</summary>
        void SetCardTransitionDuration(int cardIdx, float seconds);

        /// <summary>清除第 cardIdx 张卡的 inline transitionDuration，让 USS 默认生效。</summary>
        void ClearCardTransitionDuration(int cardIdx);

        /// <summary>调整 _cardItems 列表顺序：把 from 位置的卡移到 to 位置（仅 UI 层）。</summary>
        void ReorderCardItem(int from, int to);

        /// <summary>按 _cardItems 列表顺序同步 hand-fan sibling 顺序（c0 在底、c[N-1] 在顶）。</summary>
        void SyncSiblingOrder();

        // ── Drop zone & hand fan 几何 ──

        /// <summary>drop-zone 的 worldBound（命中检测）。</summary>
        Rect DropZoneWorldBound { get; }

        /// <summary>hand-fan 的 worldBound（命中检测）。</summary>
        Rect HandFanWorldBound { get; }

        /// <summary>drop-zone 是否就绪（null 时为 false，即不进入 OverDropZone 子态）。</summary>
        bool DropZoneAvailable { get; }

        /// <summary>切换 drop-zone 的 active 类（拖拽中显示高亮边框）。</summary>
        void SetDropZoneActive(bool active);

        /// <summary>hand-fan 容器尺寸（resolvedStyle width，未就绪时回退到 layout/默认）。</summary>
        float HandFanWidth { get; }

        /// <summary>hand-fan 容器尺寸（resolvedStyle height）。</summary>
        float HandFanHeight { get; }

        // ── Ghost 与占位卡 ──

        /// <summary>创建 ghost（半透明拖拽副本），加入 preview-layer。</summary>
        void CreateGhost(int sourceCardIdx, Vector2 pos);

        /// <summary>移动 ghost 跟随指针。</summary>
        void UpdateGhostPosition(Vector2 pos);

        /// <summary>销毁 ghost。幂等，重复调用不抛异常。</summary>
        void DestroyGhost();

        /// <summary>创建 InsertSlot 占位卡，加入 hand-fan，复制 source 卡的 name/cost 文本。</summary>
        void CreateInsertSlot(int sourceCardIdx);

        /// <summary>销毁 InsertSlot 占位卡。幂等。</summary>
        void DestroyInsertSlot();

        /// <summary>把扇形布局结果应用到占位卡的 inline style。仅 InsertSlot 子态有占位卡时才有效。</summary>
        void ApplyInsertSlotTransform(FanSlotAssignment slot);

        // ── 调度（用于 ReboundDurationMs 延迟回弹）──

        /// <summary>延迟 delayMs 毫秒后执行 action。生产用 VisualElement.schedule.Execute。</summary>
        void Schedule(Action action, long delayMs);

        // ── PointerCapture ──

        /// <summary>第 cardIdx 张卡捕获 pointerId 的指针。</summary>
        void CapturePointer(int cardIdx, int pointerId);

        /// <summary>第 cardIdx 张卡释放 pointerId 的指针。幂等。</summary>
        void ReleasePointer(int cardIdx, int pointerId);

        // ── 上层回调 ──

        /// <summary>拖拽控制器抛事件给 HandFanView 的桥。</summary>
        IDragHostCallbacks Callbacks { get; }
    }
}
