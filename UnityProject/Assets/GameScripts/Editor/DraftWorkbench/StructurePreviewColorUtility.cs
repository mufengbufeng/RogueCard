#if UNITY_EDITOR
using System.Linq;
using UnityEngine;

namespace RogueCard.Editor.DraftWorkbench
{
    /// <summary>
    /// 结构框预览着色模式。
    /// </summary>
    public enum StructurePreviewColorMode
    {
        /// <summary>
        /// 按 UGUI 控件类型着色。
        /// </summary>
        ComponentType = 0,

        /// <summary>
        /// 按节点变更状态着色。
        /// </summary>
        ChangeStatus = 1
    }

    /// <summary>
    /// 结构框预览颜色工具类，提供根据节点变更/组件类型的颜色映射。
    /// <para>从 <see cref="DraftWorkbenchWindow"/> 提取，便于独立测试。</para>
    /// </summary>
    internal static class StructurePreviewColorUtility
    {
        /// <summary>
        /// 新建节点结构框颜色。
        /// </summary>
        public static readonly Color NewColor = new Color(0.25f, 0.9f, 0.35f, 1f);

        /// <summary>
        /// 已存在节点结构框颜色。
        /// </summary>
        public static readonly Color ExistingColor = new Color(0.3f, 0.65f, 1f, 1f);

        /// <summary>
        /// 冲突节点结构框颜色。
        /// </summary>
        public static readonly Color ConflictColor = new Color(1f, 0.55f, 0.1f, 1f);

        /// <summary>
        /// Button 控件结构框颜色。
        /// </summary>
        public static readonly Color ButtonColor = new Color(1f, 0.45f, 0.85f, 1f);

        /// <summary>
        /// Image / RawImage 控件结构框颜色。
        /// </summary>
        public static readonly Color ImageColor = new Color(0.1f, 0.85f, 1f, 1f);

        /// <summary>
        /// Text / TMP 文本控件结构框颜色。
        /// </summary>
        public static readonly Color TextColor = new Color(1f, 0.85f, 0.25f, 1f);

        /// <summary>
        /// LayoutGroup 容器结构框颜色。
        /// </summary>
        public static readonly Color LayoutColor = new Color(0.75f, 0.55f, 1f, 1f);

        /// <summary>
        /// 未识别控件类型结构框颜色。
        /// </summary>
        public static readonly Color DefaultTypeColor = new Color(0.85f, 0.85f, 0.85f, 1f);

        /// <summary>
        /// 根据当前着色模式选择结构框颜色。
        /// <para>冲突节点始终优先显示冲突色。</para>
        /// </summary>
        /// <param name="colorMode">着色模式。</param>
        /// <param name="change">节点变更。</param>
        /// <returns>用于绘制的颜色。</returns>
        public static Color GetStructurePreviewColor(StructurePreviewColorMode colorMode, UguiNodeChange change)
        {
            if (change.HasConflict)
                return ConflictColor;

            return colorMode == StructurePreviewColorMode.ComponentType
                ? GetComponentTypeColor(change.Descriptor)
                : GetChangeStatusColor(change);
        }

        /// <summary>
        /// 根据变更状态选择结构框颜色。
        /// </summary>
        /// <param name="change">节点变更。</param>
        /// <returns>用于绘制的颜色。</returns>
        public static Color GetChangeStatusColor(UguiNodeChange change)
        {
            return change.IsNew ? NewColor : ExistingColor;
        }

        /// <summary>
        /// 根据节点组件类型选择结构框颜色。
        /// </summary>
        /// <param name="descriptor">节点描述符。</param>
        /// <returns>用于绘制的颜色。</returns>
        public static Color GetComponentTypeColor(UguiNodeDescriptor descriptor)
        {
            if (descriptor == null)
                return DefaultTypeColor;

            if (HasComponentType(descriptor, "UnityEngine.UI.Button"))
                return ButtonColor;

            if (HasComponentType(descriptor, "UnityEngine.UI.Text") || HasComponentType(descriptor, "TMPro.TextMeshProUGUI"))
                return TextColor;

            if (HasComponentType(descriptor, "UnityEngine.UI.Image") || HasComponentType(descriptor, "UnityEngine.UI.RawImage"))
                return ImageColor;

            if (descriptor.LayoutInfo != null)
                return LayoutColor;

            return DefaultTypeColor;
        }

        /// <summary>
        /// 判断节点描述符是否包含指定组件类型名。
        /// </summary>
        /// <param name="descriptor">节点描述符。</param>
        /// <param name="componentTypeName">组件类型全名。</param>
        /// <returns>包含该组件类型时返回 true。</returns>
        public static bool HasComponentType(UguiNodeDescriptor descriptor, string componentTypeName)
        {
            return descriptor.ComponentTypeNames != null
                   && descriptor.ComponentTypeNames.Any(typeName => typeName == componentTypeName);
        }
    }
}
#endif
