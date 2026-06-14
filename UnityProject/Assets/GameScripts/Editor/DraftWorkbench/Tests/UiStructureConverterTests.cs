#if UNITY_EDITOR

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace RogueCard.Editor.DraftWorkbench.Tests
{
    /// <summary>
    /// <see cref="UiStructureConverter"/> 的 EditMode 单元测试，
    /// 覆盖单容器转换、对齐锚点计算、绝对定位 Y 轴翻转、
    /// 布局组映射、元素类型组件映射、未知类型警告、
    /// 名称去重、层级路径计算以及布局子节点默认锚点。
    /// </summary>
    [TestFixture]
    public class UiStructureConverterTests
    {
        private UiStructureConverter _converter;

        [SetUp]
        public void SetUp()
        {
            _converter = new UiStructureConverter();
        }

        // ──────────────────────── 1. 单容器转换 ────────────────────────

        /// <summary>
        /// 验证单容器结构能正确转换为 1 个描述符，
        /// 且类型、名称和尺寸均正确。
        /// </summary>
        [Test]
        public void Convert_SingleContainer_CreatesDescriptor()
        {
            var structure = new UiStructure
            {
                canvas = new UiCanvas { width = 1080, height = 1920 },
                root = new UiElement
                {
                    type = "container",
                    name = "root",
                    size = new UiSize { width = 1080, height = 1920 }
                }
            };

            List<ConvertedNode> result = _converter.Convert(structure, UiStructureConversionOptions.IncludeRootNode);

            Assert.AreEqual(1, result.Count, "应生成恰好 1 个描述符");
            var desc = result[0].Descriptor;
            Assert.AreEqual("root", desc.Name, "名称应为 root");
            // 容器无 align/vAlign/position → 拉伸模式
            Assert.AreEqual(Vector2.zero, desc.AnchorMin, "拉伸模式 AnchorMin 应为 (0,0)");
            Assert.AreEqual(Vector2.one, desc.AnchorMax, "拉伸模式 AnchorMax 应为 (1,1)");
            Assert.AreEqual(new Vector2(1080f, 1920f), desc.SizeDelta, "尺寸应为 1080x1920");
        }

        // ──────────────────────── 2. 居中对齐 ────────────────────────

        /// <summary>
        /// 验证 align:center + vAlign:middle 时，
        /// 锚点固定在 (0.5, 0.5) 且偏移为零。
        /// </summary>
        [Test]
        public void Convert_AlignCenter_SetsCenterAnchor()
        {
            var structure = new UiStructure
            {
                canvas = new UiCanvas { width = 1080, height = 1920 },
                root = new UiElement
                {
                    type = "container",
                    name = "root",
                    size = new UiSize { width = 1080, height = 1920 },
                    children = new List<UiElement>
                    {
                        new UiElement
                        {
                            type = "image",
                            name = "centered",
                            align = "center",
                            vAlign = "middle",
                            size = new UiSize { width = 300, height = 100 }
                        }
                    }
                }
            };

            List<ConvertedNode> result = _converter.Convert(structure, UiStructureConversionOptions.IncludeRootNode);

            // result[0] = root, result[1] = centered
            var child = result[1].Descriptor;
            Assert.AreEqual(new Vector2(0.5f, 0.5f), child.AnchorMin,
                "AnchorMin 应为 (0.5, 0.5)");
            Assert.AreEqual(new Vector2(0.5f, 0.5f), child.AnchorMax,
                "AnchorMax 应为 (0.5, 0.5)");
            Assert.AreEqual(Vector2.zero, child.AnchoredPosition,
                "居中对齐 AnchoredPosition 应为 (0,0)");
        }

        // ──────────────────────── 3. 左对齐 ────────────────────────

        /// <summary>
        /// 验证 align:left 时锚点 x=0，
        /// AnchoredPosition.x = width/2（左边缘偏移半个元素宽度）。
        /// </summary>
        [Test]
        public void Convert_AlignLeft_SetsLeftAnchor()
        {
            var structure = new UiStructure
            {
                canvas = new UiCanvas { width = 1080, height = 1920 },
                root = new UiElement
                {
                    type = "container",
                    name = "root",
                    size = new UiSize { width = 1080, height = 1920 },
                    children = new List<UiElement>
                    {
                        new UiElement
                        {
                            type = "image",
                            name = "leftAligned",
                            align = "left",
                            size = new UiSize { width = 200, height = 50 }
                        }
                    }
                }
            };

            List<ConvertedNode> result = _converter.Convert(structure, UiStructureConversionOptions.IncludeRootNode);
            var child = result[1].Descriptor;

            Assert.AreEqual(0f, child.AnchorMin.x, "AnchorMin.x 应为 0");
            Assert.AreEqual(0f, child.AnchorMax.x, "AnchorMax.x 应为 0");
            Assert.AreEqual(100f, child.AnchoredPosition.x, "AnchoredPosition.x 应为 100（200/2）");
        }

        // ──────────────────────── 4. 顶部对齐 ────────────────────────

        /// <summary>
        /// 验证 vAlign:top 时锚点 y=1，
        /// AnchoredPosition.y = -height/2（向下偏移半个元素高度）。
        /// </summary>
        [Test]
        public void Convert_VAlignTop_SetsTopAnchor()
        {
            var structure = new UiStructure
            {
                canvas = new UiCanvas { width = 1080, height = 1920 },
                root = new UiElement
                {
                    type = "container",
                    name = "root",
                    size = new UiSize { width = 1080, height = 1920 },
                    children = new List<UiElement>
                    {
                        new UiElement
                        {
                            type = "image",
                            name = "topAligned",
                            vAlign = "top",
                            size = new UiSize { width = 300, height = 100 }
                        }
                    }
                }
            };

            List<ConvertedNode> result = _converter.Convert(structure, UiStructureConversionOptions.IncludeRootNode);
            var child = result[1].Descriptor;

            Assert.AreEqual(1f, child.AnchorMin.y, "AnchorMin.y 应为 1");
            Assert.AreEqual(1f, child.AnchorMax.y, "AnchorMax.y 应为 1");
            Assert.AreEqual(-50f, child.AnchoredPosition.y, "AnchoredPosition.y 应为 -50（100/2）");
        }

        // ──────────────────────── 5. 绝对定位 Y 轴翻转 ────────────────────────

        /// <summary>
        /// 验证带 position 的元素（无 align/vAlign/layout）走绝对定位模式，
        /// 锚点固定左上角 (0,1)，position 作为左上角输入时会换算为中心点输出。
        /// </summary>
        [Test]
        public void Convert_Position_UsesCenterAnchoredPosition()
        {
            var structure = new UiStructure
            {
                canvas = new UiCanvas { width = 1080, height = 1920 },
                root = new UiElement
                {
                    type = "container",
                    name = "root",
                    size = new UiSize { width = 1080, height = 1920 },
                    children = new List<UiElement>
                    {
                        new UiElement
                        {
                            type = "image",
                            name = "positioned",
                            position = new UiPosition { x = 100, y = 200 },
                            size = new UiSize { width = 300, height = 50 }
                        }
                    }
                }
            };

            List<ConvertedNode> result = _converter.Convert(structure, UiStructureConversionOptions.IncludeRootNode);
            var child = result[1].Descriptor;

            Assert.AreEqual(new Vector2(0f, 1f), child.AnchorMin,
                "绝对定位 AnchorMin 应为 (0,1)");
            Assert.AreEqual(new Vector2(0f, 1f), child.AnchorMax,
                "绝对定位 AnchorMax 应为 (0,1)");
            Assert.AreEqual(250f, child.AnchoredPosition.x,
                "AnchoredPosition.x 应为 position.x + width/2");
            Assert.AreEqual(-225f, child.AnchoredPosition.y,
                "AnchoredPosition.y 应为 -(position.y + height/2)");
        }

        /// <summary>
        /// 验证同时带 position 与 align/vAlign 的元素，仍以源图像素框作为 RectTransform 位置。
        /// </summary>
        [Test]
        public void Convert_PositionAndAlign_Position优先于对齐定位()
        {
            var structure = new UiStructure
            {
                canvas = new UiCanvas { width = 768, height = 1376 },
                root = new UiElement
                {
                    type = "container",
                    name = "root",
                    size = new UiSize { width = 768, height = 1376 },
                    children = new List<UiElement>
                    {
                        new UiElement
                        {
                            type = "text",
                            name = "EnergyTitle",
                            position = new UiPosition { x = 178, y = 42 },
                            size = new UiSize { width = 70, height = 36 },
                            align = "center",
                            vAlign = "top",
                            textContent = "体力",
                            fontSize = 32,
                        }
                    }
                }
            };

            List<ConvertedNode> result = _converter.Convert(structure, UiStructureConversionOptions.IncludeRootNode);
            var child = result[1].Descriptor;
            var visuals = result[1].Visuals;

            Assert.AreEqual(new Vector2(0f, 1f), child.AnchorMin, "有 position 时应使用左上角锚点");
            Assert.AreEqual(new Vector2(0f, 1f), child.AnchorMax, "有 position 时应使用左上角锚点");
            Assert.AreEqual(213f, child.AnchoredPosition.x, "x 应按 position.x + width/2 计算，而不是居中锚点");
            Assert.AreEqual(-60f, child.AnchoredPosition.y, "y 应按 -(position.y + height/2) 计算，而不是 top 对齐兜底");
            Assert.AreEqual("体力", visuals.TextContent, "应读取 AI2UI 默认输出的 textContent 字段");
            Assert.IsTrue(visuals.HasTextAlignment, "文本节点应保留框内对齐语义");
            Assert.AreEqual(TextAnchor.UpperCenter, visuals.TextAlignment, "align/vAlign 应映射到 Text.alignment，而不是 RectTransform 定位");
        }

        /// <summary>
        /// 验证嵌套节点的 position 使用源图全局坐标，并会减去父节点源图框后换算为父本地位置。
        /// </summary>
        [Test]
        public void Convert_NestedGlobalPosition_减去父源图坐标()
        {
            var structure = new UiStructure
            {
                canvas = new UiCanvas { width = 768, height = 1376 },
                root = new UiElement
                {
                    type = "container",
                    name = "root",
                    position = new UiPosition { x = 0, y = 0 },
                    size = new UiSize { width = 768, height = 1376 },
                    children = new List<UiElement>
                    {
                        new UiElement
                        {
                            type = "container",
                            name = "LevelPreviewPanel",
                            position = new UiPosition { x = 123, y = 306 },
                            size = new UiSize { width = 522, height = 415 },
                            children = new List<UiElement>
                            {
                                new UiElement
                                {
                                    type = "text",
                                    name = "LevelName",
                                    position = new UiPosition { x = 241, y = 643 },
                                    size = new UiSize { width = 286, height = 45 },
                                    align = "center",
                                    vAlign = "middle",
                                    textContent = "第7-5关：水晶森林",
                                    fontSize = 30,
                                }
                            }
                        }
                    }
                }
            };

            List<ConvertedNode> result = _converter.Convert(structure, UiStructureConversionOptions.IncludeRootNode);
            var levelName = result[2].Descriptor;
            var visuals = result[2].Visuals;

            Assert.AreEqual("root/LevelPreviewPanel", levelName.ParentPath, "子节点仍应挂到父容器路径下");
            Assert.AreEqual(new Vector2(0f, 1f), levelName.AnchorMin, "源图全局坐标换算后仍使用父节点左上角锚点");
            Assert.AreEqual(new Vector2(0f, 1f), levelName.AnchorMax, "源图全局坐标换算后仍使用父节点左上角锚点");
            Assert.That(levelName.AnchoredPosition.x, Is.EqualTo(261f).Within(0.001f), "x 应为 (241-123)+286/2");
            Assert.That(levelName.AnchoredPosition.y, Is.EqualTo(-359.5f).Within(0.001f), "y 应为 -((643-306)+45/2)");
            Assert.AreEqual(TextAnchor.MiddleCenter, visuals.TextAlignment, "align/vAlign 应只影响文本框内对齐");
        }

        // ──────────────────────── 6. 行布局 → HorizontalLayoutGroup ────────

        /// <summary>
        /// 验证 layout.type="row" 映射为 LayoutGroupType.Horizontal。
        /// </summary>
        [Test]
        public void Convert_LayoutRow_AddsHorizontalLayoutGroupInfo()
        {
            var structure = new UiStructure
            {
                canvas = new UiCanvas { width = 1080, height = 1920 },
                root = new UiElement
                {
                    type = "container",
                    name = "root",
                    size = new UiSize { width = 1080, height = 1920 },
                    layout = new UiLayout { type = "row", spacing = "even" }
                }
            };

            List<ConvertedNode> result = _converter.Convert(structure, UiStructureConversionOptions.IncludeRootNode);

            Assert.IsNotNull(result[0].LayoutInfo, "LayoutInfo 不应为 null");
            Assert.AreEqual(LayoutGroupType.Horizontal, result[0].LayoutInfo.GroupType,
                "row 应映射为 Horizontal");
        }

        // ──────────────────────── 7. 列布局 → VerticalLayoutGroup ────────

        /// <summary>
        /// 验证 layout.type="column" 映射为 LayoutGroupType.Vertical。
        /// </summary>
        [Test]
        public void Convert_LayoutColumn_AddsVerticalLayoutGroupInfo()
        {
            var structure = new UiStructure
            {
                canvas = new UiCanvas { width = 1080, height = 1920 },
                root = new UiElement
                {
                    type = "container",
                    name = "root",
                    size = new UiSize { width = 1080, height = 1920 },
                    layout = new UiLayout { type = "column", spacing = "10" }
                }
            };

            List<ConvertedNode> result = _converter.Convert(structure, UiStructureConversionOptions.IncludeRootNode);

            Assert.IsNotNull(result[0].LayoutInfo, "LayoutInfo 不应为 null");
            Assert.AreEqual(LayoutGroupType.Vertical, result[0].LayoutInfo.GroupType,
                "column 应映射为 Vertical");
        }

        // ──────────────────────── 8. image 类型 → Image 组件 ────────

        /// <summary>
        /// 验证 type="image" 映射到 UnityEngine.UI.Image 组件。
        /// </summary>
        [Test]
        public void Convert_ImageType_AddsImageComponent()
        {
            var structure = new UiStructure
            {
                canvas = new UiCanvas { width = 1080, height = 1920 },
                root = new UiElement
                {
                    type = "image",
                    name = "bg",
                    size = new UiSize { width = 100, height = 100 }
                }
            };

            List<ConvertedNode> result = _converter.Convert(structure, UiStructureConversionOptions.IncludeRootNode);

            Assert.Contains("UnityEngine.UI.Image", result[0].Descriptor.ComponentTypeNames,
                "image 类型应包含 UnityEngine.UI.Image 组件");
        }

        /// <summary>
        /// 验证 Auto Slice 语义字段会从 schema 保留到 descriptor visuals。
        /// </summary>
        [Test]
        public void Convert_ImageType_保留AutoSlice语义字段()
        {
            var structure = new UiStructure
            {
                canvas = new UiCanvas { width = 1080, height = 1920 },
                root = new UiElement
                {
                    type = "image",
                    name = "coin",
                    asset = "legacy_coin_asset",
                    marker = "icon_coin",
                    spriteHint = "coin_hint",
                    regionId = "r001",
                    size = new UiSize { width = 100, height = 100 }
                }
            };

            List<ConvertedNode> result = _converter.Convert(structure, UiStructureConversionOptions.IncludeRootNode);
            var visuals = result[0].Descriptor.Visuals ?? result[0].Visuals;

            Assert.AreEqual("icon_coin", visuals.AssetMarker);
            Assert.AreEqual("coin_hint", visuals.SpriteHint);
            Assert.AreEqual("r001", visuals.RegionId);
        }

        /// <summary>
        /// 验证缺少 marker 时 asset 会作为 JSON-driven slicing 的 AssetMarker 兜底。
        /// </summary>
        [Test]
        public void Convert_ImageType_缺少Marker时保留AssetMarker()
        {
            var structure = new UiStructure
            {
                canvas = new UiCanvas { width = 1080, height = 1920 },
                root = new UiElement
                {
                    type = "image",
                    name = "coin",
                    asset = "legacy_coin_asset",
                    size = new UiSize { width = 100, height = 100 }
                }
            };

            List<ConvertedNode> result = _converter.Convert(structure, UiStructureConversionOptions.IncludeRootNode);
            var visuals = result[0].Descriptor.Visuals ?? result[0].Visuals;

            Assert.AreEqual("legacy_coin_asset", visuals.AssetMarker);
        }

        /// <summary>
        /// 验证显式 position/size 会记录 source bounds 与 source canvas size，供 JSON-driven slicing 使用。
        /// </summary>
        [Test]
        public void Convert_ImageType_记录SourceBounds和SourceCanvasSize()
        {
            var structure = new UiStructure
            {
                canvas = new UiCanvas { width = 1920, height = 1080 },
                root = new UiElement
                {
                    type = "image",
                    name = "coin",
                    position = new UiPosition { x = 100, y = 120 },
                    size = new UiSize { width = 64, height = 80 }
                }
            };

            List<ConvertedNode> result = _converter.Convert(structure, UiStructureConversionOptions.IncludeRootNode);
            var descriptor = result[0].Descriptor;

            Assert.IsTrue(descriptor.HasSourceBounds);
            Assert.AreEqual(new Rect(100, 120, 64, 80), descriptor.SourceBounds);
            Assert.AreEqual(new Vector2(1920, 1080), descriptor.SourceCanvasSize);
        }

        // ──────────────────────── 9. text 类型 → TextMeshProUGUI 组件 + 视觉属性 ────────

        /// <summary>
        /// 验证 type="text" 映射到 TMPro.TextMeshProUGUI 组件，
        /// 且 TextContent、FontSize、NodeColor 均正确提取。
        /// </summary>
        [Test]
        public void Convert_TextType_AddsTextComponent()
        {
            var structure = new UiStructure
            {
                canvas = new UiCanvas { width = 1080, height = 1920 },
                root = new UiElement
                {
                    type = "text",
                    name = "label",
                    size = new UiSize { width = 200, height = 50 },
                    text = "PLAY",
                    fontSize = 32,
                    color = "#FFFFFF"
                }
            };

            List<ConvertedNode> result = _converter.Convert(structure, UiStructureConversionOptions.IncludeRootNode);
            var desc = result[0].Descriptor;
            var visuals = result[0].Visuals;

            Assert.Contains("TMPro.TextMeshProUGUI", desc.ComponentTypeNames,
                "text 类型应包含 TMPro.TextMeshProUGUI 组件");
            Assert.AreEqual("PLAY", visuals.TextContent,
                "TextContent 应为 PLAY");
            Assert.AreEqual(32, visuals.FontSize,
                "FontSize 应为 32");
        }

        // ──────────────────────── 10. rect 类型 → Image + 颜色/透明度 ────────

        /// <summary>
        /// 验证 type="rect" 映射到 UnityEngine.UI.Image，
        /// 且 NodeColor 为红色、Opacity 为 0.5。
        /// </summary>
        [Test]
        public void Convert_RectType_SetsColor()
        {
            var structure = new UiStructure
            {
                canvas = new UiCanvas { width = 1080, height = 1920 },
                root = new UiElement
                {
                    type = "rect",
                    name = "bg",
                    size = new UiSize { width = 500, height = 200 },
                    color = "#FF0000",
                    opacity = 0.5f
                }
            };

            List<ConvertedNode> result = _converter.Convert(structure, UiStructureConversionOptions.IncludeRootNode);
            var desc = result[0].Descriptor;
            var visuals = result[0].Visuals;

            Assert.Contains("UnityEngine.UI.Image", desc.ComponentTypeNames,
                "rect 类型应包含 UnityEngine.UI.Image 组件");

            Assert.IsNotNull(visuals.NodeColor, "NodeColor 不应为 null");
            Assert.AreEqual(Color.red, visuals.NodeColor.Value,
                "NodeColor 应为红色");

            Assert.IsNotNull(visuals.Opacity, "Opacity 不应为 null");
            Assert.AreEqual(0.5f, visuals.Opacity.Value,
                "Opacity 应为 0.5");
        }

        // ──────────────────────── 11. button 类型 → Image + Button ────────

        /// <summary>
        /// 验证 type="button" 同时映射到 Image 和 Button 组件。
        /// </summary>
        [Test]
        public void Convert_ButtonType_AddsImageAndButton()
        {
            var structure = new UiStructure
            {
                canvas = new UiCanvas { width = 1080, height = 1920 },
                root = new UiElement
                {
                    type = "button",
                    name = "btn",
                    size = new UiSize { width = 200, height = 60 }
                }
            };

            List<ConvertedNode> result = _converter.Convert(structure, UiStructureConversionOptions.IncludeRootNode);
            var componentNames = result[0].Descriptor.ComponentTypeNames;

            Assert.Contains("UnityEngine.UI.Image", componentNames,
                "button 类型应包含 Image 组件");
            Assert.Contains("UnityEngine.UI.Button", componentNames,
                "button 类型应包含 Button 组件");
        }

        // ──────────────────────── 12. 未知类型 → 警告 ────────

        /// <summary>
        /// 验证未知元素类型（如 scrollview）被跳过，
        /// 并在 Report.Warnings 中记录包含该类型名的警告。
        /// </summary>
        [Test]
        public void Convert_UnknownType_SkipsWithWarning()
        {
            var structure = new UiStructure
            {
                canvas = new UiCanvas { width = 1080, height = 1920 },
                root = new UiElement
                {
                    type = "scrollview",
                    name = "scroller",
                    size = new UiSize { width = 400, height = 800 }
                }
            };

            List<ConvertedNode> result = _converter.Convert(structure, UiStructureConversionOptions.IncludeRootNode);

            Assert.IsNotNull(_converter.Report, "Report 不应为 null");
            Assert.IsTrue(
                _converter.Report.Warnings.Any(w => w.Contains("scrollview")),
                "警告列表中应包含提及 'scrollview' 的条目");
        }

        // ──────────────────────── 13. 同名去重 ────────

        /// <summary>
        /// 验证同级出现两个同名子元素时，
        /// 第二个被自动重命名为 "icon_1"，且 Report.RenamedElements 有记录。
        /// </summary>
        [Test]
        public void Convert_DuplicateNames_AutoRenames()
        {
            var structure = new UiStructure
            {
                canvas = new UiCanvas { width = 1080, height = 1920 },
                root = new UiElement
                {
                    type = "container",
                    name = "root",
                    size = new UiSize { width = 1080, height = 1920 },
                    children = new List<UiElement>
                    {
                        new UiElement
                        {
                            type = "image",
                            name = "icon",
                            size = new UiSize { width = 50, height = 50 }
                        },
                        new UiElement
                        {
                            type = "image",
                            name = "icon",
                            size = new UiSize { width = 50, height = 50 }
                        }
                    }
                }
            };

            List<ConvertedNode> result = _converter.Convert(structure, UiStructureConversionOptions.IncludeRootNode);

            // result[0]=root, result[1]=icon, result[2]=icon_1
            Assert.AreEqual(3, result.Count, "应生成 3 个节点");
            Assert.AreEqual("icon", result[1].Descriptor.Name,
                "第一个 icon 名称不变");
            Assert.AreEqual("icon_1", result[2].Descriptor.Name,
                "第二个 icon 应重命名为 icon_1");

            Assert.IsTrue(
                _converter.Report.RenamedElements.Any(r => r.Contains("icon") && r.Contains("icon_1")),
                "RenamedElements 中应有 icon → icon_1 的记录");
        }

        // ──────────────────────── 14. 嵌套层级路径 ────────

        /// <summary>
        /// 验证三层嵌套结构中各节点的 ParentPath 正确计算：
        /// root → ""（空）, panel → "root", bg → "root/panel"。
        /// </summary>
        [Test]
        public void Convert_NestedHierarchy_ComputesParentPaths()
        {
            var structure = new UiStructure
            {
                canvas = new UiCanvas { width = 1080, height = 1920 },
                root = new UiElement
                {
                    type = "container",
                    name = "root",
                    size = new UiSize { width = 1080, height = 1920 },
                    children = new List<UiElement>
                    {
                        new UiElement
                        {
                            type = "container",
                            name = "panel",
                            size = new UiSize { width = 800, height = 600 },
                            children = new List<UiElement>
                            {
                                new UiElement
                                {
                                    type = "image",
                                    name = "bg",
                                    size = new UiSize { width = 800, height = 600 }
                                }
                            }
                        }
                    }
                }
            };

            List<ConvertedNode> result = _converter.Convert(structure, UiStructureConversionOptions.IncludeRootNode);

            // result[0]=root, result[1]=panel, result[2]=bg
            Assert.AreEqual("", result[0].Descriptor.ParentPath,
                "root 的 ParentPath 应为空字符串");
            Assert.AreEqual("root", result[1].Descriptor.ParentPath,
                "panel（root 直接子节点）的 ParentPath 应为 'root'");
            Assert.AreEqual("root/panel", result[2].Descriptor.ParentPath,
                "bg 的 ParentPath 应为 'root/panel'");
        }

        /// <summary>
        /// 验证父节点带 layout 但子节点有显式 position 时，子节点仍按源图绝对坐标换算，
        /// 不会被 LayoutGroup 语义改写为 stretch。
        /// </summary>
        [Test]
        public void Convert_ChildInSemanticLayout_WithPosition_保留绝对坐标()
        {
            var structure = new UiStructure
            {
                canvas = new UiCanvas { width = 768, height = 1376 },
                root = new UiElement
                {
                    type = "container",
                    name = "LevelPreviewMenu",
                    position = new UiPosition { x = 0, y = 0 },
                    size = new UiSize { width = 768, height = 1376 },
                    children = new List<UiElement>
                    {
                        new UiElement
                        {
                            type = "container",
                            name = "TopStatusBar",
                            position = new UiPosition { x = 28, y = 40 },
                            size = new UiSize { width = 711, height = 126 },
                            layout = new UiLayout { type = "row" },
                            children = new List<UiElement>
                            {
                                new UiElement
                                {
                                    type = "container",
                                    name = "StaminaGroup",
                                    position = new UiPosition { x = 28, y = 40 },
                                    size = new UiSize { width = 352, height = 126 }
                                }
                            }
                        }
                    }
                }
            };

            List<ConvertedNode> result = _converter.Convert(structure, UiStructureConversionOptions.IncludeRootNode);
            var topStatusBar = result[1];
            var staminaGroup = result[2].Descriptor;

            Assert.IsNotNull(topStatusBar.LayoutInfo, "TopStatusBar 应保留 layout 语义信息");
            Assert.IsFalse(topStatusBar.LayoutInfo.UseUnityLayoutGroup, "含显式 position 子节点时不应真实添加 LayoutGroup");
            Assert.AreEqual(new Vector2(0f, 1f), staminaGroup.AnchorMin, "子节点应使用左上角锚点");
            Assert.AreEqual(new Vector2(0f, 1f), staminaGroup.AnchorMax, "子节点应使用左上角锚点");
            Assert.That(staminaGroup.AnchoredPosition.x, Is.EqualTo(176f).Within(0.001f), "x 应为 (28-28)+352/2");
            Assert.That(staminaGroup.AnchoredPosition.y, Is.EqualTo(-63f).Within(0.001f), "y 应为 -((40-40)+126/2)");
            Assert.AreEqual(new Vector2(352f, 126f), staminaGroup.SizeDelta, "尺寸应来自 JSON");
        }

        // ──────────────────────── 15. 布局子节点不设手动锚点 ────────

        /// <summary>
        /// 验证父节点带 layout 且子节点没有显式 position 时，子节点走默认拉伸锚点（0,0）~（1,1），
        /// 而非手动对齐定位。
        /// </summary>
        [Test]
        public void Convert_ChildInLayout_NoManualPosition()
        {
            var structure = new UiStructure
            {
                canvas = new UiCanvas { width = 1080, height = 1920 },
                root = new UiElement
                {
                    type = "container",
                    name = "root",
                    size = new UiSize { width = 1080, height = 1920 },
                    layout = new UiLayout { type = "row", spacing = "10" },
                    children = new List<UiElement>
                    {
                        new UiElement
                        {
                            type = "image",
                            name = "child1",
                            size = new UiSize { width = 100, height = 50 }
                        },
                        new UiElement
                        {
                            type = "image",
                            name = "child2",
                            size = new UiSize { width = 100, height = 50 }
                        }
                    }
                }
            };

            List<ConvertedNode> result = _converter.Convert(structure, UiStructureConversionOptions.IncludeRootNode);

            // result[1]=child1, result[2]=child2
            var child1 = result[1].Descriptor;
            var child2 = result[2].Descriptor;

            Assert.AreEqual(Vector2.zero, child1.AnchorMin,
                "布局子节点 AnchorMin 应为 (0,0)");
            Assert.AreEqual(Vector2.one, child1.AnchorMax,
                "布局子节点 AnchorMax 应为 (1,1)");
            Assert.AreEqual(Vector2.zero, child1.AnchoredPosition,
                "布局子节点 AnchoredPosition 应为 (0,0)");

            Assert.AreEqual(Vector2.zero, child2.AnchorMin,
                "第二个布局子节点 AnchorMin 应为 (0,0)");
            Assert.AreEqual(Vector2.one, child2.AnchorMax,
                "第二个布局子节点 AnchorMax 应为 (1,1)");
        }
    }
}

#endif

