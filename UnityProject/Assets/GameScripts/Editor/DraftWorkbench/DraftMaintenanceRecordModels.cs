#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace RogueCard.Editor.DraftWorkbench
{
    /// <summary>
    /// Draft Workbench JSON 维护记录文档。
    /// <para>该文档以 prefab 同名 sidecar JSON 形式保存，用于长期维护和审计 AI2UI 生成结果。</para>
    /// </summary>
    [Serializable]
    public class DraftMaintenanceDocument
    {
        /// <summary>维护记录 JSON 的 schema 版本。</summary>
        public int SchemaVersion = 1;

        /// <summary>文档唯一标识。</summary>
        public string DocumentGuid;

        /// <summary>文档创建时间（UTC ISO-8601）。</summary>
        public string CreatedAtUtc;

        /// <summary>文档最后更新时间（UTC ISO-8601）。</summary>
        public string UpdatedAtUtc;

        /// <summary>目标 prefab 资源引用。</summary>
        public DraftMaintenanceAssetRef TargetPrefab = new DraftMaintenanceAssetRef();

        /// <summary>源设计图资源引用。</summary>
        public DraftMaintenanceAssetRef SourceImage = new DraftMaintenanceAssetRef();

        /// <summary>维护记录条目列表。</summary>
        public List<DraftMaintenanceEntry> Entries = new List<DraftMaintenanceEntry>();
    }

    /// <summary>
    /// 单次维护记录条目，可表示 preview、applied 或 reverted。
    /// </summary>
    [Serializable]
    public class DraftMaintenanceEntry
    {
        /// <summary>条目唯一标识。</summary>
        public string EntryGuid;

        /// <summary>记录阶段：preview / applied / reverted。</summary>
        public string Stage;

        /// <summary>记录时间（UTC ISO-8601）。</summary>
        public string RecordedAtUtc;

        /// <summary>源 JSON 的 SHA-256 哈希。</summary>
        public string SourceJsonHash;

        /// <summary>从 AI Result 中提取出的源 JSON 字符串。</summary>
        public string SourceJson;

        /// <summary>画布快照。</summary>
        public DraftMaintenanceCanvasSnapshot Canvas = new DraftMaintenanceCanvasSnapshot();

        /// <summary>是否包含预览快照。</summary>
        public bool HasPreview;

        /// <summary>预览快照。</summary>
        public DraftMaintenancePreviewSnapshot Preview = new DraftMaintenancePreviewSnapshot();

        /// <summary>是否包含应用记录快照。</summary>
        public bool HasApplyRecord;

        /// <summary>应用记录快照。</summary>
        public DraftMaintenanceApplySnapshot ApplyRecord = new DraftMaintenanceApplySnapshot();

        /// <summary>是否包含绑定报告快照。</summary>
        public bool HasBindingReport;

        /// <summary>绑定报告快照。</summary>
        public DraftMaintenanceBindingSnapshot BindingReport = new DraftMaintenanceBindingSnapshot();

        /// <summary>是否包含回滚记录快照。</summary>
        public bool HasRevert;

        /// <summary>回滚记录快照。</summary>
        public DraftMaintenanceRevertSnapshot Revert = new DraftMaintenanceRevertSnapshot();
    }

    /// <summary>
    /// Unity 资源引用快照。
    /// </summary>
    [Serializable]
    public class DraftMaintenanceAssetRef
    {
        /// <summary>资源 GUID。</summary>
        public string Guid;

        /// <summary>资源路径。</summary>
        public string AssetPath;

        /// <summary>资源名称。</summary>
        public string Name;
    }

    /// <summary>
    /// 画布尺寸快照。
    /// </summary>
    [Serializable]
    public class DraftMaintenanceCanvasSnapshot
    {
        /// <summary>画布宽度。</summary>
        public int Width;

        /// <summary>画布高度。</summary>
        public int Height;
    }

    /// <summary>
    /// Parse & Preview 阶段快照。
    /// </summary>
    [Serializable]
    public class DraftMaintenancePreviewSnapshot
    {
        /// <summary>节点总数。</summary>
        public int TotalCount;

        /// <summary>新建节点数量。</summary>
        public int NewCount;

        /// <summary>更新节点数量。</summary>
        public int UpdateCount;

        /// <summary>冲突节点数量。</summary>
        public int ConflictCount;

        /// <summary>转换警告。</summary>
        public List<string> ConversionWarnings = new List<string>();

        /// <summary>重命名记录。</summary>
        public List<string> RenamedElements = new List<string>();

        /// <summary>节点变更快照。</summary>
        public List<DraftMaintenanceNodeChangeSnapshot> Nodes = new List<DraftMaintenanceNodeChangeSnapshot>();
    }

    /// <summary>
    /// 单个节点变更快照。
    /// </summary>
    [Serializable]
    public class DraftMaintenanceNodeChangeSnapshot
    {
        /// <summary>节点名称。</summary>
        public string Name;

        /// <summary>父节点路径。</summary>
        public string ParentPath;

        /// <summary>描述符路径。</summary>
        public string DescriptorPath;

        /// <summary>当前解析到的 prefab 路径。</summary>
        public string ResolvedPath;

        /// <summary>是否新建节点。</summary>
        public bool IsNew;

        /// <summary>是否存在冲突。</summary>
        public bool HasConflict;

        /// <summary>最小锚点。</summary>
        public DraftVector2Dto AnchorMin = new DraftVector2Dto();

        /// <summary>最大锚点。</summary>
        public DraftVector2Dto AnchorMax = new DraftVector2Dto();

        /// <summary>锚点位置。</summary>
        public DraftVector2Dto AnchoredPosition = new DraftVector2Dto();

        /// <summary>尺寸增量。</summary>
        public DraftVector2Dto SizeDelta = new DraftVector2Dto();

        /// <summary>组件类型全名列表。</summary>
        public List<string> ComponentTypeNames = new List<string>();

        /// <summary>是否包含视觉信息。</summary>
        public bool HasVisuals;

        /// <summary>视觉信息快照。</summary>
        public DraftMaintenanceVisualsSnapshot Visuals = new DraftMaintenanceVisualsSnapshot();

        /// <summary>是否包含布局信息。</summary>
        public bool HasLayoutInfo;

        /// <summary>布局信息快照。</summary>
        public DraftMaintenanceLayoutSnapshot LayoutInfo = new DraftMaintenanceLayoutSnapshot();

        /// <summary>是否包含源图识别框。</summary>
        public bool HasSourceBounds;

        /// <summary>源图识别框。</summary>
        public DraftRectDto SourceBounds = new DraftRectDto();

        /// <summary>源图画布尺寸。</summary>
        public DraftVector2Dto SourceCanvasSize = new DraftVector2Dto();
    }

    /// <summary>
    /// 节点视觉信息快照。
    /// </summary>
    [Serializable]
    public class DraftMaintenanceVisualsSnapshot
    {
        /// <summary>是否包含节点颜色。</summary>
        public bool HasNodeColor;

        /// <summary>节点颜色。</summary>
        public DraftColorDto NodeColor = new DraftColorDto();

        /// <summary>文本内容。</summary>
        public string TextContent;

        /// <summary>字体大小。</summary>
        public int FontSize;

        /// <summary>是否包含不透明度。</summary>
        public bool HasOpacity;

        /// <summary>不透明度。</summary>
        public float Opacity;

        /// <summary>是否包含文本对齐。</summary>
        public bool HasTextAlignment;

        /// <summary>文本对齐枚举名称。</summary>
        public string TextAlignment;
    }

    /// <summary>
    /// 布局信息快照。
    /// </summary>
    [Serializable]
    public class DraftMaintenanceLayoutSnapshot
    {
        /// <summary>布局组类型。</summary>
        public string GroupType;

        /// <summary>间距。</summary>
        public string Spacing;

        /// <summary>内边距。</summary>
        public DraftVector2Dto Padding = new DraftVector2Dto();

        /// <summary>子节点对齐。</summary>
        public string ChildAlignment;

        /// <summary>是否真实使用 Unity LayoutGroup。</summary>
        public bool UseUnityLayoutGroup;
    }

    /// <summary>
    /// 应用记录快照。
    /// </summary>
    [Serializable]
    public class DraftMaintenanceApplySnapshot
    {
        /// <summary>应用记录 GUID。</summary>
        public string RecordGuid;

        /// <summary>应用时间（UTC ISO-8601）。</summary>
        public string AppliedAtUtc;

        /// <summary>新建对象路径。</summary>
        public List<string> CreatedObjectPaths = new List<string>();

        /// <summary>修改 RectTransform 快照。</summary>
        public List<DraftMaintenanceRectChangeSnapshot> ModifiedRects = new List<DraftMaintenanceRectChangeSnapshot>();

        /// <summary>修改 Image.sprite 快照。</summary>
        public List<DraftMaintenanceSpriteChangeSnapshot> SpriteChanges = new List<DraftMaintenanceSpriteChangeSnapshot>();

        /// <summary>新增 ReferenceCollector key。</summary>
        public List<string> AddedCollectorKeys = new List<string>();

        /// <summary>新增脚本字段。</summary>
        public List<string> AddedScriptFields = new List<string>();
    }

    /// <summary>
    /// RectTransform 变更快照。
    /// </summary>
    [Serializable]
    public class DraftMaintenanceRectChangeSnapshot
    {
        /// <summary>对象路径。</summary>
        public string ObjectPath;

        /// <summary>旧锚点位置。</summary>
        public DraftVector2Dto OldAnchoredPosition = new DraftVector2Dto();

        /// <summary>新锚点位置。</summary>
        public DraftVector2Dto NewAnchoredPosition = new DraftVector2Dto();

        /// <summary>旧尺寸增量。</summary>
        public DraftVector2Dto OldSizeDelta = new DraftVector2Dto();

        /// <summary>新尺寸增量。</summary>
        public DraftVector2Dto NewSizeDelta = new DraftVector2Dto();

        /// <summary>旧最小锚点。</summary>
        public DraftVector2Dto OldAnchorMin = new DraftVector2Dto();

        /// <summary>新最小锚点。</summary>
        public DraftVector2Dto NewAnchorMin = new DraftVector2Dto();

        /// <summary>旧最大锚点。</summary>
        public DraftVector2Dto OldAnchorMax = new DraftVector2Dto();

        /// <summary>新最大锚点。</summary>
        public DraftVector2Dto NewAnchorMax = new DraftVector2Dto();

        /// <summary>是否包含 pivot。</summary>
        public bool HasPivot;

        /// <summary>旧 pivot。</summary>
        public DraftVector2Dto OldPivot = new DraftVector2Dto();

        /// <summary>新 pivot。</summary>
        public DraftVector2Dto NewPivot = new DraftVector2Dto();
    }

    /// <summary>
    /// Image.sprite 变更快照。
    /// </summary>
    [Serializable]
    public class DraftMaintenanceSpriteChangeSnapshot
    {
        /// <summary>对象路径。</summary>
        public string ObjectPath;

        /// <summary>旧 Sprite GUID。</summary>
        public string OldSpriteGuid;

        /// <summary>旧 Sprite 资源路径。</summary>
        public string OldSpriteAssetPath;

        /// <summary>新 Sprite GUID。</summary>
        public string NewSpriteGuid;

        /// <summary>新 Sprite 资源路径。</summary>
        public string NewSpriteAssetPath;

        /// <summary>来源 region id。</summary>
        public string RegionId;

        /// <summary>来源 marker。</summary>
        public string Marker;
    }

    /// <summary>
    /// 绑定报告快照。
    /// </summary>
    [Serializable]
    public class DraftMaintenanceBindingSnapshot
    {
        /// <summary>新增绑定。</summary>
        public List<DraftMaintenanceBindingEntrySnapshot> AddedEntries = new List<DraftMaintenanceBindingEntrySnapshot>();

        /// <summary>跳过绑定。</summary>
        public List<DraftMaintenanceBindingEntrySnapshot> SkippedEntries = new List<DraftMaintenanceBindingEntrySnapshot>();

        /// <summary>冲突绑定。</summary>
        public List<DraftMaintenanceBindingEntrySnapshot> ConflictEntries = new List<DraftMaintenanceBindingEntrySnapshot>();

        /// <summary>新增脚本字段。</summary>
        public List<string> AddedScriptFields = new List<string>();

        /// <summary>缺少组件警告。</summary>
        public List<string> MissingComponentWarnings = new List<string>();
    }

    /// <summary>
    /// 绑定报告条目快照。
    /// </summary>
    [Serializable]
    public class DraftMaintenanceBindingEntrySnapshot
    {
        /// <summary>绑定 key。</summary>
        public string Key;

        /// <summary>对象路径。</summary>
        public string ObjectPath;

        /// <summary>组件类型。</summary>
        public string ComponentType;

        /// <summary>原因。</summary>
        public string Reason;
    }

    /// <summary>
    /// 回滚记录快照。
    /// </summary>
    [Serializable]
    public class DraftMaintenanceRevertSnapshot
    {
        /// <summary>被回滚的 apply record GUID。</summary>
        public string RevertedApplyRecordGuid;

        /// <summary>是否回滚成功。</summary>
        public bool Success;

        /// <summary>被回滚的应用记录快照。</summary>
        public DraftMaintenanceApplySnapshot RevertedApplyRecord = new DraftMaintenanceApplySnapshot();
    }

    /// <summary>
    /// Vector2 的 JSON 稳定 DTO。
    /// </summary>
    [Serializable]
    public class DraftVector2Dto
    {
        /// <summary>X 值。</summary>
        public float X;

        /// <summary>Y 值。</summary>
        public float Y;
    }

    /// <summary>
    /// Rect 的 JSON 稳定 DTO。
    /// </summary>
    [Serializable]
    public class DraftRectDto
    {
        /// <summary>X 值。</summary>
        public float X;

        /// <summary>Y 值。</summary>
        public float Y;

        /// <summary>宽度。</summary>
        public float Width;

        /// <summary>高度。</summary>
        public float Height;
    }

    /// <summary>
    /// Color 的 JSON 稳定 DTO。
    /// </summary>
    [Serializable]
    public class DraftColorDto
    {
        /// <summary>R 通道。</summary>
        public float R;

        /// <summary>G 通道。</summary>
        public float G;

        /// <summary>B 通道。</summary>
        public float B;

        /// <summary>A 通道。</summary>
        public float A;
    }
}
#endif
