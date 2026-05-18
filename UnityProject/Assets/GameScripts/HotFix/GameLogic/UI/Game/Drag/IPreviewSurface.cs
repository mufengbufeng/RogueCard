using UnityEngine;

namespace GameLogic
{
    /// <summary>
    /// 卡牌预览控制器与 UGUI 副作用的边界。
    /// </summary>
    public interface IPreviewSurface
    {
        /// <summary>
        /// 克隆源卡牌生成预览对象。
        /// </summary>
        GameObject ClonePreviewElement(CardItemView source);

        /// <summary>
        /// 把预览对象加入 preview-layer。
        /// </summary>
        void AddToPreviewLayer(GameObject element);

        /// <summary>
        /// 从 preview-layer 移除预览对象。
        /// </summary>
        void RemoveFromPreviewLayer(GameObject element);

        /// <summary>
        /// 取得源卡顶部中心在 hand-fan 本地坐标中的位置。
        /// </summary>
        Vector2 GetSourceTopCenterInHandFanLocal(CardItemView source);

        /// <summary>
        /// 把 hand-fan 本地坐标转换到 preview-layer 本地坐标。
        /// </summary>
        Vector2 ConvertHandFanLocalToPreviewLocal(Vector2 sourceTopCenterInHandFan);

        /// <summary>
        /// 应用预览对象的位置、尺寸、层级和缩放。
        /// </summary>
        void ApplyPreviewTransform(GameObject element, Vector2 topCenterInPreviewLayer, HandFanLayoutOptions options);

        /// <summary>
        /// 清理所有卡牌 hover 视觉。
        /// </summary>
        void ClearAllHoverState();
    }
}
