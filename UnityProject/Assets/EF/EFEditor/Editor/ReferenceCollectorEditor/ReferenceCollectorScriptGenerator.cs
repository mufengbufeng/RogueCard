using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using EF.Common;
using UnityEditor;
using UnityEngine;

namespace GT
{
    internal static class ReferenceCollectorScriptGenerator
    {
        private const string NewScriptTemplate =
            "{0}" +
            "namespace GameLogic\n" +
            "{{\n" +
            "    [Window(UILayer.UI, location: \"{1}\")]\n" +
            "    class {2} : UIWindow\n" +
            "    {{\n" +
            "{3}\n" +
            "    }}\n" +
            "}}\n";

        private const string RegionTemplate =
            "#region 脚本工具生成的代码\n\n" +
            "        private ReferenceCollector _referenceCollector;\n" +
            "{0}" +
            "        protected override void BindMemberProperty()\n" +
            "        {{\n" +
            "            base.BindMemberProperty();\n" +
            "            _referenceCollector = gameObject.GetComponent<ReferenceCollector>();\n" +
            "            if (_referenceCollector == null)\n" +
            "            {{\n" +
            "                return;\n" +
            "            }}\n" +
            "{1}" +
            "        }}\n\n" +
            "#endregion";

        private static readonly UTF8Encoding Utf8EncodingNoBom = new UTF8Encoding(false);

        internal static void Generate(ReferenceCollector collector)
        {
            if (collector == null)
            {
                return;
            }

            var className = SanitizeClassName(collector.gameObject != null ? collector.gameObject.name : "GeneratedView");
            var generation = BuildGenerationData(collector);

            var assetPath = ResolveScriptAssetPath(className);
            var absolutePath = GetAbsolutePath(assetPath);
            var directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (!File.Exists(absolutePath))
            {
                var content = BuildNewScriptContent(collector.gameObject != null ? collector.gameObject.name : className, className, generation);
                File.WriteAllText(absolutePath, content, Utf8EncodingNoBom);
            }
            else
            {
                var content = File.ReadAllText(absolutePath);
                var updated = UpdateExistingScript(content, generation);
                if (!string.Equals(content, updated, StringComparison.Ordinal))
                {
                    File.WriteAllText(absolutePath, updated, Utf8EncodingNoBom);
                }
            }

            AssetDatabase.ImportAsset(assetPath);
            AssetDatabase.Refresh();
            Debug.Log($"脚本已生成/更新：{assetPath}");
        }

        private static GenerationResult BuildGenerationData(ReferenceCollector collector)
        {
            var codeStyle = ScriptGeneratorSettingProxy.GetCodeStyle();
            var requiredUsings = new HashSet<string>(StringComparer.Ordinal) { "EF", "UnityEngine", "GT.Runtime" };
            var fieldNames = new HashSet<string>(StringComparer.Ordinal);
            var fields = new List<FieldBinding>();

            if (collector.data != null)
            {
                foreach (var entry in collector.data.Where(d => !string.IsNullOrEmpty(d.key)).OrderBy(d => d.key, StringComparer.Ordinal))
                {
                    var binding = CreateBinding(entry, codeStyle, fieldNames);
                    if (binding != null)
                    {
                        if (!string.IsNullOrEmpty(binding.Namespace))
                        {
                            requiredUsings.Add(binding.Namespace);
                        }

                        fields.Add(binding);
                    }
                }
            }

            var region = BuildRegionBlock(fields);
            return new GenerationResult(region, requiredUsings);
        }

        private static string ResolveScriptAssetPath(string className)
        {
            string assetPath = TryFindExistingScript(className);
            if (!string.IsNullOrEmpty(assetPath))
            {
                return assetPath;
            }

            var configuredPath = ScriptGeneratorSettingProxy.GetCodePath();
            if (string.IsNullOrEmpty(configuredPath))
            {
                configuredPath = "Assets/GameScripts/HotFix/GameLogic/UI";
            }

            configuredPath = configuredPath.Replace('\\', '/');
            if (!configuredPath.StartsWith("Assets", StringComparison.Ordinal))
            {
                configuredPath = Path.Combine("Assets", configuredPath).Replace('\\', '/');
            }

            var directory = Path.Combine(configuredPath, className).Replace('\\', '/');
            return Path.Combine(directory, className + ".cs").Replace('\\', '/');
        }

        private static string TryFindExistingScript(string className)
        {
            var guids = AssetDatabase.FindAssets(className + " t:Script");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.Equals(Path.GetFileNameWithoutExtension(path), className, StringComparison.Ordinal))
                {
                    return path;
                }
            }

            return null;
        }

        private static string UpdateExistingScript(string content, GenerationResult generation)
        {
            var updated = EnsureUsings(content, generation.RequiredUsings);
            updated = ReplaceRegion(updated, generation.RegionBlock);
            return updated;
        }

        private static string EnsureUsings(string content, HashSet<string> requiredUsings)
        {
            if (requiredUsings == null || requiredUsings.Count == 0)
            {
                return content;
            }

            var existing = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match match in Regex.Matches(content, @"^using\s+([^;\s]+)\s*;", RegexOptions.Multiline))
            {
                existing.Add(match.Groups[1].Value);
            }

            var missing = requiredUsings.Where(u => !existing.Contains(u)).ToList();
            if (missing.Count == 0)
            {
                return content;
            }

            var insertion = new StringBuilder();
            foreach (var ns in missing.OrderBy(n => n, StringComparer.Ordinal))
            {
                insertion.Append("using ").Append(ns).Append(';').AppendLine();
            }

            var namespaceIndex = content.IndexOf("namespace", StringComparison.Ordinal);
            if (namespaceIndex < 0)
            {
                return insertion.AppendLine().Append(content).ToString();
            }

            var usingBlockMatch = Regex.Match(content, @"^(using\s+[^\n]+\n)+", RegexOptions.Multiline);
            if (usingBlockMatch.Success)
            {
                return content.Insert(usingBlockMatch.Index + usingBlockMatch.Length, insertion.ToString());
            }

            insertion.AppendLine();
            return content.Insert(namespaceIndex, insertion.ToString());
        }

        private static string ReplaceRegion(string content, string regionBlock)
        {
            var regex = new Regex(@"#region\s+脚本工具生成(?:的)?代码[\s\S]*?#endregion", RegexOptions.Multiline);
            if (regex.IsMatch(content))
            {
                return regex.Replace(content, regionBlock, 1);
            }

            var classMatch = Regex.Match(content, @"class\s+[^\{]+\{", RegexOptions.Singleline);
            if (!classMatch.Success)
            {
                return content;
            }

            return content.Insert(classMatch.Index + classMatch.Length, Environment.NewLine + regionBlock + Environment.NewLine);
        }

        private static string BuildNewScriptContent(string objectName, string className, GenerationResult generation)
        {
            var usings = generation.RequiredUsings
                .OrderBy(u => u, StringComparer.Ordinal)
                .Select(u => $"using {u};")
                .ToList();

            var usingBlock = usings.Count > 0
                ? string.Join("\n", usings) + "\n\n"
                : string.Empty;

            return string.Format(NewScriptTemplate, usingBlock, objectName, className, generation.RegionBlock + "\n");
        }

        private static string BuildRegionBlock(List<FieldBinding> fields)
        {
            // Build field declarations and assignments as joined strings, preserving original indentation.
            string fieldDeclBlock = string.Empty;
            string assignBlock = string.Empty;

            if (fields.Count > 0)
            {
                var declLines = fields.Select(f => $"        private {f.TypeName} {f.FieldName};");
                fieldDeclBlock = "\n" + string.Join("\n", declLines) + "\n\n";

                var assignLines = fields.Select(f => $"            {f.Assignment}");
                assignBlock = "\n" + string.Join("\n", assignLines) + "\n";
            }
            else
            {
                fieldDeclBlock = "\n";
            }

            return string.Format(RegionTemplate, fieldDeclBlock, assignBlock);
        }

        private static FieldBinding CreateBinding(ReferenceCollectorData entry, FieldCodeStyle codeStyle, HashSet<string> fieldNames)
        {
            var key = entry.key?.Trim();
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            var fieldName = SanitizeFieldName(key, codeStyle, fieldNames);
            var target = entry.gameObject;
            if (target == null)
            {
                return null;
            }

            var projectRule = ReferenceCollectorRuleService.FindFirstMatchingRule(key);
            if (target is GameObject go)
            {
                return CreateBindingForGameObject(fieldName, key, go, projectRule);
            }

            var objectType = target.GetType();
            var typeName = objectType.Name;
            var ns = objectType.Namespace ?? string.Empty;
            return new FieldBinding(fieldName, typeName, ns, fieldName + " = _referenceCollector.GetObject(\"" + key + "\") as " + typeName + ";");
        }

        private static FieldBinding CreateBindingForGameObject(string fieldName, string key, GameObject go, ReferenceCollectorRule rule)
        {
            if (rule != null)
            {
                var resolvedRule = ReferenceCollectorRuleService.ResolveRule(rule);
                if (resolvedRule.IsValid && resolvedRule.ComponentType != typeof(GameObject))
                {
                    var component = go.GetComponent(resolvedRule.ComponentType);
                    var componentType = component != null ? component.GetType() : resolvedRule.ComponentType;
                    var typeName = componentType.Name;
                    var ns = componentType.Namespace ?? string.Empty;
                    return new FieldBinding(fieldName, typeName, ns, fieldName + " = _referenceCollector.Get<" + typeName + ">(\"" + key + "\");");
                }
            }

            return new FieldBinding(fieldName, "GameObject", "UnityEngine", fieldName + " = _referenceCollector.GetObject(\"" + key + "\") as GameObject;");
        }

        private static string SanitizeClassName(string name)
        {
            var sanitized = Regex.Replace(name ?? string.Empty, "[^a-zA-Z0-9_]", string.Empty);
            if (string.IsNullOrEmpty(sanitized))
            {
                sanitized = "GeneratedView";
            }

            if (char.IsDigit(sanitized[0]))
            {
                sanitized = "_" + sanitized;
            }

            return sanitized;
        }

        private static string SanitizeFieldName(string key, FieldCodeStyle codeStyle, HashSet<string> usedNames)
        {
            var sanitized = Regex.Replace(key ?? string.Empty, "[^a-zA-Z0-9_]", "_");
            if (string.IsNullOrEmpty(sanitized))
            {
                sanitized = "field";
            }
            else if (char.IsDigit(sanitized[0]))
            {
                sanitized = "_" + sanitized;
            }

            sanitized = ApplyCodeStyle(sanitized, codeStyle);
            var unique = sanitized;
            var index = 1;
            while (usedNames.Contains(unique))
            {
                unique = sanitized + "_" + index++;
            }

            usedNames.Add(unique);
            return unique;
        }

        private static string ApplyCodeStyle(string name, FieldCodeStyle codeStyle)
        {
            switch (codeStyle)
            {
                case FieldCodeStyle.MPrefix:
                    if (name.StartsWith("m_", StringComparison.Ordinal))
                    {
                        return name;
                    }

                    if (name.StartsWith("_", StringComparison.Ordinal))
                    {
                        return "m" + name;
                    }

                    return "m_" + name;
                default:
                    if (name.StartsWith("_", StringComparison.Ordinal))
                    {
                        return name;
                    }

                    if (name.StartsWith("m_", StringComparison.Ordinal))
                    {
                        return "_" + name.Substring(2);
                    }

                    return "_" + name;
            }
        }

        private static string GetAbsolutePath(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }

        private enum FieldCodeStyle
        {
            UnderscorePrefix = 0,
            MPrefix = 1,
        }

        private sealed class FieldBinding
        {
            internal FieldBinding(string fieldName, string typeName, string ns, string assignment)
            {
                FieldName = fieldName;
                TypeName = typeName;
                Namespace = ns;
                Assignment = assignment;
            }

            internal string FieldName { get; }

            internal string TypeName { get; }

            internal string Namespace { get; }

            internal string Assignment { get; }
        }

        private sealed class GenerationResult
        {
            internal GenerationResult(string regionBlock, HashSet<string> requiredUsings)
            {
                RegionBlock = regionBlock;
                RequiredUsings = requiredUsings;
            }

            internal string RegionBlock { get; }

            internal HashSet<string> RequiredUsings { get; }
        }

        private static class ScriptGeneratorSettingProxy
        {
            private static readonly Type SettingType = ResolveSettingType();

            internal static FieldCodeStyle GetCodeStyle()
            {
                var value = Invoke("GetCodeStyle");
                if (value == null)
                {
                    return FieldCodeStyle.UnderscorePrefix;
                }

                return (FieldCodeStyle)Convert.ToInt32(value);
            }

            internal static string GetCodePath()
            {
                string path = null;
#if UNITY_EDITOR
                try
                {
                    path = UnityEditor.EditorPrefs.GetString("GT.ReferenceCollector.CodePath", string.Empty);
                    if (!string.IsNullOrEmpty(path)) return path;
                }
                catch { }
#endif
                path = Invoke("GetCodePath") as string;
                return path ?? string.Empty;
            }

            private static object Invoke(string methodName)
            {
                if (SettingType == null)
                {
                    return null;
                }

                var method = SettingType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
                return method?.Invoke(null, null);
            }

            private static Type ResolveSettingType()
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var type = assembly.GetType("EF.Editor.UI.ScriptGeneratorSetting");
                    if (type != null)
                    {
                        return type;
                    }
                }

                return null;
            }
        }
    }
}
