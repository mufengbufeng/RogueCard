using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GT
{
    /// <summary>
    /// 从 ReferenceCollector 反向生成同名 UIView 脚本的 [UHubBind] 字段定义。
    /// 目标脚本按 GameObject 名称在 GameLogic UI 目录下查找，不要求脚本挂载到 GameObject。
    /// 仅在编辑器中运行。
    /// </summary>
    internal static class ReferenceCollectorUiScriptBinder
    {
        private const string UiScriptSearchRoot = "Assets/GameScripts/HotFix/GameLogic/UI";

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        /// <summary>
        /// 入口：根据 ReferenceCollector 数据更新挂载的 UIView 脚本。
        /// </summary>
        public static void Generate(ReferenceCollector collector)
        {
            if (collector == null)
            {
                return;
            }

            var assetPath = ResolveTargetScript(collector.gameObject);
            if (string.IsNullOrEmpty(assetPath))
            {
                return;
            }

            var absolutePath = GetAbsolutePath(assetPath);
            if (!File.Exists(absolutePath))
            {
                EditorUtility.DisplayDialog("脚本未找到", $"未在 {assetPath} 找到脚本文件。", "确定");
                return;
            }

            var content = File.ReadAllText(absolutePath);
            var originalContent = content;

            var rawFields = BuildFieldBindings(collector);
            var externalFields = UiScriptBinderTextRewriter.DetectExternalFields(content);

            var collisions = rawFields.Where(f => externalFields.Contains(f.FieldName)).ToList();
            var resolution = collisions.Count > 0
                ? PromptCollisionResolution(assetPath, collisions)
                : CollisionResolution.Proceed;

            if (resolution == CollisionResolution.Cancel)
            {
                Debug.Log($"[UiScriptBinder] 用户取消，未修改 {assetPath}");
                return;
            }

            var keptFields = new List<UiScriptBinderTextRewriter.FieldDescriptor>();
            var skippedFields = new List<UiScriptBinderTextRewriter.FieldDescriptor>();
            var overriddenFields = new List<UiScriptBinderTextRewriter.FieldDescriptor>();

            foreach (var field in rawFields)
            {
                if (!externalFields.Contains(field.FieldName))
                {
                    keptFields.Add(field);
                    continue;
                }

                if (resolution == CollisionResolution.Overwrite)
                {
                    overriddenFields.Add(field);
                    keptFields.Add(field);
                }
                else
                {
                    skippedFields.Add(field);
                }
            }

            if (overriddenFields.Count > 0)
            {
                content = UiScriptBinderTextRewriter.RemoveExternalFields(content, overriddenFields.Select(f => f.FieldName));
                Debug.Log(
                    $"[UiScriptBinder] 已覆盖 {overriddenFields.Count} 个 region 外字段：" +
                    string.Join(", ", overriddenFields.Select(f => f.FieldName)));
            }

            Debug.Log(
                $"[UiScriptBinder] 目标脚本 {assetPath}：将生成 {keptFields.Count} 个字段，跳过 {skippedFields.Count} 个 region 外已有同名字段。" +
                (skippedFields.Count > 0 ? " 跳过列表：" + string.Join(", ", skippedFields.Select(f => f.FieldName)) : string.Empty));

            var requiredUsings = CollectRequiredUsings(keptFields);
            content = UiScriptBinderTextRewriter.EnsureUsings(content, requiredUsings);

            var regionBlock = UiScriptBinderTextRewriter.BuildAutoRegionBlock(keptFields);
            content = UiScriptBinderTextRewriter.ReplaceOrInsertRegion(content, regionBlock);

            var injectResult = UiScriptBinderTextRewriter.EnsureUHubInitializeCall(content);
            content = injectResult.Content;
            if (!injectResult.MethodFound)
            {
                Debug.LogWarning(
                    $"[UiScriptBinder] {Path.GetFileName(assetPath)} 未找到 OnInitialize() 方法，未注入 UHub.Initialize() 调用。" +
                    " 请手动添加 protected override void OnInitialize() 并调用 UHub.Initialize();");
            }

            if (!string.Equals(content, originalContent, StringComparison.Ordinal))
            {
                File.WriteAllText(absolutePath, content, Utf8NoBom);
                AssetDatabase.ImportAsset(assetPath);
                AssetDatabase.Refresh();
                Debug.Log($"[UiScriptBinder] 已更新 {assetPath}");
            }
            else
            {
                Debug.Log($"[UiScriptBinder] {assetPath} 无变更");
            }
        }

        /// <summary>
        /// 根据 GameObject 名称推导类名，并在 GameLogic UI 目录下按文件名查找对应 .cs 资产。
        /// 不要求脚本挂载到 GameObject。
        /// </summary>
        private static string ResolveTargetScript(GameObject target)
        {
            if (target == null)
            {
                EditorUtility.DisplayDialog("无效目标", "ReferenceCollector 所在 GameObject 为空。", "确定");
                return null;
            }

            var className = SanitizeClassName(target.name);
            if (string.IsNullOrEmpty(className))
            {
                EditorUtility.DisplayDialog(
                    "无法推导类名",
                    $"无法从 GameObject 名称 \"{target.name}\" 推导出合法的 C# 类名。请重命名 GameObject/Prefab。",
                    "确定");
                return null;
            }

            string matched = null;
            string ambiguous = null;

            var guids = AssetDatabase.FindAssets($"{className} t:MonoScript", new[] { UiScriptSearchRoot });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                if (!string.Equals(Path.GetFileNameWithoutExtension(path), className, StringComparison.Ordinal))
                {
                    continue;
                }

                if (matched == null)
                {
                    matched = path;
                }
                else
                {
                    ambiguous = matched + " 与 " + path;
                    break;
                }
            }

            if (matched == null)
            {
                EditorUtility.DisplayDialog(
                    "未找到目标脚本",
                    $"未在 {UiScriptSearchRoot}/ 下找到 {className}.cs。请确认脚本文件名与 Prefab 名称完全一致（区分大小写）。",
                    "确定");
                return null;
            }

            if (ambiguous != null)
            {
                EditorUtility.DisplayDialog(
                    "找到多个同名脚本",
                    $"在 UI 目录下找到多个 {className}.cs：{ambiguous}。请重命名或删除多余文件后再试。",
                    "确定");
                return null;
            }

            return matched;
        }

        /// <summary>
        /// 把 GameObject 名称清洗为合法 C# 类名（去除非字母数字下划线、数字起始补 `_`）。
        /// </summary>
        private static string SanitizeClassName(string rawName)
        {
            var sanitized = Regex.Replace(rawName ?? string.Empty, "[^a-zA-Z0-9_]", string.Empty);
            if (string.IsNullOrEmpty(sanitized))
            {
                return null;
            }

            if (char.IsDigit(sanitized[0]))
            {
                sanitized = "_" + sanitized;
            }

            return sanitized;
        }

        /// <summary>
        /// 按 key 字典序遍历 ReferenceCollector.data，生成字段描述列表。
        /// </summary>
        private static List<UiScriptBinderTextRewriter.FieldDescriptor> BuildFieldBindings(ReferenceCollector collector)
        {
            var result = new List<UiScriptBinderTextRewriter.FieldDescriptor>();
            if (collector.data == null)
            {
                return result;
            }

            var usedNames = new HashSet<string>(StringComparer.Ordinal);
            var sortedEntries = collector.data
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.key) && entry.gameObject != null)
                .OrderBy(entry => entry.key, StringComparer.Ordinal);

            foreach (var entry in sortedEntries)
            {
                var fieldName = UiScriptBinderTextRewriter.SanitizeFieldName(entry.key, usedNames);
                var (typeName, namespaceName) = InferComponentType(entry.key, entry.gameObject);
                result.Add(new UiScriptBinderTextRewriter.FieldDescriptor(entry.key, fieldName, typeName, namespaceName));
            }

            return result;
        }

        /// <summary>
        /// 类型推断：实际组件类型 > 规则服务匹配 > GameObject 兜底。
        /// </summary>
        private static (string typeName, string namespaceName) InferComponentType(string key, Object referenced)
        {
            if (referenced is Component component)
            {
                var componentType = component.GetType();
                return (componentType.Name, componentType.Namespace ?? string.Empty);
            }

            var rule = ReferenceCollectorRuleService.FindFirstMatchingRule(key);
            if (rule != null)
            {
                var resolved = ReferenceCollectorRuleService.ResolveRule(rule);
                if (resolved.IsValid && resolved.ComponentType != null && resolved.ComponentType != typeof(GameObject))
                {
                    return (resolved.ComponentType.Name, resolved.ComponentType.Namespace ?? string.Empty);
                }
            }

            return ("GameObject", "UnityEngine");
        }

        /// <summary>
        /// 汇总字段类型对应的命名空间，并加入 UHubBind 所在的 EF.UI 命名空间。
        /// </summary>
        private static IEnumerable<string> CollectRequiredUsings(IEnumerable<UiScriptBinderTextRewriter.FieldDescriptor> fields)
        {
            var set = new HashSet<string>(StringComparer.Ordinal) { "EF.UI" };
            foreach (var field in fields)
            {
                if (!string.IsNullOrWhiteSpace(field.NamespaceName))
                {
                    set.Add(field.NamespaceName);
                }
            }

            return set;
        }

        private static string GetAbsolutePath(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }

        private enum CollisionResolution
        {
            Proceed,
            Overwrite,
            Skip,
            Cancel,
        }

        /// <summary>
        /// 弹出三选一对话框，让用户决定如何处理 region 外字段冲突。
        /// </summary>
        private static CollisionResolution PromptCollisionResolution(
            string assetPath,
            IReadOnlyList<UiScriptBinderTextRewriter.FieldDescriptor> collisions)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"在 {assetPath} 中检测到 {collisions.Count} 个 region 外字段与 prefab key 同名：");
            sb.AppendLine();
            foreach (var field in collisions)
            {
                sb.AppendLine($"  · {field.FieldName}  (key: {field.Key}, 期望类型: {field.TypeName})");
            }
            sb.AppendLine();
            sb.AppendLine("点击「覆盖」将删除这些字段并在 region 内以 [UHubBind] 形式重新生成；");
            sb.AppendLine("点击「跳过」保留它们不变，region 内不重复生成同名字段；");
            sb.AppendLine("点击「取消」立即终止，不写入任何修改。");

            var choice = EditorUtility.DisplayDialogComplex(
                "Region 外字段冲突",
                sb.ToString(),
                "覆盖",
                "取消",
                "跳过");

            switch (choice)
            {
                case 0:
                    return CollisionResolution.Overwrite;
                case 2:
                    return CollisionResolution.Skip;
                default:
                    return CollisionResolution.Cancel;
            }
        }
    }
}
