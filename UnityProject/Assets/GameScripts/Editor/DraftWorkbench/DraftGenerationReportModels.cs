#if UNITY_EDITOR

using System;
using System.Collections.Generic;

namespace RogueCard.Editor.DraftWorkbench
{
    /// <summary>
    /// Draft Workbench 生成报告文档。
    /// </summary>
    [Serializable]
    public class DraftGenerationReportDocument
    {
        /// <summary>报告 schema 版本。</summary>
        public int SchemaVersion = 1;

        /// <summary>源图哈希。</summary>
        public string SourceHash;

        /// <summary>创建时间（UTC）。</summary>
        public string CreatedAtUtc;

        /// <summary>更新时间（UTC）。</summary>
        public string UpdatedAtUtc;

        /// <summary>raw crop 后的 region 快照。</summary>
        public List<DraftSegmentRegion> RawRegions = new List<DraftSegmentRegion>();

        /// <summary>refinement 记录。</summary>
        public List<DraftCropRefinementRecord> RefinementRecords = new List<DraftCropRefinementRecord>();

        /// <summary>Sprite 导入报告。</summary>
        public DraftSpriteImportReport SpriteImportReport = new DraftSpriteImportReport();
    }
}

#endif
