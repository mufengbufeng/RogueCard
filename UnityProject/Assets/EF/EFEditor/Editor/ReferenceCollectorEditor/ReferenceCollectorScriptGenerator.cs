using System;
using System.Collections;
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
        private const string RegionWithParticle = "#region 脚本工具生成的代码";
        private const string RegionEnd = "#endregion";
        // Template strings to build code via replacements instead of AppendLine chains.
        // Keep indentation consistent with generated code.
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
        private static readonly Dictionary<string, Type> TypeCacheByName = new Dictionary<string, Type>(StringComparer.Ordinal);

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
            var rules = ScriptGeneratorSettingProxy.GetRules();
            var codeStyle = ScriptGeneratorSettingProxy.GetCodeStyle();
            var requiredUsings = new HashSet<string>(StringComparer.Ordinal) { "EF", "UnityEngine", "GT.Runtime" };
            var fieldNames = new HashSet<string>(StringComparer.Ordinal);
            var fields = new List<FieldBinding>();
            var matchMode = ScriptGeneratorSettingProxy.GetRuleMatchMode();

            if (collector.data != null)
            {
                foreach (var entry in collector.data.Where(d => !string.IsNullOrEmpty(d.key)).OrderBy(d => d.key, StringComparer.Ordinal))
                {
                    var binding = CreateBinding(entry, codeStyle, rules, fieldNames, matchMode);
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
            // Build using block with a trailing blank line when non-empty.
            var usings = generation.RequiredUsings
                .OrderBy(u => u, StringComparer.Ordinal)
                .Select(u => $"using {u};")
                .ToList();

            var usingBlock = usings.Count > 0
                ? string.Join("\n", usings) + "\n\n"
                : string.Empty;

            // Plug parts into the template: {0}=usings, {1}=objectName, {2}=className, {3}=region block
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
                fieldDeclBlock = "\n" + string.Join("\n", declLines) + "\n\n"; // blank line before and after decls

                var assignLines = fields.Select(f => $"            {f.Assignment}");
                assignBlock = "\n" + string.Join("\n", assignLines) + "\n"; // blank line before assignments
            }
            else
            {
                fieldDeclBlock = "\n"; // keep single blank line to match previous output
            }

            return string.Format(RegionTemplate, fieldDeclBlock, assignBlock);
        }

        private static FieldBinding CreateBinding(ReferenceCollectorData entry, FieldCodeStyle codeStyle, List<Rule> rules, HashSet<string> fieldNames, RuleMatchMode matchMode)
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

            var rule = FindRule(rules, key, matchMode);
            if (target is GameObject go)
            {
                return CreateBindingForGameObject(fieldName, key, go, rule);
            }

            var objectType = target.GetType();
            var typeName = objectType.Name;
            var ns = objectType.Namespace ?? string.Empty;
            return new FieldBinding(fieldName, typeName, ns, fieldName + " = _referenceCollector.GetObject(\"" + key + "\") as " + typeName + ";");
        }

        private static FieldBinding CreateBindingForGameObject(string fieldName, string key, GameObject go, Rule rule)
        {
            if (rule != null && !string.IsNullOrEmpty(rule.ComponentName) && !string.Equals(rule.ComponentName, "GameObject", StringComparison.Ordinal))
            {
                var component = go.GetComponent(rule.ComponentName);
                if (component != null)
                {
                    var componentType = component.GetType();
                    var typeName = componentType.Name;
                    var ns = componentType.Namespace ?? string.Empty;
                    return new FieldBinding(fieldName, typeName, ns, fieldName + " = _referenceCollector.Get<" + typeName + ">(\"" + key + "\");");
                }

                var resolvedType = ResolveType(rule.ComponentName);
                if (resolvedType != null)
                {
                    var typeName = resolvedType.Name;
                    var ns = resolvedType.Namespace ?? string.Empty;
                    return new FieldBinding(fieldName, typeName, ns, fieldName + " = _referenceCollector.Get<" + typeName + ">(\"" + key + "\");");
                }

                var guessedNamespace = GuessNamespace(rule.ComponentName);
                return new FieldBinding(fieldName, rule.ComponentName, guessedNamespace, fieldName + " = _referenceCollector.Get<" + rule.ComponentName + ">(\"" + key + "\");");
            }

            return new FieldBinding(fieldName, "GameObject", "UnityEngine", fieldName + " = _referenceCollector.GetObject(\"" + key + "\") as GameObject;");
        }

        private static string GuessNamespace(string typeName)
        {
            var resolved = ResolveType(typeName);
            return resolved != null ? resolved.Namespace ?? string.Empty : string.Empty;
        }

        private static Type ResolveType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                return null;
            }

            if (TypeCacheByName.TryGetValue(typeName, out var cached))
            {
                return cached;
            }

            var type = TypeCache.GetTypesDerivedFrom<UnityEngine.Object>().FirstOrDefault(t => t.Name == typeName);
            TypeCacheByName[typeName] = type;
            return type;
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

        private enum RuleMatchMode
        {
            Prefix = 0,
            Regex = 1,
        }

        private sealed class Rule
        {
            internal Rule(string prefix, string componentName, bool isUiWidget)
            {
                Prefix = prefix;
                ComponentName = componentName;
                IsUIWidget = isUiWidget;
            }

            internal string Prefix { get; }

            internal string ComponentName { get; }

            internal bool IsUIWidget { get; }

            internal Regex CompiledRegex { get; set; }
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

            internal static List<Rule> GetRules()
            {
                var list = new List<Rule>();
                var raw = Invoke("GetScriptGenerateRule") as IEnumerable;
                if (raw == null)
                {
                    return list;
                }

                foreach (var item in raw)
                {
                    if (item == null)
                    {
                        continue;
                    }

                    var prefix = ReadString(item, "uiElementRegex");
                    var component = ReadString(item, "componentName");
                    var isWidget = ReadBool(item, "isUIWidget");
                    if (string.IsNullOrEmpty(prefix) || string.IsNullOrEmpty(component))
                    {
                        continue;
                    }

                    list.Add(new Rule(prefix, component, isWidget));
                }

                return list;
            }

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

            internal static RuleMatchMode GetRuleMatchMode()
            {
                // Prefer explicit setting from host settings if available
                var modeObj = Invoke("GetRuleMatchMode");
                if (modeObj != null)
                {
                    return (RuleMatchMode)Convert.ToInt32(modeObj);
                }

                var useRegexObj = Invoke("GetUseRegex");
                if (useRegexObj != null)
                {
                    var flag = false;
                    if (useRegexObj is bool b) flag = b;
                    else bool.TryParse(useRegexObj.ToString(), out flag);
                    return flag ? RuleMatchMode.Regex : RuleMatchMode.Prefix;
                }

#if UNITY_EDITOR
                // Fallback to EditorPrefs toggle
                try
                {
                    var val = UnityEditor.EditorPrefs.GetInt("GT.ReferenceCollector.RuleMatchMode", 0);
                    return (RuleMatchMode)val;
                }
                catch
                {
                    return RuleMatchMode.Prefix;
                }
#else
                return RuleMatchMode.Prefix;
#endif
            }

#if UNITY_EDITOR
            // Simple menu toggles to switch match mode from Unity menu
            [UnityEditor.MenuItem("GT/ScriptGen/Rule Match Mode/Prefix", priority = 1000)]
            private static void SetPrefixMode()
            {
                UnityEditor.EditorPrefs.SetInt("GT.ReferenceCollector.RuleMatchMode", (int)RuleMatchMode.Prefix);
            }

            [UnityEditor.MenuItem("GT/ScriptGen/Rule Match Mode/Prefix", true)]
            private static bool SetPrefixModeValidate()
            {
                UnityEditor.Menu.SetChecked("GT/ScriptGen/Rule Match Mode/Prefix", GetRuleMatchMode() == RuleMatchMode.Prefix);
                return true;
            }

            [UnityEditor.MenuItem("GT/ScriptGen/Rule Match Mode/Regex", priority = 1001)]
            private static void SetRegexMode()
            {
                UnityEditor.EditorPrefs.SetInt("GT.ReferenceCollector.RuleMatchMode", (int)RuleMatchMode.Regex);
            }

            [UnityEditor.MenuItem("GT/ScriptGen/Rule Match Mode/Regex", true)]
            private static bool SetRegexModeValidate()
            {
                UnityEditor.Menu.SetChecked("GT/ScriptGen/Rule Match Mode/Regex", GetRuleMatchMode() == RuleMatchMode.Regex);
                return true;
            }
#endif

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

            private static string ReadString(object instance, string memberName)
            {
                return ReadMember(instance, memberName) as string ?? string.Empty;
            }

            private static bool ReadBool(object instance, string memberName)
            {
                var value = ReadMember(instance, memberName);
                if (value is bool boolean)
                {
                    return boolean;
                }

                if (value != null && bool.TryParse(value.ToString(), out var parsed))
                {
                    return parsed;
                }

                return false;
            }

            private static object ReadMember(object instance, string memberName)
            {
                if (instance == null)
                {
                    return null;
                }

                var type = instance.GetType();
                var field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    return field.GetValue(instance);
                }

                var property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (property != null)
                {
                    return property.GetValue(instance);
                }

                return null;
            }
        }
        private static Rule FindRule(List<Rule> rules, string key, RuleMatchMode mode)
        {
            if (rules == null || rules.Count == 0 || string.IsNullOrEmpty(key))
            {
                return null;
            }

            if (mode == RuleMatchMode.Regex)
            {
                foreach (var r in rules)
                {
                    var rx = r.CompiledRegex ?? (r.CompiledRegex = CompileRegexSafe(r.Prefix));
                    if (rx != null && rx.IsMatch(key))
                    {
                        return r;
                    }
                }
                return null;
            }

            // Prefix match (original behavior)
            return rules.FirstOrDefault(r => key.StartsWith(r.Prefix, StringComparison.Ordinal));
        }

        private static Regex CompileRegexSafe(string pattern)
        {
            if (string.IsNullOrEmpty(pattern)) return null;
            try
            {
                return new Regex(pattern, RegexOptions.Compiled);
            }
            catch
            {
                return null;
            }
        }

    }
}
