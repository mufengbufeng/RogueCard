using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GT
{
    internal sealed class ReferenceCollectorResolvedRule
    {
        internal ReferenceCollectorResolvedRule(ReferenceCollectorRule rule, Type componentType, string invalidReason)
        {
            Rule = rule;
            ComponentType = componentType;
            InvalidReason = invalidReason;
        }

        internal ReferenceCollectorRule Rule { get; }

        internal Type ComponentType { get; }

        internal string InvalidReason { get; }

        internal bool IsValid => ComponentType != null;
    }

    internal static class ReferenceCollectorRuleService
    {
        private const string SettingsAssetPath = "Assets/EF/EFEditor/Editor/ReferenceCollectorEditor/ReferenceCollectorRuleSettings.asset";
        private static readonly Dictionary<string, Type> TypeCacheByName = new Dictionary<string, Type>(StringComparer.Ordinal);

        /// <summary>
        /// 获取当前项目启用的收集规则。
        /// </summary>
        internal static IReadOnlyList<ReferenceCollectorRule> GetEnabledRules()
        {
            return GetRules().Where(rule => rule != null && rule.enabled && !string.IsNullOrWhiteSpace(rule.suffix)).ToList();
        }

        /// <summary>
        /// 获取当前项目所有收集规则。
        /// </summary>
        internal static IReadOnlyList<ReferenceCollectorRule> GetRules()
        {
            var settings = LoadSettingsAsset(false);
            if (settings == null || settings.rules == null || settings.rules.Count == 0)
            {
                return ReferenceCollectorRuleDefaults.CreateDefaultRules();
            }

            return settings.rules;
        }

        /// <summary>
        /// 加载或创建项目级规则配置资产。
        /// </summary>
        internal static ReferenceCollectorRuleSettings LoadOrCreateSettingsAsset()
        {
            var settings = LoadSettingsAsset(true);
            if (settings.rules == null || settings.rules.Count == 0)
            {
                settings.ResetToDefaultRules();
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }

            return settings;
        }

        /// <summary>
        /// 查找对象名命中的第一条启用规则。
        /// </summary>
        internal static ReferenceCollectorRule FindFirstMatchingRule(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return null;
            }

            foreach (var rule in GetEnabledRules())
            {
                if (objectName.EndsWith(rule.suffix, StringComparison.OrdinalIgnoreCase))
                {
                    return rule;
                }
            }

            return null;
        }

        /// <summary>
        /// 判断指定 key 是否符合当前项目自动收集规则。
        /// </summary>
        internal static bool IsAutoCollectedKey(string key)
        {
            return FindFirstMatchingRule(key) != null;
        }

        /// <summary>
        /// 解析规则中的 Unity 对象类型。
        /// </summary>
        internal static ReferenceCollectorResolvedRule ResolveRule(ReferenceCollectorRule rule)
        {
            if (rule == null)
            {
                return new ReferenceCollectorResolvedRule(null, null, "规则为空");
            }

            if (string.IsNullOrWhiteSpace(rule.componentTypeName))
            {
                return new ReferenceCollectorResolvedRule(rule, null, "组件类型为空");
            }

            var type = ResolveUnityObjectType(rule.componentTypeName);
            if (type == null)
            {
                return new ReferenceCollectorResolvedRule(rule, null, $"无法解析组件类型 {rule.componentTypeName}");
            }

            return new ReferenceCollectorResolvedRule(rule, type, string.Empty);
        }

        /// <summary>
        /// 解析 UnityEngine.Object 派生类型。
        /// </summary>
        internal static Type ResolveUnityObjectType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return null;
            }

            if (TypeCacheByName.TryGetValue(typeName, out var cachedType))
            {
                return cachedType;
            }

            var type = ResolveKnownType(typeName) ?? Type.GetType(typeName);
            if (type == null)
            {
                type = TypeCache.GetTypesDerivedFrom<Object>().FirstOrDefault(candidate =>
                    string.Equals(candidate.FullName, typeName, StringComparison.Ordinal) ||
                    string.Equals(candidate.Name, typeName, StringComparison.Ordinal));
            }

            if (type != null && !typeof(Object).IsAssignableFrom(type))
            {
                type = null;
            }

            if (type != null)
            {
                TypeCacheByName[typeName] = type;
            }

            return type;
        }

        /// <summary>
        /// 获取规则摘要行。
        /// </summary>
        internal static List<string> BuildRuleSummaryLines()
        {
            var lines = new List<string>();
            foreach (var rule in GetRules().Where(rule => rule != null))
            {
                var state = rule.enabled ? "启用" : "禁用";
                var resolved = ResolveRule(rule);
                var valid = resolved.IsValid ? "有效" : "无效：" + resolved.InvalidReason;
                lines.Add($"{rule.suffix} -> {rule.componentTypeName}（{state}，{valid}）");
            }

            return lines;
        }

        /// <summary>
        /// 收集指定 Transform 对应的目标对象。
        /// </summary>
        internal static bool TryResolveTargetObject(Transform transform, ReferenceCollectorRule rule, out Object targetObject, out string warning)
        {
            targetObject = null;
            warning = string.Empty;

            var resolved = ResolveRule(rule);
            if (!resolved.IsValid)
            {
                warning = $"[ReferenceCollector] 规则 {rule?.suffix} 无效：{resolved.InvalidReason}";
                return false;
            }

            if (resolved.ComponentType == typeof(GameObject))
            {
                targetObject = transform.gameObject;
                return true;
            }

            if (!typeof(Component).IsAssignableFrom(resolved.ComponentType))
            {
                warning = $"[ReferenceCollector] 规则 {rule.suffix} 的类型 {resolved.ComponentType.FullName} 不是组件类型";
                return false;
            }

            targetObject = transform.GetComponent(resolved.ComponentType);
            if (targetObject == null)
            {
                warning = $"[ReferenceCollector] {transform.name} 匹配规则 {rule.suffix}，但缺少组件 {resolved.ComponentType.FullName}";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 创建或选中项目级规则配置资产。
        /// </summary>
        [MenuItem("EF/ReferenceCollector/创建规则配置")]
        private static void CreateSettingsAssetMenu()
        {
            Selection.activeObject = LoadOrCreateSettingsAsset();
        }

        private static ReferenceCollectorRuleSettings LoadSettingsAsset(bool createIfMissing)
        {
            var settings = AssetDatabase.LoadAssetAtPath<ReferenceCollectorRuleSettings>(SettingsAssetPath);
            if (settings == null)
            {
                settings = FindSettingsAsset();
            }

            if (settings != null || !createIfMissing)
            {
                return settings;
            }

            settings = ScriptableObject.CreateInstance<ReferenceCollectorRuleSettings>();
            settings.ResetToDefaultRules();
            AssetDatabase.CreateAsset(settings, SettingsAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return settings;
        }

        private static ReferenceCollectorRuleSettings FindSettingsAsset()
        {
            var guid = AssetDatabase.FindAssets("t:ReferenceCollectorRuleSettings").FirstOrDefault();
            if (string.IsNullOrEmpty(guid))
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<ReferenceCollectorRuleSettings>(AssetDatabase.GUIDToAssetPath(guid));
        }

        private static Type ResolveKnownType(string typeName)
        {
            switch (typeName)
            {
                case "GameObject":
                case "UnityEngine.GameObject":
                    return typeof(GameObject);
                case "Transform":
                case "UnityEngine.Transform":
                    return typeof(Transform);
                case "SpriteRenderer":
                case "UnityEngine.SpriteRenderer":
                    return typeof(SpriteRenderer);
                case "Button":
                case "UnityEngine.UI.Button":
                    return typeof(UnityEngine.UI.Button);
                case "Text":
                case "UnityEngine.UI.Text":
                    return typeof(UnityEngine.UI.Text);
                case "Image":
                case "UnityEngine.UI.Image":
                    return typeof(UnityEngine.UI.Image);
                case "Slider":
                case "UnityEngine.UI.Slider":
                    return typeof(UnityEngine.UI.Slider);
                case "Toggle":
                case "UnityEngine.UI.Toggle":
                    return typeof(UnityEngine.UI.Toggle);
                case "InputField":
                case "UnityEngine.UI.InputField":
                    return typeof(UnityEngine.UI.InputField);
                case "Dropdown":
                case "UnityEngine.UI.Dropdown":
                    return typeof(UnityEngine.UI.Dropdown);
                default:
                    return null;
            }
        }
    }
}
