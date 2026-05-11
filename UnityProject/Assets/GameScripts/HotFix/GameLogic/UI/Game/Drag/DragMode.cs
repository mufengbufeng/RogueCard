namespace GameLogic
{
    /// <summary>
    /// 拖拽态下的子模式（仅 CardInteractionState == Dragging 时有效）。
    /// </summary>
    public enum DragMode
    {
        /// <summary>中间地带：被拖卡脱离扇形，剩余卡按 N-1 紧凑排。</summary>
        Detached,

        /// <summary>hand-fan 内：留出一个空槽 + 半透明占位卡，剩余卡按 N 槽排。</summary>
        InsertSlot,

        /// <summary>drop-zone 内：等同 Detached 布局，但松手会出牌。</summary>
        OverDropZone,
    }
}
