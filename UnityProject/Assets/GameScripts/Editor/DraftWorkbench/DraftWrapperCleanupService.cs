#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RogueCard.Editor.DraftWorkbench
{
    /// <summary>
    /// 历史 JSON root wrapper 清理候选。
    /// </summary>
    [Serializable]
    public class DraftWrapperCleanupCandidate
    {
        public string ObjectPath;
        public string Name;
        public bool IsRecorded;
        public string Reason;
    }

    /// <summary>
    /// 历史 JSON root wrapper 清理报告。
    /// </summary>
    [Serializable]
    public class DraftWrapperCleanupReport
    {
        public List<DraftWrapperCleanupCandidate> RecordedCandidates = new List<DraftWrapperCleanupCandidate>();
        public List<DraftWrapperCleanupCandidate> UnrecordedCandidates = new List<DraftWrapperCleanupCandidate>();
        public List<string> RemovedPaths = new List<string>();
        public List<string> Messages = new List<string>();
    }

    /// <summary>
    /// 安全清理 Draft Workbench 早期生成的顶层 JSON root wrapper。
    /// </summary>
    public static class DraftWrapperCleanupService
    {
        /// <summary>
        /// 发现可清理的记录内 wrapper，以及仅报告不删除的未记录候选。
        /// </summary>
        public static DraftWrapperCleanupReport DiscoverCandidates(
            GameObject prefabRoot,
            DraftWorkbenchMetadata metadata,
            IEnumerable<string> jsonRootNames)
        {
            var report = new DraftWrapperCleanupReport();
            if (prefabRoot == null)
            {
                report.Messages.Add("Prefab root 为空，无法执行 wrapper 清理发现。");
                return report;
            }

            var rootNames = BuildNameSet(jsonRootNames);
            if (rootNames.Count == 0)
            {
                report.Messages.Add("未提供 JSON root 名称，跳过 wrapper 清理发现。");
                return report;
            }

            var recordedTopLevelPaths = CollectRecordedTopLevelPaths(metadata);
            foreach (Transform child in prefabRoot.transform)
            {
                if (child == null || !rootNames.Contains(child.name))
                    continue;

                string path = child.name;
                bool recorded = recordedTopLevelPaths.Contains(path);
                var candidate = new DraftWrapperCleanupCandidate
                {
                    ObjectPath = path,
                    Name = child.name,
                    IsRecorded = recorded,
                    Reason = recorded
                        ? "对象路径存在于 Draft Workbench apply record 的 CreatedObjectPaths。"
                        : "名称类似 JSON root，但没有 apply record 证明其由 Draft Workbench 创建。"
                };

                if (recorded)
                {
                    report.RecordedCandidates.Add(candidate);
                }
                else
                {
                    report.UnrecordedCandidates.Add(candidate);
                }
            }

            return report;
        }

        /// <summary>
        /// 删除记录内 wrapper，并保留未记录候选供人工审查。
        /// </summary>
        public static DraftWrapperCleanupReport CleanupRecordedWrappers(
            GameObject prefabRoot,
            DraftWorkbenchMetadata metadata,
            IEnumerable<string> jsonRootNames)
        {
            var report = DiscoverCandidates(prefabRoot, metadata, jsonRootNames);
            if (prefabRoot == null)
                return report;

            foreach (var candidate in report.RecordedCandidates.ToList())
            {
                if (candidate == null || string.IsNullOrEmpty(candidate.ObjectPath))
                    continue;

                Transform target = prefabRoot.transform.Find(candidate.ObjectPath);
                if (target == null)
                {
                    report.Messages.Add(candidate.ObjectPath + ": 清理时对象已不存在。");
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(target.gameObject);
                report.RemovedPaths.Add(candidate.ObjectPath);
                report.Messages.Add(candidate.ObjectPath + ": 已删除记录内 JSON root wrapper。");
            }

            foreach (var candidate in report.UnrecordedCandidates)
            {
                if (candidate != null)
                {
                    report.Messages.Add(candidate.ObjectPath + ": 未记录候选，仅报告不自动删除。");
                }
            }

            return report;
        }

        /// <summary>
        /// 收集 apply record 中直接位于 Prefab root 下的新建对象路径。
        /// </summary>
        private static HashSet<string> CollectRecordedTopLevelPaths(DraftWorkbenchMetadata metadata)
        {
            var paths = new HashSet<string>(StringComparer.Ordinal);
            if (metadata?.ApplyRecords == null)
                return paths;

            foreach (var record in metadata.ApplyRecords)
            {
                if (record?.CreatedObjectPaths == null)
                    continue;

                foreach (string path in record.CreatedObjectPaths)
                {
                    if (string.IsNullOrWhiteSpace(path) || path.Contains("/"))
                        continue;

                    paths.Add(path);
                }
            }

            return paths;
        }

        /// <summary>
        /// 规范化 JSON root 名称集合。
        /// </summary>
        private static HashSet<string> BuildNameSet(IEnumerable<string> jsonRootNames)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            if (jsonRootNames == null)
                return names;

            foreach (string name in jsonRootNames)
            {
                if (!string.IsNullOrWhiteSpace(name))
                {
                    names.Add(name.Trim());
                }
            }

            return names;
        }
    }
}

#endif
