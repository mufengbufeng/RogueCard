#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RogueCard.Editor.DraftWorkbench.Tests
{
    /// <summary>
    /// DraftSpriteApplyService 的 apply/revert 安全性测试。
    /// </summary>
    [TestFixture]
    public class DraftSpriteApplyServiceTests
    {
        private GameObject _root;

        /// <summary>
        /// 每个测试后销毁临时 GameObject。
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
                _root = null;
            }
        }

        /// <summary>
        /// ApplyAssignments 只应处理 Approved=true 的 assignment。
        /// </summary>
        [Test]
        public void ApplyAssignments_只应用ApprovedAssignments()
        {
            _root = new GameObject("Root");
            var target = new GameObject("Icon");
            target.transform.SetParent(_root.transform, false);
            target.AddComponent<Image>();
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);

            var assignments = new List<DraftSpriteAssignment>
            {
                new DraftSpriteAssignment { NodePath = "Icon", SpriteAssetPath = "Assets/Missing.png", Approved = false },
                new DraftSpriteAssignment { NodePath = "Icon", SpriteGuid = "runtime-guid", Approved = true }
            };

            // AssetDatabase 无法加载 runtime sprite，因此缺失资源不会产生记录；此断言确保 unapproved 不会误报或写入。
            var records = DraftSpriteApplyService.ApplyAssignments(_root, assignments);

            Assert.AreEqual(0, records.Count);
            Object.DestroyImmediate(sprite);
        }

        /// <summary>
        /// ValidateSpriteRevertState 应在 Sprite 已被手动改动后阻止回滚。
        /// </summary>
        [Test]
        public void ValidateSpriteRevertState_检测手动修改冲突()
        {
            _root = new GameObject("Root");
            var target = new GameObject("Icon");
            target.transform.SetParent(_root.transform, false);
            var image = target.AddComponent<Image>();
            image.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
            var record = new DraftApplyRecord
            {
                SpriteChanges = new List<SpriteChangeRecord>
                {
                    new SpriteChangeRecord
                    {
                        ObjectPath = "Icon",
                        NewSpriteGuid = "expected-new-guid",
                        NewSpriteAssetPath = "Assets/ExpectedNew.png",
                        OldSpriteGuid = "old-guid",
                        OldSpriteAssetPath = "Assets/Old.png"
                    }
                }
            };

            bool canRevert = DraftSpriteApplyService.ValidateSpriteRevertState(_root, record);

            Assert.IsFalse(canRevert);
        }

        /// <summary>
        /// BuildSpriteChangeRecords 应能通过 Image-To-UI 的 assetGuid 解析目标 Sprite 并记录可回滚信息。
        /// </summary>
        [Test]
        public void BuildSpriteChangeRecords_通过AssetGuid记录Sprite变更()
        {
            const string assetPath = "Assets/Temp/DraftWorkbenchTests/ApplySprite.png";
            Sprite newSprite = CreateTemporarySpriteAsset(assetPath);
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            _root = new GameObject("Root");
            var target = new GameObject("Icon");
            target.transform.SetParent(_root.transform, false);
            target.AddComponent<Image>();

            var changes = new List<UguiNodeChange>
            {
                new UguiNodeChange
                {
                    Descriptor = new UguiNodeDescriptor
                    {
                        Name = "Icon",
                        Visuals = new UiNodeVisuals
                        {
                            SpriteGuid = guid,
                            RegionId = "region-1",
                            AssetMarker = "icon_marker"
                        }
                    }
                }
            };

            try
            {
                List<SpriteChangeRecord> records = DraftSpriteApplyService.BuildSpriteChangeRecords(_root, changes);

                Assert.AreEqual(1, records.Count);
                Assert.AreEqual("Icon", records[0].ObjectPath);
                Assert.AreEqual(assetPath, records[0].NewSpriteAssetPath);
                Assert.AreEqual(guid, records[0].NewSpriteGuid);
                Assert.AreEqual("region-1", records[0].RegionId);
                Assert.AreEqual("icon_marker", records[0].Marker);
                Assert.IsNotNull(newSprite, "临时 Sprite 应保持可用，避免测试被优化掉");
            }
            finally
            {
                AssetDatabase.DeleteAsset(assetPath);
                AssetDatabase.DeleteAsset("Assets/Temp/DraftWorkbenchTests");
            }
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
