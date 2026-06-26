#if UNITY_EDITOR

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace RogueCard.Editor.DraftWorkbench.Tests
{
    /// <summary>
    /// <see cref="UiStructure"/> 的 JSON 反序列化与 JSON-driven slicing 契约测试。
    /// </summary>
    [TestFixture]
    public class UiStructureSchemaTests
    {
        /// <summary>
        /// 验证完整 JSON 反序列化后，asset / marker / spriteHint / regionId
        /// 以及 source canvas / bounds 能稳定传递到转换器输出。
        /// </summary>
        [Test]
        public void FromJson_CompleteSchema_PreservesAutoSliceFieldsAndSourceBounds()
        {
            const string json = @"{
  ""canvas"": {
    ""width"": 1920,
    ""height"": 1080,
    ""name"": ""MainCanvas""
  },
  ""root"": {
    ""type"": ""image"",
    ""name"": ""coin"",
    ""position"": { ""x"": 100, ""y"": 120 },
    ""size"": { ""width"": 64, ""height"": 80 },
    ""asset"": ""legacy_coin_asset"",
    ""marker"": ""icon_coin"",
    ""spriteHint"": ""coin_hint"",
    ""regionId"": ""r001""
  }
}";

            UiStructure structure = JsonUtility.FromJson<UiStructure>(json);

            Assert.IsNotNull(structure);
            Assert.IsNotNull(structure.canvas);
            Assert.AreEqual(1920, structure.canvas.width);
            Assert.AreEqual(1080, structure.canvas.height);
            Assert.AreEqual("MainCanvas", structure.canvas.name);

            Assert.IsNotNull(structure.root);
            Assert.AreEqual("legacy_coin_asset", structure.root.asset);
            Assert.AreEqual("icon_coin", structure.root.marker);
            Assert.AreEqual("coin_hint", structure.root.spriteHint);
            Assert.AreEqual("r001", structure.root.regionId);

            var converter = new UiStructureConverter();
            List<ConvertedNode> result = converter.Convert(structure, UiStructureConversionOptions.IncludeRootNode);
            var descriptor = result[0].Descriptor;
            var visuals = descriptor.Visuals ?? result[0].Visuals;

            Assert.IsTrue(descriptor.HasSourceBounds);
            Assert.AreEqual(new Rect(100f, 120f, 64f, 80f), descriptor.SourceBounds);
            Assert.AreEqual(new Vector2(1920f, 1080f), descriptor.SourceCanvasSize);
            Assert.AreEqual("icon_coin", visuals.AssetMarker);
            Assert.AreEqual("coin_hint", visuals.SpriteHint);
            Assert.AreEqual("r001", visuals.RegionId);
        }

        /// <summary>
        /// 验证缺省可选字段时，JSON 反序列化仍然安全，且转换器不会误判 source bounds。
        /// </summary>
        [Test]
        public void FromJson_MissingOptionalFields_UsesSafeDefaults()
        {
            const string json = @"{
  ""canvas"": {
    ""width"": 1080,
    ""height"": 1920,
    ""name"": ""FallbackCanvas""
  },
  ""root"": {
    ""type"": ""image"",
    ""name"": ""fallback_icon"",
    ""size"": { ""width"": 48, ""height"": 48 }
  }
}";

            UiStructure structure = JsonUtility.FromJson<UiStructure>(json);

            Assert.IsNotNull(structure);
            Assert.IsNotNull(structure.root);
            Assert.IsNull(structure.root.asset);
            Assert.IsNull(structure.root.marker);
            Assert.IsNull(structure.root.spriteHint);
            Assert.IsNull(structure.root.regionId);

            var converter = new UiStructureConverter();
            List<ConvertedNode> result = converter.Convert(structure, UiStructureConversionOptions.IncludeRootNode);
            var descriptor = result[0].Descriptor;
            var visuals = descriptor.Visuals ?? result[0].Visuals;

            Assert.AreEqual(new Vector2(1080f, 1920f), descriptor.SourceCanvasSize);
            Assert.IsNull(visuals.AssetMarker);
            Assert.IsNull(visuals.SpriteHint);
            Assert.IsNull(visuals.RegionId);
        }

        /// <summary>
        /// 验证 Image-To-UI 顶层 unity 配置和 Sprite 身份字段会进入 Draft Workbench schema。
        /// </summary>
        [Test]
        public void FromJson_ImageToUiCoreFields_PreservesUnityAndSpriteIdentity()
        {
            const string json = @"{
  ""canvas"": {
    ""width"": 768,
    ""height"": 1376,
    ""name"": ""MainMenu""
  },
  ""unity"": {
    ""schemaVersion"": 1,
    ""outputPrefabPath"": ""Assets/UI/MainMenu.prefab"",
    ""spriteRootFolder"": ""Assets/UI/Sprites""
  },
  ""root"": {
    ""type"": ""container"",
    ""name"": ""Root"",
    ""size"": { ""width"": 768, ""height"": 1376 },
    ""children"": [
      {
        ""type"": ""image"",
        ""name"": ""StartButton"",
        ""position"": { ""x"": 100, ""y"": 200 },
        ""size"": { ""width"": 300, ""height"": 96 },
        ""asset"": ""Button01.png"",
        ""assetGuid"": ""0123456789abcdef0123456789abcdef"",
        ""spriteName"": ""Button01_Normal"",
        ""nineSlice"": true
      }
    ]
  }
}";

            UiStructure structure = JsonUtility.FromJson<UiStructure>(json);

            Assert.IsNotNull(structure.unity);
            Assert.AreEqual(1, structure.unity.schemaVersion);
            Assert.AreEqual("Assets/UI/MainMenu.prefab", structure.unity.outputPrefabPath);
            Assert.AreEqual("Assets/UI/Sprites", structure.unity.spriteRootFolder);

            UiElement child = structure.root.children[0];
            Assert.AreEqual("0123456789abcdef0123456789abcdef", child.assetGuid);
            Assert.AreEqual("Button01_Normal", child.spriteName);

            var converter = new UiStructureConverter();
            List<ConvertedNode> result = converter.Convert(structure, UiStructureConversionOptions.DraftWorkbenchDefault);
            UiNodeVisuals visuals = result[0].Descriptor.Visuals;

            Assert.AreEqual("0123456789abcdef0123456789abcdef", visuals.SpriteGuid);
            Assert.AreEqual("Button01_Normal", visuals.SpriteName);
            Assert.AreEqual("Assets/UI/Sprites", visuals.SpriteRootFolder);
            Assert.IsTrue(visuals.UseSlicedImage);
        }
    }
}

#endif

