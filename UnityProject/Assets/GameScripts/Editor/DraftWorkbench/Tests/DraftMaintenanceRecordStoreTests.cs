#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace RogueCard.Editor.DraftWorkbench.Tests
{
    /// <summary>
    /// <see cref="DraftMaintenanceRecordStore"/> 的 EditMode 单元测试，覆盖 JSON 路径、hash 和维护记录追加。
    /// </summary>
    [TestFixture]
    public class DraftMaintenanceRecordStoreTests
    {
        private const string TestFolder = "Assets/Test/DraftMaintenanceRecordStoreTests";
        private const string TestPrefabPath = TestFolder + "/MaintenanceTarget.prefab";
        private GameObject _prefabRoot;

        /// <summary>
        /// 每个测试前准备测试目录。
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            EnsureFolder("Assets", "Test");
            EnsureFolder("Assets/Test", "DraftMaintenanceRecordStoreTests");
            DeleteMaintenanceFile();
            _prefabRoot = new GameObject("MaintenanceTarget", typeof(RectTransform));
        }

        /// <summary>
        /// 每个测试后清理临时对象和测试文件。
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (_prefabRoot != null)
            {
                Object.DestroyImmediate(_prefabRoot);
                _prefabRoot = null;
            }

            DeleteMaintenanceFile();
        }

        /// <summary>
        /// 验证 prefab 路径能推导出同名维护记录 JSON 路径。
        /// </summary>
        [Test]
        public void GetMaintenanceAssetPathForPrefab_生成同名Json路径()
        {
            string path = DraftMaintenanceRecordStore.GetMaintenanceAssetPathForPrefab("Assets/UI/Foo.prefab");

            Assert.AreEqual("Assets/UI/Foo.draft.maintenance.json", path);
        }

        /// <summary>
        /// 验证 source JSON hash 对同内容稳定，对不同内容变化。
        /// </summary>
        [Test]
        public void ComputeSourceJsonHash_稳定且区分内容()
        {
            string json = "{\"root\":{\"name\":\"A\"}}";

            string hashA = DraftMaintenanceRecordStore.ComputeSourceJsonHash(json);
            string hashB = DraftMaintenanceRecordStore.ComputeSourceJsonHash(json);
            string hashC = DraftMaintenanceRecordStore.ComputeSourceJsonHash("{\"root\":{\"name\":\"B\"}}");

            Assert.AreEqual(hashA, hashB, "相同 JSON 应产生相同 hash");
            Assert.AreNotEqual(hashA, hashC, "不同 JSON 应产生不同 hash");
            Assert.IsTrue(hashA.StartsWith("sha256:"), "hash 应包含 sha256 前缀");
        }

        /// <summary>
        /// 验证 preview 记录能写入 JSON 并保留节点快照和源 JSON 字符串。
        /// </summary>
        [Test]
        public void AppendPreviewRecord_写入节点快照和SourceJson()
        {
            var changes = new List<UguiNodeChange>
            {
                CreatePreviewChange("InfoPanel", isNew: true, hasConflict: false)
            };
            var report = new ConversionReport();
            report.AddWarning("测试警告");
            report.AddRename("Old", "New");
            string sourceJson = "{\"root\":{\"name\":\"InfoPanel\",\"extra\":true}}";

            var entry = DraftMaintenanceRecordStore.AppendPreviewRecord(
                TestPrefabPath,
                null,
                null,
                sourceJson,
                changes,
                report,
                768,
                1376,
                _prefabRoot);

            var document = DraftMaintenanceRecordStore.Load(TestPrefabPath);

            Assert.IsNotNull(entry, "应返回新增记录");
            Assert.IsNotNull(document, "应能加载维护记录文档");
            Assert.AreEqual(1, document.Entries.Count, "应写入 1 条记录");
            Assert.AreEqual("preview", document.Entries[0].Stage, "阶段应为 preview");
            Assert.AreEqual(sourceJson, document.Entries[0].SourceJson, "SourceJson 应原样保留");
            Assert.IsTrue(document.Entries[0].HasPreview, "preview entry 应包含预览快照");
            Assert.AreEqual(1, document.Entries[0].Preview.Nodes.Count, "应包含 1 个节点快照");
            Assert.AreEqual("InfoPanel", document.Entries[0].Preview.Nodes[0].Name, "节点名称应保留");
            Assert.AreEqual("UnityEngine.UI.Image", document.Entries[0].Preview.Nodes[0].ComponentTypeNames[0], "组件类型应保留");
            Assert.IsTrue(document.Entries[0].Preview.Nodes[0].HasVisuals, "应包含视觉快照");
            Assert.AreEqual("维护文本", document.Entries[0].Preview.Nodes[0].Visuals.TextContent, "文本内容应保留");
            Assert.IsTrue(document.Entries[0].Preview.Nodes[0].HasLayoutInfo, "应包含布局快照");
            Assert.IsFalse(document.Entries[0].Preview.Nodes[0].LayoutInfo.UseUnityLayoutGroup, "布局语义标记应保留");
            Assert.AreEqual("测试警告", document.Entries[0].Preview.ConversionWarnings[0], "转换警告应保留");
            Assert.AreEqual("Old -> New", document.Entries[0].Preview.RenamedElements[0], "重命名记录应保留");
        }

        /// <summary>
        /// 验证 applied 记录能写入 apply record 和 binding report 快照。
        /// </summary>
        [Test]
        public void AppendAppliedRecord_写入Apply和Binding快照()
        {
            var applyRecord = CreateApplyRecord();
            var bindingReport = CreateBindingReport();

            DraftMaintenanceRecordStore.AppendAppliedRecord(
                TestPrefabPath,
                null,
                null,
                "{\"root\":{}}",
                new List<UguiNodeChange> { CreatePreviewChange("ApplyNode", isNew: false, hasConflict: false) },
                null,
                applyRecord,
                bindingReport,
                768,
                1376,
                _prefabRoot);

            var document = DraftMaintenanceRecordStore.Load(TestPrefabPath);
            var entry = document.Entries[0];

            Assert.AreEqual("applied", entry.Stage, "阶段应为 applied");
            Assert.IsTrue(entry.HasPreview, "applied entry 应自包含 preview 快照");
            Assert.IsTrue(entry.HasApplyRecord, "应包含 apply record");
            Assert.AreEqual("record-001", entry.ApplyRecord.RecordGuid, "Apply record guid 应保留");
            Assert.AreEqual("ApplyNode", entry.ApplyRecord.CreatedObjectPaths[0], "CreatedObjectPaths 应保留");
            Assert.AreEqual("Panel/Title", entry.ApplyRecord.ModifiedRects[0].ObjectPath, "ModifiedRects 应保留");
            Assert.AreEqual("Panel/Icon", entry.ApplyRecord.SpriteChanges[0].ObjectPath, "Sprite change 目标路径应保留");
            Assert.AreEqual("old-guid", entry.ApplyRecord.SpriteChanges[0].OldSpriteGuid, "旧 Sprite GUID 应保留");
            Assert.AreEqual("Assets/New.png", entry.ApplyRecord.SpriteChanges[0].NewSpriteAssetPath, "新 Sprite path 应保留");
            Assert.AreEqual("r001", entry.ApplyRecord.SpriteChanges[0].RegionId, "Region id 应保留");
            Assert.AreEqual("icon_coin", entry.ApplyRecord.SpriteChanges[0].Marker, "marker 应保留");
            Assert.IsTrue(entry.HasBindingReport, "应包含 binding report");
            Assert.AreEqual("StartBtn", entry.BindingReport.AddedEntries[0].Key, "新增绑定应保留");
            Assert.AreEqual("Missing Image", entry.BindingReport.MissingComponentWarnings[0], "缺失组件警告应保留");
        }

        /// <summary>
        /// 验证 reverted 记录追加后不会删除历史 applied 记录。
        /// </summary>
        [Test]
        public void AppendRevertedRecord_追加历史不删除Applied()
        {
            var applyRecord = CreateApplyRecord();
            DraftMaintenanceRecordStore.AppendAppliedRecord(
                TestPrefabPath,
                null,
                null,
                "{\"root\":{}}",
                new List<UguiNodeChange> { CreatePreviewChange("ApplyNode", isNew: false, hasConflict: false) },
                null,
                applyRecord,
                null,
                768,
                1376,
                _prefabRoot);

            DraftMaintenanceRecordStore.AppendRevertedRecord(
                TestPrefabPath,
                null,
                null,
                applyRecord,
                DraftMaintenanceRecordStore.ComputeSourceJsonHash("{\"root\":{}}"),
                true);

            var document = DraftMaintenanceRecordStore.Load(TestPrefabPath);

            Assert.AreEqual(2, document.Entries.Count, "应保留 applied 并追加 reverted");
            Assert.AreEqual("applied", document.Entries[0].Stage, "第一条仍为 applied");
            Assert.AreEqual("reverted", document.Entries[1].Stage, "第二条为 reverted");
            Assert.IsTrue(document.Entries[1].HasRevert, "reverted entry 应包含回滚快照");
            Assert.AreEqual("record-001", document.Entries[1].Revert.RevertedApplyRecordGuid, "被回滚记录 guid 应保留");
            Assert.IsTrue(document.Entries[1].Revert.Success, "回滚成功标记应保留");
        }

        /// <summary>
        /// 创建测试用 UGUI 变更快照。
        /// </summary>
        private static UguiNodeChange CreatePreviewChange(string name, bool isNew, bool hasConflict)
        {
            return new UguiNodeChange
            {
                IsNew = isNew,
                HasConflict = hasConflict,
                Descriptor = new UguiNodeDescriptor
                {
                    Name = name,
                    ParentPath = "Root",
                    AnchorMin = new Vector2(0f, 1f),
                    AnchorMax = new Vector2(0f, 1f),
                    AnchoredPosition = new Vector2(100f, -50f),
                    SizeDelta = new Vector2(200f, 80f),
                    ComponentTypeNames = new List<string> { "UnityEngine.UI.Image" },
                    Visuals = new UiNodeVisuals
                    {
                        TextContent = "维护文本",
                        FontSize = 24,
                        NodeColor = Color.cyan,
                        Opacity = 0.8f,
                        HasTextAlignment = true,
                        TextAlignment = TextAnchor.MiddleCenter
                    },
                    LayoutInfo = new LayoutGroupInfo
                    {
                        GroupType = LayoutGroupType.Horizontal,
                        Spacing = "12",
                        Padding = new Vector2(4f, 8f),
                        ChildAlignment = "center",
                        UseUnityLayoutGroup = false
                    },
                    HasSourceBounds = true,
                    SourceBounds = new Rect(10f, 20f, 200f, 80f),
                    SourceCanvasSize = new Vector2(768f, 1376f)
                }
            };
        }

        /// <summary>
        /// 创建测试用应用记录。
        /// </summary>
        private static DraftApplyRecord CreateApplyRecord()
        {
            return new DraftApplyRecord
            {
                RecordGuid = "record-001",
                AppliedAt = new System.DateTime(2026, 6, 7, 12, 0, 0, System.DateTimeKind.Utc),
                CreatedObjectPaths = new List<string> { "ApplyNode" },
                AddedCollectorKeys = new List<string> { "StartBtn" },
                AddedScriptFields = new List<string> { "_startBtn" },
                ModifiedRects = new List<RectChangeRecord>
                {
                    new RectChangeRecord
                    {
                        ObjectPath = "Panel/Title",
                        OldAnchoredPosition = new Vector2(1f, 2f),
                        NewAnchoredPosition = new Vector2(3f, 4f),
                        OldSizeDelta = new Vector2(100f, 50f),
                        NewSizeDelta = new Vector2(200f, 80f),
                        OldAnchorMin = Vector2.zero,
                        NewAnchorMin = new Vector2(0f, 1f),
                        OldAnchorMax = Vector2.one,
                        NewAnchorMax = new Vector2(0f, 1f),
                        HasPivot = true,
                        OldPivot = new Vector2(0.5f, 0.5f),
                        NewPivot = new Vector2(0.5f, 0.5f)
                    }
                },
                SpriteChanges = new List<SpriteChangeRecord>
                {
                    new SpriteChangeRecord
                    {
                        ObjectPath = "Panel/Icon",
                        OldSpriteGuid = "old-guid",
                        OldSpriteAssetPath = "Assets/Old.png",
                        NewSpriteGuid = "new-guid",
                        NewSpriteAssetPath = "Assets/New.png",
                        RegionId = "r001",
                        Marker = "icon_coin"
                    }
                }
            };
        }

        /// <summary>
        /// 创建测试用绑定报告。
        /// </summary>
        private static DraftBindingReport CreateBindingReport()
        {
            var report = new DraftBindingReport();
            report.AddedEntries.Add(new BindingReportEntry
            {
                Key = "StartBtn",
                ObjectPath = "Root/StartBtn",
                ComponentType = "UnityEngine.UI.Button"
            });
            report.SkippedEntries.Add(new BindingReportEntry
            {
                Key = "BadNode",
                ObjectPath = "Root/BadNode",
                Reason = "测试跳过"
            });
            report.ConflictEntries.Add(new BindingReportEntry
            {
                Key = "ExistingBtn",
                ObjectPath = "Root/ExistingBtn",
                Reason = "测试冲突"
            });
            report.AddedScriptFields.Add("_startBtn");
            report.MissingComponentWarnings.Add("Missing Image");
            return report;
        }

        /// <summary>
        /// 确保指定父目录下存在子目录。
        /// </summary>
        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        /// <summary>
        /// 删除测试维护记录 JSON。
        /// </summary>
        private static void DeleteMaintenanceFile()
        {
            string path = DraftMaintenanceRecordStore.GetMaintenanceAssetPathForPrefab(TestPrefabPath);
            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.DeleteAsset(path + ".meta");
            }
        }
    }
}
#endif
