#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace RogueCard.Editor.DraftWorkbench
{
    /// <summary>
    /// Draft Workbench JSON 维护记录存储服务。
    /// <para>负责把 preview / applied / reverted 记录追加到与目标 prefab 同名的 JSON sidecar 文件。</para>
    /// </summary>
    public static class DraftMaintenanceRecordStore
    {
        private const int CurrentSchemaVersion = 1;
        private const string PreviewStage = "preview";
        private const string AppliedStage = "applied";
        private const string RevertedStage = "reverted";

        /// <summary>
        /// 根据目标 prefab 路径推导 JSON 维护记录路径。
        /// </summary>
        /// <param name="prefabAssetPath">目标 prefab 的 Assets 相对路径。</param>
        /// <returns>维护记录 JSON 路径。</returns>
        public static string GetMaintenanceAssetPathForPrefab(string prefabAssetPath)
        {
            if (string.IsNullOrEmpty(prefabAssetPath))
                return string.Empty;

            string ext = Path.GetExtension(prefabAssetPath);
            string withoutExt = string.IsNullOrEmpty(ext)
                ? prefabAssetPath
                : prefabAssetPath.Substring(0, prefabAssetPath.Length - ext.Length);
            return withoutExt + ".draft.maintenance.json";
        }

        /// <summary>
        /// 计算源 JSON 的 SHA-256 哈希。
        /// </summary>
        /// <param name="sourceJson">源 JSON 字符串。</param>
        /// <returns>形如 sha256:&lt;hex&gt; 的哈希字符串。</returns>
        public static string ComputeSourceJsonHash(string sourceJson)
        {
            string normalized = (sourceJson ?? string.Empty).Trim();
            using (var sha = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(normalized);
                byte[] hash = sha.ComputeHash(bytes);
                var builder = new StringBuilder("sha256:");
                foreach (byte value in hash)
                {
                    builder.Append(value.ToString("x2"));
                }

                return builder.ToString();
            }
        }

        /// <summary>
        /// 追加 Parse & Preview 阶段维护记录。
        /// </summary>
        /// <returns>追加后的维护记录条目；如果未写入则返回 null。</returns>
        public static DraftMaintenanceEntry AppendPreviewRecord(
            string prefabAssetPath,
            Texture2D sourceImage,
            DraftWorkbenchMetadata metadata,
            string sourceJson,
            IReadOnlyList<UguiNodeChange> previewChanges,
            ConversionReport conversionReport,
            int canvasWidth,
            int canvasHeight,
            GameObject prefabRoot)
        {
            return AppendRecord(
                prefabAssetPath,
                sourceImage,
                metadata,
                sourceJson,
                previewChanges,
                conversionReport,
                canvasWidth,
                canvasHeight,
                prefabRoot,
                PreviewStage,
                null,
                null,
                null,
                true);
        }

        /// <summary>
        /// 追加成功应用到 prefab 后的维护记录。
        /// </summary>
        /// <returns>追加后的维护记录条目；如果未写入则返回 null。</returns>
        public static DraftMaintenanceEntry AppendAppliedRecord(
            string prefabAssetPath,
            Texture2D sourceImage,
            DraftWorkbenchMetadata metadata,
            string sourceJson,
            IReadOnlyList<UguiNodeChange> previewChanges,
            ConversionReport conversionReport,
            DraftApplyRecord applyRecord,
            DraftBindingReport bindingReport,
            int canvasWidth,
            int canvasHeight,
            GameObject prefabRoot)
        {
            return AppendRecord(
                prefabAssetPath,
                sourceImage,
                metadata,
                sourceJson,
                previewChanges,
                conversionReport,
                canvasWidth,
                canvasHeight,
                prefabRoot,
                AppliedStage,
                applyRecord,
                bindingReport,
                null,
                true);
        }

        /// <summary>
        /// 追加成功回滚后的维护记录。
        /// </summary>
        /// <returns>追加后的维护记录条目；如果未写入则返回 null。</returns>
        public static DraftMaintenanceEntry AppendRevertedRecord(
            string prefabAssetPath,
            Texture2D sourceImage,
            DraftWorkbenchMetadata metadata,
            DraftApplyRecord revertedRecord,
            string sourceJsonHash,
            bool success)
        {
            return AppendRecord(
                prefabAssetPath,
                sourceImage,
                metadata,
                null,
                null,
                null,
                0,
                0,
                null,
                RevertedStage,
                null,
                null,
                CreateRevertSnapshot(revertedRecord, success),
                false,
                sourceJsonHash);
        }

        /// <summary>
        /// 加载维护记录文档；如果不存在则返回 null。
        /// </summary>
        /// <param name="prefabAssetPath">目标 prefab 的 Assets 相对路径。</param>
        /// <returns>维护记录文档，或 null。</returns>
        public static DraftMaintenanceDocument Load(string prefabAssetPath)
        {
            string assetPath = GetMaintenanceAssetPathForPrefab(prefabAssetPath);
            string absolutePath = ToAbsolutePath(assetPath);
            if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
                return null;

            string json = File.ReadAllText(absolutePath, Encoding.UTF8);
            return JsonUtility.FromJson<DraftMaintenanceDocument>(json);
        }

        /// <summary>
        /// 将维护记录文档保存到目标 prefab 对应的 JSON 路径。
        /// </summary>
        /// <param name="prefabAssetPath">目标 prefab 的 Assets 相对路径。</param>
        /// <param name="document">维护记录文档。</param>
        public static void Save(string prefabAssetPath, DraftMaintenanceDocument document)
        {
            string assetPath = GetMaintenanceAssetPathForPrefab(prefabAssetPath);
            string absolutePath = ToAbsolutePath(assetPath);
            if (string.IsNullOrEmpty(absolutePath))
                throw new ArgumentException("维护记录路径为空", nameof(prefabAssetPath));

            string directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(absolutePath, JsonUtility.ToJson(document, true), Encoding.UTF8);
            if (!string.IsNullOrEmpty(assetPath) && assetPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                AssetDatabase.ImportAsset(assetPath);
            }
        }

        /// <summary>
        /// 追加维护记录的通用实现。
        /// </summary>
        private static DraftMaintenanceEntry AppendRecord(
            string prefabAssetPath,
            Texture2D sourceImage,
            DraftWorkbenchMetadata metadata,
            string sourceJson,
            IReadOnlyList<UguiNodeChange> previewChanges,
            ConversionReport conversionReport,
            int canvasWidth,
            int canvasHeight,
            GameObject prefabRoot,
            string stage,
            DraftApplyRecord applyRecord,
            DraftBindingReport bindingReport,
            DraftMaintenanceRevertSnapshot revertSnapshot,
            bool includePreview,
            string explicitSourceJsonHash = null)
        {
            if (string.IsNullOrEmpty(prefabAssetPath))
            {
                Debug.LogWarning("[DraftMaintenanceRecordStore] 未选择 Target Prefab，跳过维护记录保存。");
                return null;
            }

            try
            {
                string now = DateTime.UtcNow.ToString("O");
                var document = LoadOrCreateDocument(prefabAssetPath, sourceImage, metadata, now);
                document.UpdatedAtUtc = now;
                document.TargetPrefab = CreateAssetRef(prefabAssetPath);
                document.SourceImage = CreateSourceImageAssetRef(sourceImage, metadata);

                string sourceJsonHash = !string.IsNullOrEmpty(explicitSourceJsonHash)
                    ? explicitSourceJsonHash
                    : ComputeSourceJsonHash(sourceJson);

                var entry = new DraftMaintenanceEntry
                {
                    EntryGuid = Guid.NewGuid().ToString(),
                    Stage = stage,
                    RecordedAtUtc = now,
                    SourceJsonHash = sourceJsonHash,
                    SourceJson = sourceJson ?? string.Empty,
                    Canvas = new DraftMaintenanceCanvasSnapshot { Width = canvasWidth, Height = canvasHeight }
                };

                if (includePreview)
                {
                    entry.HasPreview = true;
                    entry.Preview = CreatePreviewSnapshot(previewChanges, conversionReport, prefabRoot);
                }

                if (applyRecord != null)
                {
                    entry.HasApplyRecord = true;
                    entry.ApplyRecord = CreateApplySnapshot(applyRecord);
                }

                if (bindingReport != null)
                {
                    entry.HasBindingReport = true;
                    entry.BindingReport = CreateBindingSnapshot(bindingReport);
                }

                if (revertSnapshot != null)
                {
                    entry.HasRevert = true;
                    entry.Revert = revertSnapshot;
                }

                if (document.Entries == null)
                {
                    document.Entries = new List<DraftMaintenanceEntry>();
                }

                document.Entries.Add(entry);
                Save(prefabAssetPath, document);
                return entry;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DraftMaintenanceRecordStore] 保存维护记录失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 加载或创建维护记录文档。
        /// </summary>
        private static DraftMaintenanceDocument LoadOrCreateDocument(
            string prefabAssetPath,
            Texture2D sourceImage,
            DraftWorkbenchMetadata metadata,
            string now)
        {
            var document = Load(prefabAssetPath);
            if (document != null)
            {
                if (document.Entries == null)
                {
                    document.Entries = new List<DraftMaintenanceEntry>();
                }

                return document;
            }

            return new DraftMaintenanceDocument
            {
                SchemaVersion = CurrentSchemaVersion,
                DocumentGuid = Guid.NewGuid().ToString(),
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                TargetPrefab = CreateAssetRef(prefabAssetPath),
                SourceImage = CreateSourceImageAssetRef(sourceImage, metadata),
                Entries = new List<DraftMaintenanceEntry>()
            };
        }

        /// <summary>
        /// 创建资源引用快照。
        /// </summary>
        private static DraftMaintenanceAssetRef CreateAssetRef(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return new DraftMaintenanceAssetRef();

            return new DraftMaintenanceAssetRef
            {
                Guid = AssetDatabase.AssetPathToGUID(assetPath),
                AssetPath = assetPath,
                Name = Path.GetFileNameWithoutExtension(assetPath)
            };
        }

        /// <summary>
        /// 创建源图资源引用快照。
        /// </summary>
        private static DraftMaintenanceAssetRef CreateSourceImageAssetRef(Texture2D sourceImage, DraftWorkbenchMetadata metadata)
        {
            if (sourceImage != null)
            {
                string path = AssetDatabase.GetAssetPath(sourceImage);
                if (!string.IsNullOrEmpty(path))
                    return CreateAssetRef(path);

                return new DraftMaintenanceAssetRef { Name = sourceImage.name };
            }

            if (metadata != null && !string.IsNullOrEmpty(metadata.SourceImageAssetGuid))
            {
                string path = AssetDatabase.GUIDToAssetPath(metadata.SourceImageAssetGuid);
                if (!string.IsNullOrEmpty(path))
                    return CreateAssetRef(path);
            }

            return new DraftMaintenanceAssetRef();
        }

        /// <summary>
        /// 创建 preview 快照。
        /// </summary>
        private static DraftMaintenancePreviewSnapshot CreatePreviewSnapshot(
            IReadOnlyList<UguiNodeChange> previewChanges,
            ConversionReport conversionReport,
            GameObject prefabRoot)
        {
            var snapshot = new DraftMaintenancePreviewSnapshot();
            if (previewChanges != null)
            {
                snapshot.TotalCount = previewChanges.Count;
                snapshot.NewCount = previewChanges.Count(change => change != null && change.IsNew && !change.HasConflict);
                snapshot.UpdateCount = previewChanges.Count(change => change != null && !change.IsNew && !change.HasConflict);
                snapshot.ConflictCount = previewChanges.Count(change => change != null && change.HasConflict);

                foreach (var change in previewChanges)
                {
                    var nodeSnapshot = CreateNodeSnapshot(change, prefabRoot);
                    if (nodeSnapshot != null)
                    {
                        snapshot.Nodes.Add(nodeSnapshot);
                    }
                }
            }

            if (conversionReport != null)
            {
                CopyStrings(conversionReport.Warnings, snapshot.ConversionWarnings);
                CopyStrings(conversionReport.RenamedElements, snapshot.RenamedElements);
            }

            return snapshot;
        }

        /// <summary>
        /// 创建单个节点快照。
        /// </summary>
        private static DraftMaintenanceNodeChangeSnapshot CreateNodeSnapshot(UguiNodeChange change, GameObject prefabRoot)
        {
            if (change?.Descriptor == null)
                return null;

            var desc = change.Descriptor;
            var snapshot = new DraftMaintenanceNodeChangeSnapshot
            {
                Name = desc.Name,
                ParentPath = desc.ParentPath,
                DescriptorPath = PrefabDraftBuilder.GetDescriptorPath(desc),
                ResolvedPath = prefabRoot != null ? PrefabDraftBuilder.GetResolvedDescriptorPath(prefabRoot, desc) : string.Empty,
                IsNew = change.IsNew,
                HasConflict = change.HasConflict,
                AnchorMin = ToDto(desc.AnchorMin),
                AnchorMax = ToDto(desc.AnchorMax),
                AnchoredPosition = ToDto(desc.AnchoredPosition),
                SizeDelta = ToDto(desc.SizeDelta),
                HasSourceBounds = desc.HasSourceBounds,
                SourceBounds = ToDto(desc.SourceBounds),
                SourceCanvasSize = ToDto(desc.SourceCanvasSize)
            };

            CopyStrings(desc.ComponentTypeNames, snapshot.ComponentTypeNames);

            if (desc.Visuals != null)
            {
                snapshot.HasVisuals = true;
                snapshot.Visuals = CreateVisualsSnapshot(desc.Visuals);
            }

            if (desc.LayoutInfo != null)
            {
                snapshot.HasLayoutInfo = true;
                snapshot.LayoutInfo = CreateLayoutSnapshot(desc.LayoutInfo);
            }

            return snapshot;
        }

        /// <summary>
        /// 创建视觉信息快照。
        /// </summary>
        private static DraftMaintenanceVisualsSnapshot CreateVisualsSnapshot(UiNodeVisuals visuals)
        {
            var snapshot = new DraftMaintenanceVisualsSnapshot
            {
                TextContent = visuals.TextContent,
                FontSize = visuals.FontSize,
                HasTextAlignment = visuals.HasTextAlignment,
                TextAlignment = visuals.TextAlignment.ToString()
            };

            if (visuals.NodeColor.HasValue)
            {
                snapshot.HasNodeColor = true;
                snapshot.NodeColor = ToDto(visuals.NodeColor.Value);
            }

            if (visuals.Opacity.HasValue)
            {
                snapshot.HasOpacity = true;
                snapshot.Opacity = visuals.Opacity.Value;
            }

            return snapshot;
        }

        /// <summary>
        /// 创建布局信息快照。
        /// </summary>
        private static DraftMaintenanceLayoutSnapshot CreateLayoutSnapshot(LayoutGroupInfo layoutInfo)
        {
            return new DraftMaintenanceLayoutSnapshot
            {
                GroupType = layoutInfo.GroupType.ToString(),
                Spacing = layoutInfo.Spacing,
                Padding = ToDto(layoutInfo.Padding),
                ChildAlignment = layoutInfo.ChildAlignment,
                UseUnityLayoutGroup = layoutInfo.UseUnityLayoutGroup
            };
        }

        /// <summary>
        /// 创建应用记录快照。
        /// </summary>
        private static DraftMaintenanceApplySnapshot CreateApplySnapshot(DraftApplyRecord record)
        {
            var snapshot = new DraftMaintenanceApplySnapshot
            {
                RecordGuid = record.RecordGuid,
                AppliedAtUtc = record.AppliedAt.ToUniversalTime().ToString("O")
            };

            CopyStrings(record.CreatedObjectPaths, snapshot.CreatedObjectPaths);
            CopyStrings(record.AddedCollectorKeys, snapshot.AddedCollectorKeys);
            CopyStrings(record.AddedScriptFields, snapshot.AddedScriptFields);

            if (record.ModifiedRects != null)
            {
                foreach (var rect in record.ModifiedRects)
                {
                    if (rect == null)
                        continue;

                    snapshot.ModifiedRects.Add(new DraftMaintenanceRectChangeSnapshot
                    {
                        ObjectPath = rect.ObjectPath,
                        OldAnchoredPosition = ToDto(rect.OldAnchoredPosition),
                        NewAnchoredPosition = ToDto(rect.NewAnchoredPosition),
                        OldSizeDelta = ToDto(rect.OldSizeDelta),
                        NewSizeDelta = ToDto(rect.NewSizeDelta),
                        OldAnchorMin = ToDto(rect.OldAnchorMin),
                        NewAnchorMin = ToDto(rect.NewAnchorMin),
                        OldAnchorMax = ToDto(rect.OldAnchorMax),
                        NewAnchorMax = ToDto(rect.NewAnchorMax),
                        HasPivot = rect.HasPivot,
                        OldPivot = ToDto(rect.OldPivot),
                        NewPivot = ToDto(rect.NewPivot)
                    });
                }
            }

            if (record.SpriteChanges != null)
            {
                foreach (var spriteChange in record.SpriteChanges)
                {
                    if (spriteChange == null)
                        continue;

                    snapshot.SpriteChanges.Add(new DraftMaintenanceSpriteChangeSnapshot
                    {
                        ObjectPath = spriteChange.ObjectPath,
                        OldSpriteGuid = spriteChange.OldSpriteGuid,
                        OldSpriteAssetPath = spriteChange.OldSpriteAssetPath,
                        NewSpriteGuid = spriteChange.NewSpriteGuid,
                        NewSpriteAssetPath = spriteChange.NewSpriteAssetPath,
                        RegionId = spriteChange.RegionId,
                        Marker = spriteChange.Marker
                    });
                }
            }

            return snapshot;
        }

        /// <summary>
        /// 创建绑定报告快照。
        /// </summary>
        private static DraftMaintenanceBindingSnapshot CreateBindingSnapshot(DraftBindingReport report)
        {
            var snapshot = new DraftMaintenanceBindingSnapshot();
            CopyBindingEntries(report.AddedEntries, snapshot.AddedEntries);
            CopyBindingEntries(report.SkippedEntries, snapshot.SkippedEntries);
            CopyBindingEntries(report.ConflictEntries, snapshot.ConflictEntries);
            CopyStrings(report.AddedScriptFields, snapshot.AddedScriptFields);
            CopyStrings(report.MissingComponentWarnings, snapshot.MissingComponentWarnings);
            return snapshot;
        }

        /// <summary>
        /// 创建回滚记录快照。
        /// </summary>
        private static DraftMaintenanceRevertSnapshot CreateRevertSnapshot(DraftApplyRecord revertedRecord, bool success)
        {
            return new DraftMaintenanceRevertSnapshot
            {
                RevertedApplyRecordGuid = revertedRecord?.RecordGuid,
                Success = success,
                RevertedApplyRecord = revertedRecord != null
                    ? CreateApplySnapshot(revertedRecord)
                    : new DraftMaintenanceApplySnapshot()
            };
        }

        /// <summary>
        /// 复制绑定条目列表。
        /// </summary>
        private static void CopyBindingEntries(
            IEnumerable<BindingReportEntry> source,
            List<DraftMaintenanceBindingEntrySnapshot> target)
        {
            if (source == null || target == null)
                return;

            foreach (var entry in source)
            {
                if (entry == null)
                    continue;

                target.Add(new DraftMaintenanceBindingEntrySnapshot
                {
                    Key = entry.Key,
                    ObjectPath = entry.ObjectPath,
                    ComponentType = entry.ComponentType,
                    Reason = entry.Reason
                });
            }
        }

        /// <summary>
        /// 复制字符串集合。
        /// </summary>
        private static void CopyStrings(IEnumerable<string> source, List<string> target)
        {
            if (source == null || target == null)
                return;

            foreach (string value in source)
            {
                target.Add(value ?? string.Empty);
            }
        }

        /// <summary>
        /// 将 Vector2 转为 DTO。
        /// </summary>
        private static DraftVector2Dto ToDto(Vector2 value)
        {
            return new DraftVector2Dto { X = value.x, Y = value.y };
        }

        /// <summary>
        /// 将 Rect 转为 DTO。
        /// </summary>
        private static DraftRectDto ToDto(Rect value)
        {
            return new DraftRectDto
            {
                X = value.x,
                Y = value.y,
                Width = value.width,
                Height = value.height
            };
        }

        /// <summary>
        /// 将 Color 转为 DTO。
        /// </summary>
        private static DraftColorDto ToDto(Color value)
        {
            return new DraftColorDto
            {
                R = value.r,
                G = value.g,
                B = value.b,
                A = value.a
            };
        }

        /// <summary>
        /// 将 Assets 相对路径转换为绝对路径。
        /// </summary>
        private static string ToAbsolutePath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return string.Empty;

            if (Path.IsPathRooted(assetPath))
                return assetPath;

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            return string.IsNullOrEmpty(projectRoot)
                ? string.Empty
                : Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
#endif
