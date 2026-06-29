#if UNITY_EDITOR
using System.IO;
using NUnit.Framework;
using UnityEditor;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RogueCard.Editor.DraftWorkbench.Tests
{
    /// <summary>
    /// <see cref="UguiElementFactory"/> 的 EditMode 单元测试，覆盖 Image、Button、TextMeshProUGUI 和容器节点创建。
    /// </summary>
    [TestFixture]
    public class UguiElementFactoryTests
    {
        private GameObject _parent;

        /// <summary>
        /// 每个测试前创建一个带 RectTransform 的临时父节点。
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _parent = new GameObject("Parent", typeof(RectTransform));
        }

        /// <summary>
        /// 每个测试后销毁临时父节点。
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (_parent != null)
            {
                Object.DestroyImmediate(_parent);
                _parent = null;
            }
        }

        /// <summary>
        /// CreateImage 应创建 Image 组件并精确写入 RectTransform 布局值。
        /// </summary>
        [Test]
        public void CreateImage_SetsCorrectRectValues()
        {
            var desc = CreateDescriptor(
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(384f, -688f),
                new Vector2(768f, 1376f));
            desc.ComponentTypeNames.Add("UnityEngine.UI.Image");

            var image = UguiElementFactory.CreateImage("Background", _parent.transform, desc);
            var rect = image.GetComponent<RectTransform>();

            Assert.IsNotNull(image, "应创建 Image 组件");
            Assert.AreEqual(desc.AnchorMin, rect.anchorMin, "anchorMin 应匹配描述符");
            Assert.AreEqual(desc.AnchorMax, rect.anchorMax, "anchorMax 应匹配描述符");
            Assert.AreEqual(desc.AnchoredPosition, rect.anchoredPosition, "anchoredPosition 应匹配描述符");
            Assert.AreEqual(desc.SizeDelta, rect.sizeDelta, "sizeDelta 应匹配描述符");
        }

        /// <summary>
        /// CreateImage 不应保留 Unity 默认的 100x100 尺寸。
        /// </summary>
        [Test]
        public void CreateImage_DefaultSize_Not100x100()
        {
            var desc = CreateDescriptor(Vector2.zero, Vector2.one, Vector2.zero, new Vector2(320f, 240f));
            desc.ComponentTypeNames.Add("UnityEngine.UI.Image");

            var image = UguiElementFactory.CreateImage("Panel", _parent.transform, desc);
            var rect = image.GetComponent<RectTransform>();

            Assert.AreNotEqual(new Vector2(100f, 100f), rect.sizeDelta, "不应保留默认 100x100");
            Assert.AreEqual(new Vector2(320f, 240f), rect.sizeDelta, "应使用描述符尺寸");
        }

        /// <summary>
        /// CreateImage 应能通过 Image-To-UI 的 assetGuid 解析 Sprite，并按 nineSlice 标记设置 Image.Type。
        /// </summary>
        [Test]
        public void CreateImage_ResolvesSpriteByGuidAndAppliesSlicedType()
        {
            const string assetPath = "Assets/Temp/DraftWorkbenchTests/FactorySprite.png";
            Sprite expectedSprite = CreateTemporarySpriteAsset(assetPath);
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            var desc = CreateDescriptor(Vector2.zero, Vector2.one, Vector2.zero, new Vector2(64f, 64f));
            desc.ComponentTypeNames.Add("UnityEngine.UI.Image");
            desc.Visuals = new UiNodeVisuals
            {
                SpriteGuid = guid,
                UseSlicedImage = true
            };

            try
            {
                var image = UguiElementFactory.CreateImage("Icon", _parent.transform, desc);

                Assert.AreEqual(expectedSprite, image.sprite, "应通过 GUID 解析并应用 Sprite");
                Assert.AreEqual(Image.Type.Sliced, image.type, "nineSlice 应转换为 Sliced Image");
                Assert.AreEqual(assetPath, desc.Visuals.SpriteAssetPath, "解析成功后应回填 SpriteAssetPath");
                Assert.AreEqual(guid, desc.Visuals.SpriteGuid, "GUID 应保持不变");
            }
            finally
            {
                AssetDatabase.DeleteAsset(assetPath);
                AssetDatabase.DeleteAsset("Assets/Temp/DraftWorkbenchTests");
            }
        }

        /// <summary>
        /// CreateButton 应同时创建 Image 和 Button 组件。
        /// </summary>
        [Test]
        public void CreateButton_HasImageAndButtonComponents()
        {
            var desc = CreateDescriptor(Vector2.zero, Vector2.zero, new Vector2(80f, 30f), new Vector2(160f, 60f));
            desc.ComponentTypeNames.Add("UnityEngine.UI.Image");
            desc.ComponentTypeNames.Add("UnityEngine.UI.Button");

            var button = UguiElementFactory.CreateButton("StartButton", _parent.transform, desc);

            Assert.IsNotNull(button.GetComponent<Image>(), "Button 节点应有 Image");
            Assert.IsNotNull(button.GetComponent<Button>(), "Button 节点应有 Button");
        }

        /// <summary>
        /// CreateText 应创建 TextMeshProUGUI 并写入文本内容、字号、颜色和对齐。
        /// </summary>
        [Test]
        public void CreateText_CreatesTextMeshProUGUI_WithContent()
        {
            var desc = CreateDescriptor(Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(200f, 60f));
            desc.ComponentTypeNames.Add("TMPro.TextMeshProUGUI");
            desc.Visuals = new UiNodeVisuals
            {
                TextContent = "开始游戏",
                FontSize = 48,
                NodeColor = Color.yellow,
                HasTextAlignment = true,
                TextAlignment = TextAnchor.MiddleCenter
            };

            var text = UguiElementFactory.CreateText("StartText", _parent.transform, desc);

            Assert.IsNotNull(text, "应创建 TextMeshProUGUI");
            Assert.AreEqual("开始游戏", text.text, "文本内容应匹配");
            Assert.AreEqual(48f, text.fontSize, "字号应匹配");
            Assert.AreEqual(Color.yellow, text.color, "颜色应匹配");
            Assert.AreEqual(TextAlignmentOptions.Center, text.alignment, "对齐应转换为 TMP 居中");
        }

        /// <summary>
        /// CreateText 在 TextContent 为 null 时不应抛出异常。
        /// </summary>
        [Test]
        public void CreateText_NullContent_DoesNotThrow()
        {
            var desc = CreateDescriptor(Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(100f, 40f));
            desc.ComponentTypeNames.Add("TMPro.TextMeshProUGUI");
            desc.Visuals = new UiNodeVisuals { FontSize = 24 };

            Assert.DoesNotThrow(() => UguiElementFactory.CreateText("EmptyText", _parent.transform, desc));
        }

        /// <summary>
        /// CreateContainer 应只创建 RectTransform，不添加 Image、Button 或 TextMeshProUGUI。
        /// </summary>
        [Test]
        public void CreateContainer_OnlyRectTransform_NoExtraComponents()
        {
            var desc = CreateDescriptor(Vector2.zero, Vector2.one, Vector2.zero, new Vector2(768f, 1376f));

            var rect = UguiElementFactory.CreateContainer("RootContainer", _parent.transform, desc);

            Assert.IsNotNull(rect, "容器应有 RectTransform");
            Assert.IsNull(rect.GetComponent<Image>(), "容器不应有 Image");
            Assert.IsNull(rect.GetComponent<Button>(), "容器不应有 Button");
            Assert.IsNull(rect.GetComponent<TextMeshProUGUI>(), "容器不应有 TextMeshProUGUI");
        }

        /// <summary>
        /// ApplyRectTransform 应能覆盖已有默认 100x100 节点的所有布局值。
        /// </summary>
        [Test]
        public void ApplyRectTransform_OverridesDefaultRectValues()
        {
            var go = new GameObject("DefaultRect", typeof(RectTransform));
            go.transform.SetParent(_parent.transform, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(100f, 100f);

            var desc = CreateDescriptor(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(176f, -63f), new Vector2(352f, 126f));
            UguiElementFactory.ApplyRectTransform(rect, desc);

            Assert.AreEqual(desc.AnchorMin, rect.anchorMin, "anchorMin 应被覆盖");
            Assert.AreEqual(desc.AnchorMax, rect.anchorMax, "anchorMax 应被覆盖");
            Assert.AreEqual(desc.AnchoredPosition, rect.anchoredPosition, "anchoredPosition 应被覆盖");
            Assert.AreEqual(desc.SizeDelta, rect.sizeDelta, "sizeDelta 应被覆盖");
        }

        /// <summary>
        /// 创建测试用节点描述符。
        /// </summary>
        private static UguiNodeDescriptor CreateDescriptor(Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            return new UguiNodeDescriptor
            {
                AnchorMin = anchorMin,
                AnchorMax = anchorMax,
                AnchoredPosition = anchoredPosition,
                SizeDelta = sizeDelta,
            };
        }

        /// <summary>
        /// 创建临时单 Sprite 图片资源。
        /// </summary>
        private static Sprite CreateTemporarySpriteAsset(string assetPath)
        {
            string fullPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    texture.SetPixel(x, y, Color.white);
                }
            }
            texture.Apply();
            File.WriteAllBytes(fullPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            Assert.IsNotNull(importer, "临时测试贴图应有 TextureImporter");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            Assert.IsNotNull(sprite, "临时 Sprite 资源应导入成功");
            return sprite;
        }
    }
}
#endif
