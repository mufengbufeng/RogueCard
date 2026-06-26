#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEngine;

namespace RogueCard.Editor.DraftWorkbench
{
    /// <summary>
    /// Image-To-UI 工具输出的 ui_structure.json 顶层结构。
    /// 包含画布信息、UI 元素树和元数据。
    /// </summary>
    [Serializable]
    public class UiStructure
    {
        public UiCanvas canvas;
        public UiUnityBuildOptions unity;
        public UiElement root;
        public UiMetadata metadata;
    }

    /// <summary>
    /// 画布描述，记录目标分辨率和画布名称。
    /// </summary>
    [Serializable]
    public class UiCanvas
    {
        public int width;
        public int height;
        public string name;
    }

    /// <summary>
    /// Image-To-UI 的 Unity 构建配置。
    /// Draft Workbench 会读取 Sprite 根目录作为 asset / spriteName 解析兜底。
    /// </summary>
    [Serializable]
    public class UiUnityBuildOptions
    {
        public int schemaVersion = 1;
        public string outputPrefabPath;
        public string spriteRootFolder;
    }

    /// <summary>
    /// UI 元素节点，对应 ui_structure.json 中的一个元素。
    /// 支持容器、图片、文本、按钮等多种类型，并通过 children 构成嵌套树。
    /// </summary>
    [Serializable]
    public class UiElement
    {
        /// <summary>元素类型：container / image / rect / text / button / overlay</summary>
        public string type;

        /// <summary>元素名称</summary>
        public string name;

        /// <summary>源图全局绝对位置（可选），原点为画布左上角</summary>
        public UiPosition position;

        /// <summary>元素尺寸</summary>
        public UiSize size;

        /// <summary>水平对齐：left / center / right（可选）</summary>
        public string align;

        /// <summary>垂直对齐：top / middle / bottom（可选）</summary>
        public string vAlign;

        /// <summary>布局信息（可选，仅容器类型）</summary>
        public UiLayout layout;

        /// <summary>偏移量（可选）</summary>
        public UiOffset offset;

        /// <summary>关联资源路径或资源标记（可选）</summary>
        public string asset;

        /// <summary>Unity 资源 GUID，优先用于解析 Sprite（可选）</summary>
        public string assetGuid;

        /// <summary>多 Sprite 贴图中的子 Sprite 名称（可选）</summary>
        public string spriteName;

        /// <summary>分割/切图系统输出的稳定 marker（可选）</summary>
        public string marker;

        /// <summary>Sprite 匹配提示词（可选）</summary>
        public string spriteHint;

        /// <summary>分割/切图系统输出的 region id（可选）</summary>
        public string regionId;

        /// <summary>资产语义：background / panel / button / icon / decoration / composite（可选）</summary>
        public string assetKind;

        /// <summary>alpha 输出策略：OpaqueRect / TransparentForeground / PreserveSourceAlpha（可选）</summary>
        public string alphaMode;

        /// <summary>裁剪固定留白像素（可选）</summary>
        public int paddingPixels;

        /// <summary>裁剪比例留白，按最大边计算（可选）</summary>
        public float paddingPercent;

        /// <summary>是否输出方形透明画布（可选）</summary>
        public bool makeSquare;

        /// <summary>是否要求 refined PNG 必须包含透明 alpha（可选）</summary>
        public bool requiresTransparentAlpha;

        /// <summary>颜色值，格式 #RRGGBB（可选）</summary>
        public string color;

        /// <summary>不透明度，-1 表示未设置（因为 0 是合法值）</summary>
        public float opacity = -1f;

        /// <summary>文本内容（可选，兼容旧版 text 字段）</summary>
        public string text;

        /// <summary>文本内容（可选，AI2UI 默认输出字段）</summary>
        public string textContent;

        /// <summary>字体大小，0 表示未设置</summary>
        public int fontSize;

        /// <summary>字体族（可选）</summary>
        public string fontFamily;

        /// <summary>描边颜色（可选）</summary>
        public string strokeColor;

        /// <summary>描边宽度，0 表示未设置</summary>
        public int strokeWidth;

        /// <summary>行高，0 表示未设置</summary>
        public int lineHeight;

        /// <summary>文本水平对齐（可选）</summary>
        public string alignment;

        /// <summary>文本垂直对齐（可选）</summary>
        public string textVAlign;

        /// <summary>是否使用 Sprite 已配置的九宫格切片（可选）</summary>
        public bool nineSlice;

        /// <summary>子元素列表</summary>
        public List<UiElement> children = new List<UiElement>();
    }

    /// <summary>
    /// 元素的绝对坐标位置。
    /// </summary>
    [Serializable]
    public class UiPosition
    {
        public int x;
        public int y;
    }

    /// <summary>
    /// 元素的宽高尺寸。
    /// </summary>
    [Serializable]
    public class UiSize
    {
        public int width;
        public int height;
    }

    /// <summary>
    /// 元素的偏移量。
    /// </summary>
    [Serializable]
    public class UiOffset
    {
        public int x;
        public int y;
    }

    /// <summary>
    /// 九宫格切片的四个边距。
    /// </summary>
    [Serializable]
    public class UiNineSlice
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    /// <summary>
    /// 容器布局描述，支持行/列排列。
    /// </summary>
    [Serializable]
    public class UiLayout
    {
        /// <summary>布局类型：row / column</summary>
        public string type;

        /// <summary>间距，可为字符串或整数</summary>
        public object spacing;

        /// <summary>内边距</summary>
        public UiOffset padding;

        /// <summary>子元素水平对齐</summary>
        public string align;

        /// <summary>子元素垂直对齐</summary>
        public string vAlign;
    }

    /// <summary>
    /// UI 结构的元数据，记录元素总数和备注信息。
    /// </summary>
    [Serializable]
    public class UiMetadata
    {
        /// <summary>元素总数</summary>
        public int totalElements;

        /// <summary>备注信息</summary>
        public string notes;
    }
}

#endif
