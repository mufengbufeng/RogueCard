namespace GameLogic
{
    /// <summary>
    /// 手牌交互状态。从 GameView 私有 enum 提升到独立文件，供 CardDragController 与上层共享。
    /// </summary>
    public enum CardInteractionState
    {
        /// <summary>空闲态。</summary>
        Idle,

        /// <summary>悬停态（鼠标悬于某卡，含 card-item--hovering 类）。</summary>
        Hovering,

        /// <summary>预览态（单击放大克隆卡到 preview-layer）。</summary>
        Previewing,

        /// <summary>拖拽态（PointerDown + 越过 DragThreshold 后进入）。</summary>
        Dragging,

        /// <summary>SingleManual 卡释放在 drop-zone 后，等玩家点选具体怪物（GameView/TargetSelector 维护）。</summary>
        SelectingTarget,
    }
}
