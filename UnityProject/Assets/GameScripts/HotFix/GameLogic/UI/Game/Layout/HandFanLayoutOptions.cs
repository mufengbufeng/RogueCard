namespace GameLogic
{
    /// <summary>
    /// 手牌扇形布局与拖拽手感参数集合。所有字段为可写 POCO，便于测试注入不同值验证边界。
    /// 默认值与 GameView 现有常量一致（DragThreshold=10、MaxCardSpacing=120、RotatePerStep=3°、
    /// TranslateYCoeff=3.5、CardWidth=150、CardHeight=230、HandFanBottomPadding=20、ReboundDurationMs=160）。
    /// </summary>
    public sealed class HandFanLayoutOptions
    {
        /// <summary>越过此位移才视为拖拽（PointerMove 中比较 PointerDown 起点）。</summary>
        public float DragThreshold { get; set; } = 10f;

        /// <summary>相邻卡水平间距上限（防止 hand-fan 过宽时卡牌散开）。</summary>
        public float MaxCardSpacing { get; set; } = 120f;

        /// <summary>每张卡相对中心旋转角度（度）。</summary>
        public float RotatePerStep { get; set; } = 3f;

        /// <summary>抛物线下沉系数（offset² × 此值 = TranslateY）。</summary>
        public float TranslateYCoeff { get; set; } = 3.5f;

        /// <summary>卡牌宽度（像素）。</summary>
        public float CardWidth { get; set; } = 150f;

        /// <summary>卡牌高度（像素）。</summary>
        public float CardHeight { get; set; } = 230f;

        /// <summary>卡牌底边距 hand-fan 底部留白。</summary>
        public float HandFanBottomPadding { get; set; } = 20f;

        /// <summary>回弹动画时长（略大于 USS transition 0.15s，单位 ms）。</summary>
        public long ReboundDurationMs { get; set; } = 160L;
    }
}
