#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace RogueCard.Editor.DraftWorkbench
{
    /// <summary>
    /// Draft Workbench 生成报告存储服务。
    /// 负责把 raw / refined / Sprite 生成状态汇总到 Library 下的 editor-only JSON 报告中。
    /// </summary>
    public static class DraftGenerationReportStore
    {
        /// <summary>默认报告根目录。</summary>
        public const string DefaultReportRoot = "Library/DraftWorkbench/Reports";

        /// <summary>
        /// 保存或更新生成报告。
        /// </summary>
        /// <param name="manifest">当前 region 清单。</param>
        /// <param name="refinementRecords">refinement 记录。</param>
        /// <param name="importReport">Sprite 导入报告。</param>
        public static void SaveSnapshot(
            DraftSegmentationManifest manifest,
            IReadOnlyList<DraftCropRefinementRecord> refinementRecords,
            DraftSpriteImportReport importReport)
        {
            if (manifest == null)
                return;

            string sourceHash = string.IsNullOrWhiteSpace(manifest.sourceHash)
                ? "json"
                : DraftImageSegmentationService.SanitizeFileName(manifest.sourceHash);
            string reportAssetPath = (DefaultReportRoot.TrimEnd('/') + "/" + sourceHash + "/generation-report.json").Replace('\\', '/');
            string reportAbsolutePath = DraftPathUtility.ToAbsolutePath(reportAssetPath);
            string directory = Path.GetDirectoryName(reportAbsolutePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            DraftGenerationReportDocument document = Load(reportAbsolutePath) ?? CreateDocument(sourceHash);
            document.UpdatedAtUtc = DateTime.UtcNow.ToString("O");
            document.SourceHash = sourceHash;
            if (manifest.regions != null)
            {
                document.RawRegions = manifest.regions.ToList();
            }

            if (refinementRecords != null)
            {
                document.RefinementRecords = refinementRecords.ToList();
            }

            if (importReport != null)
            {
                document.SpriteImportReport = importReport;
            }

            File.WriteAllText(reportAbsolutePath, JsonUtility.ToJson(document, true), Encoding.UTF8);
        }

        /// <summary>
        /// 读取生成报告；不存在时返回 null。
        /// </summary>
        private static DraftGenerationReportDocument Load(string absolutePath)
        {
            if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
                return null;

            string json = File.ReadAllText(absolutePath, Encoding.UTF8);
            return JsonUtility.FromJson<DraftGenerationReportDocument>(json);
        }

        /// <summary>
        /// 创建新报告。
        /// </summary>
        private static DraftGenerationReportDocument CreateDocument(string sourceHash)
        {
            string now = DateTime.UtcNow.ToString("O");
            return new DraftGenerationReportDocument
            {
                SourceHash = sourceHash,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
        }
    }
}

#endif
