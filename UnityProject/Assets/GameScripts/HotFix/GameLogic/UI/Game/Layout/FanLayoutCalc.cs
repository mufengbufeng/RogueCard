using System.Collections.Generic;
using UnityEngine;

namespace GameLogic
{
    /// <summary>
    /// 扇形布局与插入槽位算法的纯函数。零 UI 依赖，无副作用，全部入参显式提供。
    /// 把布局公式从 GameView 中迁出后可独立做 EditMode 单元测试。
    /// </summary>
    public static class FanLayoutCalc
    {
        /// <summary>
        /// 计算第 slotIdx 槽位（共 slotCount 槽）的扇形 transform。
        /// 公式：center=(slotCount-1)/2；offset=slotIdx-center；spacing=min(MaxCardSpacing,(fanWidth-CardWidth)/(slotCount-1))（slotCount=1 时为 0）；
        /// Left=fanWidth/2 + offset*spacing - CardWidth/2；Top=max(0, fanHeight - CardHeight - HandFanBottomPadding)；
        /// TranslateY=offset²*TranslateYCoeff；RotateDegrees=offset*RotatePerStep。
        /// </summary>
        public static FanSlotAssignment ComputeSlot(
            int slotIdx,
            int slotCount,
            float fanWidth,
            float fanHeight,
            HandFanLayoutOptions options)
        {
            if (options == null) options = new HandFanLayoutOptions();
            if (slotCount <= 0) return new FanSlotAssignment(0f, 0f, 0f, 0f);

            float center = (slotCount - 1) / 2f;
            float offset = slotIdx - center;

            float spacing = slotCount > 1
                ? Mathf.Min(options.MaxCardSpacing, (fanWidth - options.CardWidth) / (slotCount - 1))
                : 0f;

            float left = fanWidth / 2f + offset * spacing - options.CardWidth / 2f;
            float top = Mathf.Max(0f, fanHeight - options.CardHeight - options.HandFanBottomPadding);
            float translateY = offset * offset * options.TranslateYCoeff;
            float rotateDeg = offset * options.RotatePerStep;

            return new FanSlotAssignment(left, top, translateY, rotateDeg);
        }

        /// <summary>
        /// 按"距最近卡 + 鼠标在其左半 / 右半"算法计算 N 槽插入位置（取值 [0, N-1]）。
        /// otherCardWorldBounds 为剩余 N-1 张卡的 worldBound（不含被拖卡），列表索引 = _cardItems 中的视觉索引。
        /// activeIdxInVisualOrder 为被拖卡视觉索引（用于跳过判断；本函数不依赖具体值，但 caller 传入辅助语义清晰）。
        /// 总卡数 N ≤ 1 时返回 0；越界 Clamp 到 [0, N-1]。
        /// </summary>
        /// <param name="pointerPos">指针世界坐标。</param>
        /// <param name="otherCardWorldBounds">剩余卡的 worldBound 列表（按 _cardItems 视觉顺序，跳过被拖卡）；
        ///   传入空列表（即 N=1）时返回 0。</param>
        /// <param name="otherCardVisualIndices">每张剩余卡在 _cardItems 中的视觉索引（与 otherCardWorldBounds 一一对应）。</param>
        public static int ComputeInsertSlot(
            Vector2 pointerPos,
            IReadOnlyList<Rect> otherCardWorldBounds,
            IReadOnlyList<int> otherCardVisualIndices)
        {
            int otherCount = otherCardWorldBounds?.Count ?? 0;
            int totalCount = otherCount + 1; // +1 是被拖卡（不在列表中）
            if (totalCount <= 1) return 0;

            float bestDist = float.MaxValue;
            int bestVisualIdx = -1;
            float bestCenterX = 0f;
            for (int i = 0; i < otherCount; i++)
            {
                float centerX = otherCardWorldBounds[i].center.x;
                float dist = Mathf.Abs(pointerPos.x - centerX);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestVisualIdx = otherCardVisualIndices != null && i < otherCardVisualIndices.Count
                        ? otherCardVisualIndices[i]
                        : i;
                    bestCenterX = centerX;
                }
            }

            if (bestVisualIdx < 0) return 0;

            int slot = pointerPos.x < bestCenterX ? bestVisualIdx : bestVisualIdx + 1;
            return Mathf.Clamp(slot, 0, totalCount - 1);
        }
    }
}
