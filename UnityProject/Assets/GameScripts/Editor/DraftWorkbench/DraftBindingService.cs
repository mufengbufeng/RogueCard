#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GT;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RogueCard.Editor.DraftWorkbench
{
    /// <summary>
    /// 草稿绑定服务，负责根据命名规则构建绑定计划、应用 ReferenceCollector 绑定以及生成脚本字段。
    /// </summary>
    public static class DraftBindingService
    {
        /// <summary>
        /// 根据 ReferenceCollector 命名规则为指定节点列表构建绑定计划。
        /// <para>对每个节点使用 <see cref="ReferenceCollectorRuleService.FindFirstMatchingRule"/> 匹配规则，
        /// 然后通过 <see cref="ReferenceCollectorRuleService.ResolveRule"/> 解析组件类型，
        /// 检查 collector 中是否已存在同名 key，最终分类为 Added / Skipped / Conflict。</para>
        /// </summary>
        /// <param name="collector">目标 ReferenceCollector 组件。</param>
        /// <param name="nodes">需要绑定的 GameObject 节点集合。</param>
        /// <returns>包含新增、跳过和冲突条目的绑定报告。</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="collector"/> 或 <paramref name="nodes"/> 为 null。
        /// </exception>
        public static DraftBindingReport BuildBindingPlan(
            ReferenceCollector collector,
            IEnumerable<GameObject> nodes)
        {
            if (collector == null) throw new ArgumentNullException(nameof(collector));
            if (nodes == null) throw new ArgumentNullException(nameof(nodes));

            var report = new DraftBindingReport();
            var existingKeys = new HashSet<string>(
                collector.data.Select(d => d.key),
                StringComparer.Ordinal);

            foreach (var node in nodes)
            {
                if (node == null) continue;

                var rule = ReferenceCollectorRuleService.FindFirstMatchingRule(node.name);
                if (rule == null) continue;

                var resolved = ReferenceCollectorRuleService.ResolveRule(rule);
                var key = node.name;
                var entry = new BindingReportEntry
                {
                    Key = key,
                    ObjectPath = GetHierarchyPath(node.transform),
                    ComponentType = resolved.ComponentType?.FullName ?? string.Empty,
                };

                if (!resolved.IsValid)
                {
                    entry.Reason = resolved.InvalidReason;
                    report.SkippedEntries.Add(entry);
                    continue;
                }

                if (existingKeys.Contains(key))
                {
                    entry.Reason = "collector 中已存在同名 key";
                    report.ConflictEntries.Add(entry);
                    continue;
                }

                entry.Reason = string.Empty;
                report.AddedEntries.Add(entry);
                report.AddedScriptFields.Add(key);
                existingKeys.Add(key);
            }

            return report;
        }

        /// <summary>
        /// 将绑定报告中无冲突的新增条目应用到 ReferenceCollector。
        /// <para>仅处理 <see cref="DraftBindingReport.AddedEntries"/>，对每个节点调用
        /// <see cref="ReferenceCollectorRuleService.TryResolveTargetObject"/> 获取目标对象，
        /// 然后通过 <see cref="ReferenceCollector.Add"/> 写入。</para>
        /// </summary>
        /// <param name="collector">目标 ReferenceCollector 组件。</param>
        /// <param name="report">由 <see cref="BuildBindingPlan"/> 生成的绑定报告。</param>
        /// <param name="nodes">与绑定计划对应的 GameObject 节点集合。</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="collector"/>、<paramref name="report"/> 或 <paramref name="nodes"/> 为 null。
        /// </exception>
        public static void ApplyBindings(
            ReferenceCollector collector,
            DraftBindingReport report,
            IEnumerable<GameObject> nodes)
        {
            if (collector == null) throw new ArgumentNullException(nameof(collector));
            if (report == null) throw new ArgumentNullException(nameof(report));
            if (nodes == null) throw new ArgumentNullException(nameof(nodes));

            var addedKeys = new HashSet<string>(
                report.AddedEntries.Select(e => e.Key),
                StringComparer.Ordinal);

            foreach (var node in nodes)
            {
                if (node == null) continue;

                var rule = ReferenceCollectorRuleService.FindFirstMatchingRule(node.name);
                if (rule == null) continue;

                var key = node.name;
                if (!addedKeys.Contains(key)) continue;

                if (!ReferenceCollectorRuleService.TryResolveTargetObject(
                        node.transform, rule, out var targetObject, out var warning))
                {
                    report.MissingComponentWarnings.Add(warning);
                    continue;
                }

                collector.Add(key, targetObject);
            }
        }

        /// <summary>
        /// 根据绑定报告向脚本文件注入自动生成的 UHub 字段、using 语句和 UHub.Initialize 调用。
        /// <para>依次调用 <see cref="UiScriptBinderTextRewriter"/> 的
        /// <see cref="UiScriptBinderTextRewriter.BuildAutoRegionBlock"/>、
        /// <see cref="UiScriptBinderTextRewriter.ReplaceOrInsertRegion"/>、
        /// <see cref="UiScriptBinderTextRewriter.EnsureUsings"/>、
        /// <see cref="UiScriptBinderTextRewriter.EnsureUHubInitializeCall"/>。</para>
        /// </summary>
        /// <param name="scriptPath">目标脚本文件的绝对路径。</param>
        /// <param name="report">由 <see cref="BuildBindingPlan"/> 生成的绑定报告。</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="scriptPath"/> 或 <paramref name="report"/> 为 null。
        /// </exception>
        /// <exception cref="FileNotFoundException"><paramref name="scriptPath"/> 指向的文件不存在。</exception>
        public static void ApplyScriptFields(string scriptPath, DraftBindingReport report)
        {
            if (scriptPath == null) throw new ArgumentNullException(nameof(scriptPath));
            if (report == null) throw new ArgumentNullException(nameof(report));
            if (!File.Exists(scriptPath)) throw new FileNotFoundException("脚本文件不存在", scriptPath);

            if (report.AddedEntries.Count == 0) return;

            var content = File.ReadAllText(scriptPath);
            var usedNames = UiScriptBinderTextRewriter.DetectExternalFields(content);
            var fields = new List<UiScriptBinderTextRewriter.FieldDescriptor>();
            var requiredNamespaces = new HashSet<string>();

            foreach (var entry in report.AddedEntries)
            {
                if (string.IsNullOrEmpty(entry.Key)) continue;

                var fieldName = UiScriptBinderTextRewriter.SanitizeFieldName(entry.Key, usedNames);

                // 从组件类型全名中提取命名空间
                var typeName = entry.ComponentType;
                var namespaceName = string.Empty;
                if (!string.IsNullOrEmpty(typeName))
                {
                    var lastDot = typeName.LastIndexOf('.');
                    if (lastDot > 0)
                    {
                        namespaceName = typeName.Substring(0, lastDot);
                        if (namespaceName.StartsWith("UnityEngine.", StringComparison.Ordinal))
                        {
                            requiredNamespaces.Add(namespaceName);
                        }
                    }
                }

                fields.Add(new UiScriptBinderTextRewriter.FieldDescriptor(
                    entry.Key, fieldName, typeName, namespaceName));
            }

            if (fields.Count == 0) return;

            // 构建自动生成区域并插入/替换
            var regionBlock = UiScriptBinderTextRewriter.BuildAutoRegionBlock(fields);
            content = UiScriptBinderTextRewriter.ReplaceOrInsertRegion(content, regionBlock);

            // 确保 using 语句
            content = UiScriptBinderTextRewriter.EnsureUsings(content, requiredNamespaces);

            // 确保 UHub.Initialize() 调用
            var uhubResult = UiScriptBinderTextRewriter.EnsureUHubInitializeCall(content);
            content = uhubResult.Content;

            File.WriteAllText(scriptPath, content);
        }

        /// <summary>
        /// 获取 Transform 在层级中的完整路径。
        /// </summary>
        private static string GetHierarchyPath(Transform transform)
        {
            if (transform.parent == null) return transform.name;
            return GetHierarchyPath(transform.parent) + "/" + transform.name;
        }
    }
}
#endif
