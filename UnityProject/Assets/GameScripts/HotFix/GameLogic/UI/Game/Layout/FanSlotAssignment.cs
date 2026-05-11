namespace GameLogic
{
    /// <summary>
    /// 单卡扇形布局结果：FanLayoutCalc.ComputeSlot 输出，IDragSurface 实现负责映射到 inline style。
    /// 强类型化避免状态机直接操作 left/top/translate/rotate 等 style 字段，方便 mock 测试。
    /// </summary>
    public readonly struct FanSlotAssignment
    {
        /// <summary>水平偏移（像素，相对 hand-fan 左边）。</summary>
        public float Left { get; }

        /// <summary>垂直偏移（像素，相对 hand-fan 顶边）。</summary>
        public float Top { get; }

        /// <summary>抛物线下沉位移（offset²×TranslateYCoeff）。</summary>
        public float TranslateY { get; }

        /// <summary>旋转角度（度，正向 = 逆时针）。</summary>
        public float RotateDegrees { get; }

        /// <summary>构造扇形布局结果。</summary>
        public FanSlotAssignment(float left, float top, float translateY, float rotateDegrees)
        {
            Left = left;
            Top = top;
            TranslateY = translateY;
            RotateDegrees = rotateDegrees;
        }
    }
}
