#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RogueCard.Editor.DraftWorkbench
{
    /// <summary>
    /// 草稿叠加层适配模式。
    /// </summary>
    public enum OverlayFitMode
    {
        /// <summary>
        /// 等比缩放并裁剪填满目标区域。
        /// </summary>
        Fill,

        /// <summary>
        /// 等比缩放，宽度适配。
        /// </summary>
        FitWidth,

        /// <summary>
        /// 等比缩放，高度适配。
        /// </summary>
        FitHeight,

        /// <summary>
        /// 拉伸至目标区域完整尺寸（不保持比例）。
        /// </summary>
        Stretch
    }

    /// <summary>
    /// 草稿叠加层显示设置。
    /// </summary>
    [Serializable]
    public class DraftOverlaySettings
    {
        /// <summary>
        /// 叠加层不透明度，范围 0~1。
        /// </summary>
        public float Opacity = 0.5f;

        /// <summary>
        /// 叠加层适配模式。
        /// </summary>
        public OverlayFitMode FitMode = OverlayFitMode.FitWidth;

        /// <summary>
        /// 画布参考分辨率，用于计算叠加层缩放。
        /// </summary>
        public Vector2 CanvasReferenceResolution = new Vector2(1920f, 1080f);
    }

    /// <summary>
    /// 草稿应用操作记录，用于撤销与审计。
    /// </summary>
    [Serializable]
    public class DraftApplyRecord
    {
        /// <summary>
        /// 记录的唯一标识。
        /// </summary>
        public string RecordGuid;

        /// <summary>
        /// 应用操作的时间戳。
        /// </summary>
        public DateTime AppliedAt;

        /// <summary>
        /// 本次操作创建的 GameObject 层级路径列表。
        /// </summary>
        public List<string> CreatedObjectPaths = new List<string>();

        /// <summary>
        /// 本次操作修改的 RectTransform 变更记录。
        /// </summary>
        public List<RectChangeRecord> ModifiedRects = new List<RectChangeRecord>();

        /// <summary>
        /// 本次操作新增的 ReferenceCollector key 列表。
        /// </summary>
        public List<string> AddedCollectorKeys = new List<string>();

        /// <summary>
        /// 本次操作新增的脚本字段名列表。
        /// </summary>
        public List<string> AddedScriptFields = new List<string>();
    }

    /// <summary>
    /// 单个 RectTransform 的变更前后快照。
    /// </summary>
    [Serializable]
    public class RectChangeRecord
    {
        /// <summary>
        /// GameObject 在层级中的路径。
        /// </summary>
        public string ObjectPath;

        /// <summary>
        /// 变更前的锚点位置。
        /// </summary>
        public Vector2 OldAnchoredPosition;

        /// <summary>
        /// 变更后的锚点位置。
        /// </summary>
        public Vector2 NewAnchoredPosition;

        /// <summary>
        /// 变更前的尺寸增量。
        /// </summary>
        public Vector2 OldSizeDelta;

        /// <summary>
        /// 变更后的尺寸增量。
        /// </summary>
        public Vector2 NewSizeDelta;

        /// <summary>
        /// 变更前的最小锚点。
        /// </summary>
        public Vector2 OldAnchorMin;

        /// <summary>
        /// 变更后的最小锚点。
        /// </summary>
        public Vector2 NewAnchorMin;

        /// <summary>
        /// 变更前的最大锚点。
        /// </summary>
        public Vector2 OldAnchorMax;

        /// <summary>
        /// 变更后的最大锚点。
        /// </summary>
        public Vector2 NewAnchorMax;
    }

    /// <summary>
    /// 绑定操作结果报告，汇总新增、跳过和冲突条目。
    /// </summary>
    [Serializable]
    public class DraftBindingReport
    {
        /// <summary>
        /// 成功新增的绑定条目。
        /// </summary>
        public List<BindingReportEntry> AddedEntries = new List<BindingReportEntry>();

        /// <summary>
        /// 跳过的绑定条目。
        /// </summary>
        public List<BindingReportEntry> SkippedEntries = new List<BindingReportEntry>();

        /// <summary>
        /// 冲突的绑定条目。
        /// </summary>
        public List<BindingReportEntry> ConflictEntries = new List<BindingReportEntry>();

        /// <summary>
        /// 新增的脚本字段名列表。
        /// </summary>
        public List<string> AddedScriptFields = new List<string>();

        /// <summary>
        /// 缺少组件的警告信息列表。
        /// </summary>
        public List<string> MissingComponentWarnings = new List<string>();
    }

    /// <summary>
    /// 单条绑定报告条目，记录 key、目标对象、组件类型及原因。
    /// </summary>
    [Serializable]
    public class BindingReportEntry
    {
        /// <summary>
        /// 绑定的 ReferenceCollector key。
        /// </summary>
        public string Key;

        /// <summary>
        /// 目标 GameObject 的层级路径。
        /// </summary>
        public string ObjectPath;

        /// <summary>
        /// 绑定的组件类型全名。
        /// </summary>
        public string ComponentType;

        /// <summary>
        /// 跳过或冲突的原因说明。
        /// </summary>
        public string Reason;
    }
}
#endif
