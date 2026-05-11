namespace GameLogic
{
    /// <summary>
    /// 拖拽控制器 → 上层（HandFanView / BattlePanelView）的回调接口。
    /// 三个事件覆盖单击 / 拖到 drop-zone / 取消三种结局，上层据此决定后续动作（出牌、SelectingTarget、回弹等）。
    /// </summary>
    public interface IDragHostCallbacks
    {
        /// <summary>
        /// 单击（位移 ≤ DragThreshold）某卡的 PointerUp 时触发。上层通常切换预览态。
        /// </summary>
        void CardClicked(int handIdx);

        /// <summary>
        /// 拖到 drop-zone 内松手时触发。
        /// needsManualTarget = true（SingleManual 卡）时上层 SHALL 进入 SelectingTarget 流程；
        /// false 时上层 SHALL 直接调 IHandContext.UseCard(handIdx)。
        /// </summary>
        void CardDroppedOnZone(int handIdx, bool needsManualTarget);

        /// <summary>
        /// 拖拽取消（中间地带松手 / PointerCaptureOut 中途丢失）时触发。
        /// 上层通常无操作（rebound 已由 controller 内部完成）。
        /// </summary>
        void CardDragCancelled(int handIdx);
    }
}
