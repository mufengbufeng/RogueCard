using UnityEngine;
using UnityEngine.UIElements;

namespace GameLogic
{
    /// <summary>
    /// 预览控制器与 UI 副作用的边界。CardPreviewController 通过此接口克隆模板 / 添加到 preview-layer / 计算坐标 /
    /// 清掉所有卡的 hover 类，便于在测试中用 mock 记录调用序列。
    /// 生产实现位于 HandFanView 内部。
    /// </summary>
    public interface IPreviewSurface
    {
        /// <summary>克隆 CardItem 模板生成预览克隆卡（含 .card-item 内层节点 + name/cost 文本同步源卡）。</summary>
        VisualElement ClonePreviewElement(VisualElement source);

        /// <summary>把克隆元素加入 preview-layer 容器。</summary>
        void AddToPreviewLayer(VisualElement element);

        /// <summary>从 preview-layer 移除指定元素。幂等。</summary>
        void RemoveFromPreviewLayer(VisualElement element);

        /// <summary>
        /// 把 source 卡在 hand-fan 内的局部坐标转换成 preview-layer 内的局部坐标。
        /// 公式：sourceTopCenterInHandFan → hand-fan.LocalToWorld → preview-layer.WorldToLocal。
        /// </summary>
        Vector2 ConvertHandFanLocalToPreviewLocal(Vector2 sourceTopCenterInHandFan);

        /// <summary>清掉所有手牌的 card-item--hovering 类。预览态前调用避免 hover 类残留。</summary>
        void ClearAllHoverState();
    }
}
