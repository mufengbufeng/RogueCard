using System.Collections.Generic;
using NUnit.Framework;

namespace GT.Tests
{
    /// <summary>
    /// UiScriptBinderTextRewriter 的纯文本改写单测。
    /// </summary>
    public sealed class UiScriptBinderTextRewriterTests
    {
        private static UiScriptBinderTextRewriter.FieldDescriptor Field(string key, string fieldName, string typeName, string ns = "UnityEngine")
        {
            return new UiScriptBinderTextRewriter.FieldDescriptor(key, fieldName, typeName, ns);
        }

        // ---------- SanitizeFieldName ----------

        [Test]
        public void SanitizeFieldName_StartGameBtn_ProducesUnderscoreCamelCase()
        {
            var used = new HashSet<string>();
            var name = UiScriptBinderTextRewriter.SanitizeFieldName("StartGameBtn", used);
            Assert.AreEqual("_startGameBtn", name);
        }

        [Test]
        public void SanitizeFieldName_LowercaseKey_PrefixesUnderscoreOnly()
        {
            var used = new HashSet<string>();
            var name = UiScriptBinderTextRewriter.SanitizeFieldName("startGameBtn", used);
            Assert.AreEqual("_startGameBtn", name);
        }

        [Test]
        public void SanitizeFieldName_DigitStart_AddsLeadingUnderscore()
        {
            var used = new HashSet<string>();
            var name = UiScriptBinderTextRewriter.SanitizeFieldName("3rdPanel", used);
            Assert.AreEqual("__3rdPanel", name);
        }

        [Test]
        public void SanitizeFieldName_Collision_AppendsIndex()
        {
            var used = new HashSet<string>();
            UiScriptBinderTextRewriter.SanitizeFieldName("Foo", used);
            var second = UiScriptBinderTextRewriter.SanitizeFieldName("Foo", used);
            Assert.AreEqual("_foo_1", second);
        }

        // ---------- BuildAutoRegionBlock ----------

        [Test]
        public void BuildAutoRegionBlock_EmitsUHubBindAttributes()
        {
            var fields = new[]
            {
                Field("EndBtn", "_endBtn", "Button", "UnityEngine.UI"),
                Field("InfoText", "_infoText", "TextMeshProUGUI", "TMPro"),
            };

            var block = UiScriptBinderTextRewriter.BuildAutoRegionBlock(fields);

            StringAssert.Contains("#region 自动生成", block);
            StringAssert.Contains("[UHubBind(\"EndBtn\")] private Button _endBtn;", block);
            StringAssert.Contains("[UHubBind(\"InfoText\")] private TextMeshProUGUI _infoText;", block);
            StringAssert.Contains("#endregion", block);
        }

        [Test]
        public void BuildAutoRegionBlock_EmptyFields_StillProducesRegionMarkers()
        {
            var block = UiScriptBinderTextRewriter.BuildAutoRegionBlock(new UiScriptBinderTextRewriter.FieldDescriptor[0]);
            StringAssert.Contains("#region 自动生成", block);
            StringAssert.Contains("#endregion", block);
        }

        // ---------- ReplaceOrInsertRegion ----------

        [Test]
        public void ReplaceOrInsertRegion_NoExistingRegion_InsertsAfterClassBrace()
        {
            const string source =
                "namespace Demo {\n" +
                "    public class FooView : UIView\n" +
                "    {\n" +
                "        // body\n" +
                "    }\n" +
                "}\n";
            const string block = "        #region 自动生成\n        private int _x;\n        #endregion";

            var result = UiScriptBinderTextRewriter.ReplaceOrInsertRegion(source, block);

            StringAssert.Contains("#region 自动生成", result);
            StringAssert.Contains("private int _x;", result);
            var classOpen = result.IndexOf("class FooView", System.StringComparison.Ordinal);
            var bodyComment = result.IndexOf("// body", System.StringComparison.Ordinal);
            var region = result.IndexOf("#region 自动生成", System.StringComparison.Ordinal);
            Assert.IsTrue(classOpen < region && region < bodyComment, "region 必须在类开括号与原 body 之间");
        }

        [Test]
        public void ReplaceOrInsertRegion_ExistingRegion_ReplacedWholesale()
        {
            const string source =
                "public class FooView : UIView\n" +
                "{\n" +
                "    #region 自动生成\n" +
                "    private int _old1;\n" +
                "    private int _old2;\n" +
                "    #endregion\n" +
                "}\n";
            const string newBlock = "    #region 自动生成\n    private int _new;\n    #endregion";

            var result = UiScriptBinderTextRewriter.ReplaceOrInsertRegion(source, newBlock);

            StringAssert.Contains("private int _new;", result);
            StringAssert.DoesNotContain("_old1", result);
            StringAssert.DoesNotContain("_old2", result);
        }

        [Test]
        public void ReplaceOrInsertRegion_RepeatedReplace_IsIdempotent()
        {
            const string source =
                "public class FooView : UIView\n" +
                "{\n" +
                "}\n";
            const string block = "    #region 自动生成\n    private int _x;\n    #endregion";

            var once = UiScriptBinderTextRewriter.ReplaceOrInsertRegion(source, block);
            var twice = UiScriptBinderTextRewriter.ReplaceOrInsertRegion(once, block);
            var thrice = UiScriptBinderTextRewriter.ReplaceOrInsertRegion(twice, block);

            Assert.AreEqual(once, twice);
            Assert.AreEqual(twice, thrice);
            Assert.AreEqual(1, CountOccurrences(thrice, "#region 自动生成"));
            Assert.AreEqual(1, CountOccurrences(thrice, "private int _x;"));
        }

        // ---------- EnsureUHubInitializeCall ----------

        [Test]
        public void EnsureUHubInitializeCall_MissingCall_InsertsAfterBaseCall()
        {
            const string source =
                "public class FooView : UIView\n" +
                "{\n" +
                "    protected override void OnInitialize()\n" +
                "    {\n" +
                "        base.OnInitialize();\n" +
                "    }\n" +
                "}\n";

            var result = UiScriptBinderTextRewriter.EnsureUHubInitializeCall(source);

            Assert.IsTrue(result.MethodFound);
            Assert.IsFalse(result.AlreadyPresent);
            StringAssert.Contains("base.OnInitialize();", result.Content);
            StringAssert.Contains("UHub.Initialize();", result.Content);
            var baseIdx = result.Content.IndexOf("base.OnInitialize();", System.StringComparison.Ordinal);
            var uhubIdx = result.Content.IndexOf("UHub.Initialize();", System.StringComparison.Ordinal);
            Assert.Less(baseIdx, uhubIdx, "UHub.Initialize() 必须出现在 base.OnInitialize() 之后");
        }

        [Test]
        public void EnsureUHubInitializeCall_AlreadyPresent_NoChange()
        {
            const string source =
                "public class FooView : UIView\n" +
                "{\n" +
                "    protected override void OnInitialize()\n" +
                "    {\n" +
                "        base.OnInitialize();\n" +
                "        UHub.Initialize();\n" +
                "    }\n" +
                "}\n";

            var result = UiScriptBinderTextRewriter.EnsureUHubInitializeCall(source);

            Assert.IsTrue(result.MethodFound);
            Assert.IsTrue(result.AlreadyPresent);
            Assert.AreEqual(source, result.Content);
            Assert.AreEqual(1, CountOccurrences(result.Content, "UHub.Initialize();"));
        }

        [Test]
        public void EnsureUHubInitializeCall_NoOnInitializeMethod_LeavesContentUnchanged()
        {
            const string source =
                "public class FooView : UIView\n" +
                "{\n" +
                "    public void Foo() { }\n" +
                "}\n";

            var result = UiScriptBinderTextRewriter.EnsureUHubInitializeCall(source);

            Assert.IsFalse(result.MethodFound);
            Assert.IsFalse(result.AlreadyPresent);
            Assert.AreEqual(source, result.Content);
        }

        // ---------- EnsureUsings ----------

        [Test]
        public void EnsureUsings_MissingNamespaces_AddedAlphabetically()
        {
            const string source =
                "using System;\n" +
                "using UnityEngine;\n" +
                "\n" +
                "namespace Demo { }\n";

            var result = UiScriptBinderTextRewriter.EnsureUsings(source, new[] { "TMPro", "UnityEngine.UI", "EF.UI" });

            StringAssert.Contains("using TMPro;", result);
            StringAssert.Contains("using UnityEngine.UI;", result);
            StringAssert.Contains("using EF.UI;", result);

            var efIdx = result.IndexOf("using EF.UI;", System.StringComparison.Ordinal);
            var tmpIdx = result.IndexOf("using TMPro;", System.StringComparison.Ordinal);
            var uiIdx = result.IndexOf("using UnityEngine.UI;", System.StringComparison.Ordinal);
            Assert.Less(efIdx, tmpIdx);
            Assert.Less(tmpIdx, uiIdx);
        }

        [Test]
        public void EnsureUsings_AllAlreadyPresent_LeavesContentUnchanged()
        {
            const string source =
                "using EF.UI;\n" +
                "using UnityEngine;\n" +
                "\n" +
                "namespace Demo { }\n";

            var result = UiScriptBinderTextRewriter.EnsureUsings(source, new[] { "EF.UI", "UnityEngine" });
            Assert.AreEqual(source, result);
        }

        // ---------- DetectExternalFields ----------

        [Test]
        public void DetectExternalFields_RegionExcluded_ReturnsOnlyExternal()
        {
            const string source =
                "public class FooView : UIView\n" +
                "{\n" +
                "    #region 自动生成\n" +
                "    [UHubBind(\"EndBtn\")] private Button _endBtn;\n" +
                "    #endregion\n" +
                "\n" +
                "    private Button _customBtn;\n" +
                "    public int counter;\n" +
                "}\n";

            var external = UiScriptBinderTextRewriter.DetectExternalFields(source);

            CollectionAssert.Contains(external, "_customBtn");
            CollectionAssert.Contains(external, "counter");
            CollectionAssert.DoesNotContain(external, "_endBtn", "_endBtn 在 region 内不应被识别为 region 外字段");
        }

        [Test]
        public void DetectExternalFields_CommentedFields_AreIgnored()
        {
            const string source =
                "public class FooView : UIView\n" +
                "{\n" +
                "    #region 自动生成\n" +
                "    #endregion\n" +
                "\n" +
                "    // public Button _startGameBtn;\n" +
                "    // public TextMeshProUGUI _feedbackText;\n" +
                "    /* private Button _blockCommented; */\n" +
                "    private Button _realField;\n" +
                "}\n";

            var external = UiScriptBinderTextRewriter.DetectExternalFields(source);

            CollectionAssert.Contains(external, "_realField");
            CollectionAssert.DoesNotContain(external, "_startGameBtn", "单行注释内字段不应识别为外部字段");
            CollectionAssert.DoesNotContain(external, "_feedbackText", "单行注释内字段不应识别为外部字段");
            CollectionAssert.DoesNotContain(external, "_blockCommented", "块注释内字段不应识别为外部字段");
        }

        [Test]
        public void DetectExternalFields_CommentBlockSpanningMultipleLines_IsStripped()
        {
            const string source =
                "public class FooView : UIView\n" +
                "{\n" +
                "    /*\n" +
                "     * 历史字段示例：\n" +
                "     * private Button _legacyBtn;\n" +
                "     * private TextMeshProUGUI _legacyText;\n" +
                "     */\n" +
                "    private Button _currentBtn;\n" +
                "}\n";

            var external = UiScriptBinderTextRewriter.DetectExternalFields(source);

            CollectionAssert.Contains(external, "_currentBtn");
            CollectionAssert.DoesNotContain(external, "_legacyBtn");
            CollectionAssert.DoesNotContain(external, "_legacyText");
        }

        // ---------- RemoveExternalFields ----------

        [Test]
        public void RemoveExternalFields_PublicAndPrivate_RemovesMatchingLines()
        {
            const string source =
                "public class FooView : UIView\n" +
                "{\n" +
                "    public Button _startGameBtn;\n" +
                "    private TextMeshProUGUI _feedbackText;\n" +
                "    public TextMeshProUGUI _statusText;\n" +
                "}\n";

            var result = UiScriptBinderTextRewriter.RemoveExternalFields(
                source,
                new[] { "_startGameBtn", "_feedbackText" });

            StringAssert.DoesNotContain("_startGameBtn", result);
            StringAssert.DoesNotContain("_feedbackText", result);
            StringAssert.Contains("public TextMeshProUGUI _statusText;", result);
        }

        [Test]
        public void RemoveExternalFields_InlineSerializeField_AlsoRemoved()
        {
            const string source =
                "public class FooView : UIView\n" +
                "{\n" +
                "    [SerializeField] private Button _endBtn;\n" +
                "    private Button _customBtn;\n" +
                "}\n";

            var result = UiScriptBinderTextRewriter.RemoveExternalFields(source, new[] { "_endBtn" });

            StringAssert.DoesNotContain("_endBtn", result);
            StringAssert.DoesNotContain("[SerializeField]", result);
            StringAssert.Contains("private Button _customBtn;", result);
        }

        [Test]
        public void RemoveExternalFields_UnknownName_LeavesContentUnchanged()
        {
            const string source =
                "public class FooView : UIView\n" +
                "{\n" +
                "    private Button _existing;\n" +
                "}\n";

            var result = UiScriptBinderTextRewriter.RemoveExternalFields(source, new[] { "_nonExistent" });

            Assert.AreEqual(source, result);
        }

        [Test]
        public void RemoveExternalFields_OnlyRemovesFirstMatchPerName()
        {
            const string source =
                "public class FooView : UIView\n" +
                "{\n" +
                "    private Button _btn;\n" +
                "    private Button _btn;\n" +
                "}\n";

            var result = UiScriptBinderTextRewriter.RemoveExternalFields(source, new[] { "_btn" });

            Assert.AreEqual(1, CountOccurrences(result, "private Button _btn;"),
                "重复声明只删除第一条，避免误伤后续未预期的同名声明");
        }

        // ---------- Integrated scenario ----------

        [Test]
        public void IntegratedScenario_RepeatedGenerate_RegionStableNoDuplicateFields()
        {
            const string baseSource =
                "using UnityEngine;\n" +
                "\n" +
                "namespace Demo\n" +
                "{\n" +
                "    public class FooView : UIView\n" +
                "    {\n" +
                "        protected override void OnInitialize()\n" +
                "        {\n" +
                "            base.OnInitialize();\n" +
                "        }\n" +
                "    }\n" +
                "}\n";

            var fields = new[]
            {
                Field("EndBtn", "_endBtn", "Button", "UnityEngine.UI"),
                Field("InfoText", "_infoText", "TextMeshProUGUI", "TMPro"),
            };

            string Run(string input)
            {
                var step = UiScriptBinderTextRewriter.EnsureUsings(input, new[] { "EF.UI", "UnityEngine.UI", "TMPro" });
                var block = UiScriptBinderTextRewriter.BuildAutoRegionBlock(fields);
                step = UiScriptBinderTextRewriter.ReplaceOrInsertRegion(step, block);
                step = UiScriptBinderTextRewriter.EnsureUHubInitializeCall(step).Content;
                return step;
            }

            var first = Run(baseSource);
            var second = Run(first);
            var third = Run(second);

            Assert.AreEqual(first, second);
            Assert.AreEqual(second, third);
            Assert.AreEqual(1, CountOccurrences(third, "#region 自动生成"));
            Assert.AreEqual(1, CountOccurrences(third, "[UHubBind(\"EndBtn\")] private Button _endBtn;"));
            Assert.AreEqual(1, CountOccurrences(third, "UHub.Initialize();"));
        }

        private static int CountOccurrences(string source, string needle)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(needle))
            {
                return 0;
            }

            int count = 0;
            int index = 0;
            while ((index = source.IndexOf(needle, index, System.StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += needle.Length;
            }

            return count;
        }
    }
}
