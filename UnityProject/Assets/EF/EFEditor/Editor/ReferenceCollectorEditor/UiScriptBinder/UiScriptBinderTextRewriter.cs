using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace GT
{
    /// <summary>
    /// UiScriptBinder 中所有纯文本改写逻辑的集合，不依赖 UnityEditor / UnityEngine，便于单测。
    /// </summary>
    public static class UiScriptBinderTextRewriter
    {
        /// <summary>
        /// region 标识符正则，匹配 `#region 自动生成 ... #endregion`。
        /// </summary>
        private static readonly Regex RegionRegex = new Regex(
            @"#region\s+自动生成[\s\S]*?#endregion",
            RegexOptions.Multiline);

        /// <summary>
        /// 类首大括号识别正则。
        /// </summary>
        private static readonly Regex ClassOpenRegex = new Regex(
            @"class\s+\w+(?:\s*:\s*[^\{]+)?\s*\{",
            RegexOptions.Multiline);

        /// <summary>
        /// 一条字段定义的描述。
        /// </summary>
        public sealed class FieldDescriptor
        {
            public FieldDescriptor(string key, string fieldName, string typeName, string namespaceName)
            {
                Key = key;
                FieldName = fieldName;
                TypeName = typeName;
                NamespaceName = namespaceName;
            }

            public string Key { get; }

            public string FieldName { get; }

            public string TypeName { get; }

            public string NamespaceName { get; }
        }

        /// <summary>
        /// 按 key 清洗成合法 C# 字段名：保留字母数字、特殊字符替换为 `_`、数字开头补 `_`、首字母小写、加 `_` 前缀，并保证集合内不重名。
        /// </summary>
        public static string SanitizeFieldName(string key, ISet<string> usedNames)
        {
            if (usedNames == null)
            {
                throw new ArgumentNullException(nameof(usedNames));
            }

            var sanitized = Regex.Replace(key ?? string.Empty, "[^a-zA-Z0-9_]", "_");
            if (string.IsNullOrEmpty(sanitized))
            {
                sanitized = "field";
            }

            if (char.IsDigit(sanitized[0]))
            {
                sanitized = "_" + sanitized;
            }

            if (sanitized.Length > 0 && char.IsUpper(sanitized[0]))
            {
                sanitized = char.ToLowerInvariant(sanitized[0]) + sanitized.Substring(1);
            }

            var baseName = sanitized.StartsWith("_", StringComparison.Ordinal) ? sanitized : "_" + sanitized;
            var unique = baseName;
            var index = 1;
            while (usedNames.Contains(unique))
            {
                unique = baseName + "_" + index++;
            }

            usedNames.Add(unique);
            return unique;
        }

        /// <summary>
        /// 按字典序补充缺失的 using 声明；命名空间为空、`System`、或已存在的不再写入。
        /// </summary>
        public static string EnsureUsings(string content, IEnumerable<string> requiredUsings)
        {
            if (requiredUsings == null)
            {
                return content;
            }

            var needed = requiredUsings
                .Where(ns => !string.IsNullOrWhiteSpace(ns))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (needed.Count == 0)
            {
                return content;
            }

            var existing = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match match in Regex.Matches(content, @"^\s*using\s+([^;\s]+)\s*;", RegexOptions.Multiline))
            {
                existing.Add(match.Groups[1].Value);
            }

            var missing = needed.Where(ns => !existing.Contains(ns))
                .OrderBy(ns => ns, StringComparer.Ordinal)
                .ToList();
            if (missing.Count == 0)
            {
                return content;
            }

            var insertion = new StringBuilder();
            foreach (var ns in missing)
            {
                insertion.Append("using ").Append(ns).Append(';').Append('\n');
            }

            var usingBlockMatch = Regex.Match(content, @"(?:^\s*using\s+[^\n]+\n)+", RegexOptions.Multiline);
            if (usingBlockMatch.Success)
            {
                return content.Insert(usingBlockMatch.Index + usingBlockMatch.Length, insertion.ToString());
            }

            var namespaceIndex = content.IndexOf("namespace", StringComparison.Ordinal);
            if (namespaceIndex < 0)
            {
                return insertion.Append('\n').Append(content).ToString();
            }

            insertion.Append('\n');
            return content.Insert(namespaceIndex, insertion.ToString());
        }

        /// <summary>
        /// 按字段列表渲染 `#region 自动生成 ... #endregion` 块，使用 8 空格缩进（类内字段层级）。
        /// </summary>
        public static string BuildAutoRegionBlock(IEnumerable<FieldDescriptor> fields, string indent = "        ")
        {
            if (indent == null)
            {
                indent = string.Empty;
            }

            var builder = new StringBuilder();
            builder.Append(indent).Append("#region 自动生成").Append('\n');

            if (fields != null)
            {
                foreach (var field in fields)
                {
                    if (field == null)
                    {
                        continue;
                    }

                    builder.Append(indent)
                        .Append("[UHubBind(\"")
                        .Append(EscapeForString(field.Key))
                        .Append("\")] private ")
                        .Append(field.TypeName)
                        .Append(' ')
                        .Append(field.FieldName)
                        .Append(';')
                        .Append('\n');
                }
            }

            builder.Append(indent).Append("#endregion");
            return builder.ToString();
        }

        /// <summary>
        /// 整块替换或首次插入 region：找到则整体替换为新内容；找不到则插入到类首大括号后。
        /// </summary>
        public static string ReplaceOrInsertRegion(string content, string regionBlock)
        {
            if (string.IsNullOrEmpty(regionBlock))
            {
                throw new ArgumentException("regionBlock 不能为空", nameof(regionBlock));
            }

            if (RegionRegex.IsMatch(content))
            {
                return RegionRegex.Replace(content, regionBlock, 1);
            }

            var classMatch = ClassOpenRegex.Match(content);
            if (!classMatch.Success)
            {
                return content;
            }

            var insertPos = classMatch.Index + classMatch.Length;
            return content.Insert(insertPos, "\n" + regionBlock + "\n");
        }

        /// <summary>
        /// 在 `OnInitialize()` 方法体内补充 `UHub.Initialize();` 调用：已有则跳过；无 OnInitialize 则不动。
        /// 返回新内容 + 是否找到 OnInitialize 的标志。
        /// </summary>
        public static UHubInitializeInjectResult EnsureUHubInitializeCall(string content)
        {
            var methodMatch = Regex.Match(
                content,
                @"(?<signature>(?:protected|public|private|internal)?\s*(?:override\s+|virtual\s+)*void\s+OnInitialize\s*\(\s*\)\s*)\{",
                RegexOptions.Multiline);
            if (!methodMatch.Success)
            {
                return new UHubInitializeInjectResult(content, methodFound: false, alreadyPresent: false);
            }

            var braceStart = methodMatch.Index + methodMatch.Length - 1;
            var braceEnd = FindMatchingClosingBrace(content, braceStart);
            if (braceEnd < 0)
            {
                return new UHubInitializeInjectResult(content, methodFound: true, alreadyPresent: false);
            }

            var body = content.Substring(braceStart + 1, braceEnd - braceStart - 1);
            if (Regex.IsMatch(body, @"\bUHub\s*\.\s*Initialize\s*\("))
            {
                return new UHubInitializeInjectResult(content, methodFound: true, alreadyPresent: true);
            }

            var baseCallMatch = Regex.Match(body, @"^(?<indent>[ \t]*)base\s*\.\s*OnInitialize\s*\(\s*\)\s*;\s*$\n?", RegexOptions.Multiline);
            string indent;
            int insertionInBody;
            if (baseCallMatch.Success)
            {
                indent = baseCallMatch.Groups["indent"].Value;
                insertionInBody = baseCallMatch.Index + baseCallMatch.Length;
            }
            else
            {
                indent = InferLeadingIndent(body, fallback: "            ");
                insertionInBody = 0;
                if (!body.StartsWith("\n", StringComparison.Ordinal))
                {
                    body = "\n" + body;
                    braceEnd += 1;
                    insertionInBody = 1;
                }
            }

            var injection = indent + "UHub.Initialize();\n";
            var newBody = body.Insert(insertionInBody, injection);
            var newContent = content.Substring(0, braceStart + 1) + newBody + content.Substring(braceEnd);
            return new UHubInitializeInjectResult(newContent, methodFound: true, alreadyPresent: false);
        }

        /// <summary>
        /// 扫描类体内 region 外的字段声明，返回已存在的字段名集合（用于跳过冲突项）。
        /// 注释中（// 单行 / /* 多行 */）出现的字段声明 SHALL 被忽略，避免被误判为已有字段。
        /// </summary>
        public static HashSet<string> DetectExternalFields(string content)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            var withoutRegion = RegionRegex.Replace(content, string.Empty);
            var withoutComments = StripComments(withoutRegion);

            var fieldRegex = new Regex(
                @"(?:^|\s)(?:private|protected|public|internal)(?:\s+(?:readonly|static))*\s+[\w<>\[\],\s\.]+?\s+(?<name>_[A-Za-z0-9_]+|[A-Za-z][A-Za-z0-9_]*)\s*(?:=|;)",
                RegexOptions.Multiline);

            foreach (Match match in fieldRegex.Matches(withoutComments))
            {
                var name = match.Groups["name"].Value;
                if (!string.IsNullOrEmpty(name))
                {
                    result.Add(name);
                }
            }

            return result;
        }

        /// <summary>
        /// 按字段名删除整行字段声明（含同行属性如 `[SerializeField]`），保留前后换行结构。
        /// 仅删除匹配 access modifier + 类型 + 名字 + `;`/`=` 的行；同一字段名最多删 1 行。
        /// 不会删除独立行上的属性，残留属性由用户在 IDE 中按编译错误清理。
        /// </summary>
        public static string RemoveExternalFields(string content, IEnumerable<string> fieldNames)
        {
            if (string.IsNullOrEmpty(content) || fieldNames == null)
            {
                return content;
            }

            var targets = new HashSet<string>(fieldNames.Where(n => !string.IsNullOrEmpty(n)), StringComparer.Ordinal);
            if (targets.Count == 0)
            {
                return content;
            }

            var lines = content.Split('\n');
            var removed = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < lines.Length; i++)
            {
                if (removed.Count == targets.Count)
                {
                    break;
                }

                foreach (var name in targets)
                {
                    if (removed.Contains(name))
                    {
                        continue;
                    }

                    var pattern = @"^\s*(?:\[[^\]]*\]\s*)*(?:private|public|protected|internal)(?:\s+(?:readonly|static))*\s+\S[\w<>\[\],\s\.]*?\s+"
                                  + Regex.Escape(name)
                                  + @"\s*[=;]";
                    if (Regex.IsMatch(lines[i], pattern))
                    {
                        lines[i] = null;
                        removed.Add(name);
                        break;
                    }
                }
            }

            if (removed.Count == 0)
            {
                return content;
            }

            var builder = new StringBuilder();
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i] == null)
                {
                    continue;
                }

                builder.Append(lines[i]);
                if (i < lines.Length - 1)
                {
                    builder.Append('\n');
                }
            }

            return builder.ToString();
        }

        private static readonly Regex BlockCommentRegex = new Regex(@"/\*[\s\S]*?\*/", RegexOptions.Multiline);

        private static readonly Regex LineCommentRegex = new Regex(@"//[^\n]*", RegexOptions.Multiline);

        /// <summary>
        /// 移除 C# 单行与块注释，保留原始换行结构以避免位置偏移影响其它正则。
        /// 注：实现刻意简化，不处理字符串内出现的 `//` 或 `/*`，对字段扫描场景已足够。
        /// </summary>
        private static string StripComments(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                return content;
            }

            var stripped = BlockCommentRegex.Replace(content, string.Empty);
            stripped = LineCommentRegex.Replace(stripped, string.Empty);
            return stripped;
        }

        private static int FindMatchingClosingBrace(string content, int openBraceIndex)
        {
            int depth = 0;
            for (int i = openBraceIndex; i < content.Length; i++)
            {
                if (content[i] == '{')
                {
                    depth++;
                }
                else if (content[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        private static string InferLeadingIndent(string body, string fallback)
        {
            var match = Regex.Match(body, @"^(?<indent>[ \t]+)\S", RegexOptions.Multiline);
            return match.Success ? match.Groups["indent"].Value : fallback;
        }

        private static string EscapeForString(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            return text.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        /// <summary>
        /// `EnsureUHubInitializeCall` 的返回值。
        /// </summary>
        public sealed class UHubInitializeInjectResult
        {
            public UHubInitializeInjectResult(string content, bool methodFound, bool alreadyPresent)
            {
                Content = content;
                MethodFound = methodFound;
                AlreadyPresent = alreadyPresent;
            }

            public string Content { get; }

            public bool MethodFound { get; }

            public bool AlreadyPresent { get; }
        }
    }
}
