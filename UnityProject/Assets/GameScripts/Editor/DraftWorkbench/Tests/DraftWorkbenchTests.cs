#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

namespace RogueCard.Editor.DraftWorkbench.Tests
{
    /// <summary>
    /// DraftWorkbench 各模块的 EditMode 单元测试，覆盖模型默认值、
    /// 叠加层创建/销毁、GameView 节点预览/应用以及绑定计划构建。
    /// </summary>
    [TestFixture]
    public class DraftWorkbenchTests
    {
        /// <summary>
        /// 每轮测试结束后销毁所有临时对象。
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            // ScriptableObject.CreateInstance 创建的对象需要显式销毁
            if (_metadata != null)
            {
                Object.DestroyImmediate(_metadata);
                _metadata = null;
            }

            if (_canvasRoot != null)
            {
                Object.DestroyImmediate(_canvasRoot);
                _canvasRoot = null;
            }

            if (_prefabRoot != null)
            {
                Object.DestroyImmediate(_prefabRoot);
                _prefabRoot = null;
            }

            if (_bindingRoot != null)
            {
                Object.DestroyImmediate(_bindingRoot);
                _bindingRoot = null;
            }
        }

        private DraftWorkbenchMetadata _metadata;
        private GameObject _canvasRoot;
        private GameObject _prefabRoot;
        private GameObject _bindingRoot;

        // ──────────────────────── DraftOverlaySettings ────────────────────────

        /// <summary>
        /// DraftOverlaySettings 默认值应为 opacity=0.5, FitMode=FitWidth, Canvas=1920x1080。
        /// </summary>
        [Test]
        public void DraftOverlaySettings_默认值正确()
        {
            var settings = new DraftOverlaySettings();

            Assert.AreEqual(0.5f, settings.Opacity, "默认 Opacity 应为 0.5");
            Assert.AreEqual(OverlayFitMode.FitWidth, settings.FitMode, "默认 FitMode 应为 FitWidth");
            Assert.AreEqual(new Vector2(1920f, 1080f), settings.CanvasReferenceResolution,
                "默认 CanvasReferenceResolution 应为 1920x1080");
        }

        // ──────────────────────── DraftApplyRecord ────────────────────────

        /// <summary>
        /// DraftWorkbenchMetadata 添加 apply 记录后可获取最新记录，
        /// 清除后最新记录应为 null。
        /// </summary>
        [Test]
        public void DraftApplyRecord_添加和清除记录()
        {
            _metadata = ScriptableObject.CreateInstance<DraftWorkbenchMetadata>();

            // 初始状态无记录
            Assert.IsNull(_metadata.GetLatestApplyRecord(), "初始状态无 apply 记录");

            // 添加一条记录
            var record = new DraftApplyRecord
            {
                RecordGuid = "test-guid-001",
                CreatedObjectPaths = new List<string> { "ChildNode" },
            };
            _metadata.AddApplyRecord(record);

            var latest = _metadata.GetLatestApplyRecord();
            Assert.IsNotNull(latest, "添加后应有最新记录");
            Assert.AreEqual("test-guid-001", latest.RecordGuid, "最新记录的 Guid 应匹配");

            // 清除最新记录
            _metadata.ClearLatestApplyRecord();
            Assert.IsNull(_metadata.GetLatestApplyRecord(), "清除后应无最新记录");
        }

        // ──────────────────────── DraftBindingReport ────────────────────────

        /// <summary>
        /// DraftBindingReport 的 Added / Skipped / Conflict 列表可正确分类条目。
        /// </summary>
        [Test]
        public void DraftBindingReport_分类条目()
        {
            var report = new DraftBindingReport();

            var added = new BindingReportEntry { Key = "StartBtn", ObjectPath = "Root/StartBtn", ComponentType = "UnityEngine.UI.Button" };
            var skipped = new BindingReportEntry { Key = "BadNode", ObjectPath = "Root/BadNode", Reason = "无法解析组件类型" };
            var conflict = new BindingReportEntry { Key = "ExistingBtn", ObjectPath = "Root/ExistingBtn", Reason = "collector 中已存在同名 key" };

            report.AddedEntries.Add(added);
            report.SkippedEntries.Add(skipped);
            report.ConflictEntries.Add(conflict);

            Assert.AreEqual(1, report.AddedEntries.Count, "AddedEntries 应含 1 条");
            Assert.AreEqual(1, report.SkippedEntries.Count, "SkippedEntries 应含 1 条");
            Assert.AreEqual(1, report.ConflictEntries.Count, "ConflictEntries 应含 1 条");

            Assert.AreEqual("StartBtn", report.AddedEntries[0].Key);
            Assert.AreEqual("BadNode", report.SkippedEntries[0].Key);
            Assert.AreEqual("ExistingBtn", report.ConflictEntries[0].Key);
        }

        // ──────────────────────── RectChangeRecord ────────────────────────

        /// <summary>
        /// RectChangeRecord 字段赋值后应正确保存。
        /// </summary>
        [Test]
        public void RectChangeRecord_序列化()
        {
            var record = new RectChangeRecord
            {
                ObjectPath = "Root/Child",
                OldAnchoredPosition = new Vector2(10f, 20f),
                NewAnchoredPosition = new Vector2(30f, 40f),
                OldSizeDelta = new Vector2(100f, 200f),
                NewSizeDelta = new Vector2(300f, 400f),
                OldAnchorMin = Vector2.zero,
                NewAnchorMin = new Vector2(0.5f, 0.5f),
                OldAnchorMax = Vector2.one,
                NewAnchorMax = new Vector2(0.5f, 0.5f),
            };

            Assert.AreEqual("Root/Child", record.ObjectPath);
            Assert.AreEqual(new Vector2(10f, 20f), record.OldAnchoredPosition);
            Assert.AreEqual(new Vector2(30f, 40f), record.NewAnchoredPosition);
            Assert.AreEqual(new Vector2(100f, 200f), record.OldSizeDelta);
            Assert.AreEqual(new Vector2(300f, 400f), record.NewSizeDelta);
            Assert.AreEqual(Vector2.zero, record.OldAnchorMin);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), record.NewAnchorMin);
            Assert.AreEqual(Vector2.one, record.OldAnchorMax);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), record.NewAnchorMax);
        }

        // ──────────────────────── DraftOverlayService ────────────────────────

        /// <summary>
        /// 创建叠加层后 canvasRoot 下应存在 "__DraftOverlay" 子对象，
        /// 调用 RemoveOverlay 后子对象应被销毁。
        /// </summary>
        [Test]
        public void DraftOverlayService_创建和移除覆盖层()
        {
            _canvasRoot = CreateCanvasRoot();
            var draftTexture = CreateDummyTexture(960, 540);
            var settings = new DraftOverlaySettings();

            // 创建叠加层
            var overlay = DraftOverlayService.CreateOverlay(_canvasRoot, draftTexture, settings);
            Assert.IsNotNull(overlay, "CreateOverlay 应返回非空对象");

            var found = DraftOverlayService.FindOverlay(_canvasRoot);
            Assert.IsNotNull(found, "应能通过 FindOverlay 找到叠加层");
            Assert.AreEqual("__DraftOverlay", found.name, "叠加层名称应为 __DraftOverlay");

            // 移除叠加层
            DraftOverlayService.RemoveOverlay(_canvasRoot);
            var afterRemove = DraftOverlayService.FindOverlay(_canvasRoot);
            Assert.IsNull(afterRemove, "移除后 FindOverlay 应返回 null");
        }

        /// <summary>
        /// 对每种 OverlayFitMode 验证 RectTransform 的 anchor 和 sizeDelta 是否正确。
        /// </summary>
        [Test]
        public void DraftOverlayService_FitMode设置_RectTransform正确()
        {
            _canvasRoot = CreateCanvasRoot();
            var draftTexture = CreateDummyTexture(1920, 1080);

            // FitWidth：垂直拉伸，水平按宽高比居中
            var settingsFitWidth = new DraftOverlaySettings { FitMode = OverlayFitMode.FitWidth };
            var overlay = DraftOverlayService.CreateOverlay(_canvasRoot, draftTexture, settingsFitWidth);
            var rt = overlay.GetComponent<RectTransform>();

            Assert.AreEqual(new Vector2(0.5f, 0f), rt.anchorMin, "FitWidth anchorMin 应为 (0.5, 0)");
            Assert.AreEqual(new Vector2(0.5f, 1f), rt.anchorMax, "FitWidth anchorMax 应为 (0.5, 1)");
            Assert.AreEqual(0f, rt.sizeDelta.y, "FitWidth sizeDelta.y 应为 0");

            Object.DestroyImmediate(overlay);

            // Fill：锚点全拉伸
            var settingsFill = new DraftOverlaySettings { FitMode = OverlayFitMode.Fill };
            overlay = DraftOverlayService.CreateOverlay(_canvasRoot, draftTexture, settingsFill);
            rt = overlay.GetComponent<RectTransform>();

            Assert.AreEqual(Vector2.zero, rt.anchorMin, "Fill anchorMin 应为 (0, 0)");
            Assert.AreEqual(Vector2.one, rt.anchorMax, "Fill anchorMax 应为 (1, 1)");
            Assert.AreEqual(Vector2.zero, rt.sizeDelta, "Fill sizeDelta 应为 (0, 0)");

            Object.DestroyImmediate(overlay);

            // FitHeight：水平拉伸，垂直按宽高比居中
            var settingsFitHeight = new DraftOverlaySettings { FitMode = OverlayFitMode.FitHeight };
            overlay = DraftOverlayService.CreateOverlay(_canvasRoot, draftTexture, settingsFitHeight);
            rt = overlay.GetComponent<RectTransform>();

            Assert.AreEqual(new Vector2(0f, 0.5f), rt.anchorMin, "FitHeight anchorMin 应为 (0, 0.5)");
            Assert.AreEqual(new Vector2(1f, 0.5f), rt.anchorMax, "FitHeight anchorMax 应为 (1, 0.5)");
            Assert.AreEqual(0f, rt.sizeDelta.x, "FitHeight sizeDelta.x 应为 0");

            Object.DestroyImmediate(overlay);

            // Stretch：与 Fill 一样全拉伸
            var settingsStretch = new DraftOverlaySettings { FitMode = OverlayFitMode.Stretch };
            overlay = DraftOverlayService.CreateOverlay(_canvasRoot, draftTexture, settingsStretch);
            rt = overlay.GetComponent<RectTransform>();

            Assert.AreEqual(Vector2.zero, rt.anchorMin, "Stretch anchorMin 应为 (0, 0)");
            Assert.AreEqual(Vector2.one, rt.anchorMax, "Stretch anchorMax 应为 (1, 1)");
            Assert.AreEqual(Vector2.zero, rt.sizeDelta, "Stretch sizeDelta 应为 (0, 0)");
        }

        // ──────────────────────── GameViewDraftBuilder ────────────────────────

        /// <summary>
        /// BuildPreview 传入 prefabRoot 中不存在的节点名时，返回的 change.IsNew 应为 true。
        /// </summary>
        [Test]
        public void GameViewDraftBuilder_Preview_检测新节点()
        {
            _prefabRoot = new GameObject("PrefabRoot");

            var descriptors = new List<UguiNodeDescriptor>
            {
                new UguiNodeDescriptor
                {
                    Name = "NewPanel",
                    ParentPath = "",
                    AnchorMin = Vector2.zero,
                    AnchorMax = Vector2.one,
                    AnchoredPosition = Vector2.zero,
                    SizeDelta = Vector2.zero,
                },
            };

            var changes = GameViewDraftBuilder.BuildPreview(_prefabRoot, descriptors);

            Assert.AreEqual(1, changes.Count, "应返回 1 条变更");
            Assert.IsTrue(changes[0].IsNew, "不存在的节点应标记为 IsNew=true");
            Assert.IsFalse(changes[0].HasConflict, "新节点不应有冲突");
        }

        /// <summary>
        /// ApplyChanges 创建新节点后，prefabRoot 下应存在对应子对象且 RectTransform 参数正确。
        /// </summary>
        [Test]
        public void GameViewDraftBuilder_Apply_创建节点()
        {
            _prefabRoot = new GameObject("PrefabRoot");

            var desc = new UguiNodeDescriptor
            {
                Name = "TestChild",
                ParentPath = "",
                AnchorMin = new Vector2(0f, 0f),
                AnchorMax = new Vector2(1f, 1f),
                AnchoredPosition = new Vector2(10f, 20f),
                SizeDelta = new Vector2(100f, 200f),
            };

            var change = new UguiNodeChange { Descriptor = desc, IsNew = true, HasConflict = false };
            var appliedPaths = GameViewDraftBuilder.ApplyChanges(_prefabRoot, new[] { change });

            Assert.AreEqual(1, appliedPaths.Count, "应应用 1 条变更");

            var child = _prefabRoot.transform.Find("TestChild");
            Assert.IsNotNull(child, "prefabRoot 下应存在 TestChild 子节点");

            var rect = child as RectTransform;
            Assert.IsNotNull(rect, "子节点应有 RectTransform");
            Assert.AreEqual(new Vector2(0f, 0f), rect.anchorMin, "anchorMin 应匹配描述符");
            Assert.AreEqual(new Vector2(1f, 1f), rect.anchorMax, "anchorMax 应匹配描述符");
            Assert.AreEqual(new Vector2(10f, 20f), rect.anchoredPosition, "anchoredPosition 应匹配描述符");
            Assert.AreEqual(new Vector2(100f, 200f), rect.sizeDelta, "sizeDelta 应匹配描述符");
        }

        // ──────────────────────── DraftBindingService ────────────────────────

        /// <summary>
        /// BuildBindingPlan 传入与 collector 已有 key 同名的节点时，
        /// 该节点应被分类到 ConflictEntries 而非 AddedEntries。
        /// <para>使用 "PanelGo" 后缀（默认规则 -> GameObject）确保规则可命中。</para>
        /// </summary>
        [Test]
        public void DraftBindingService_BuildPlan_重复Key标记跳过()
        {
            _bindingRoot = new GameObject("BindingRoot");
            var collector = _bindingRoot.AddComponent<ReferenceCollector>();
            var child = new GameObject("PanelGo");
            child.transform.SetParent(_bindingRoot.transform, false);

            // 手动向 collector.data 添加同名 key 模拟已存在状态
            collector.data.Add(new ReferenceCollectorData { key = "PanelGo", gameObject = child });

            var nodes = new List<GameObject> { child };
            var report = DraftBindingService.BuildBindingPlan(collector, nodes);

            // 由于 key 已存在，应被分类为 Conflict
            Assert.AreEqual(0, report.AddedEntries.Count, "重复 key 不应出现在 AddedEntries");
            Assert.AreEqual(1, report.ConflictEntries.Count, "重复 key 应出现在 ConflictEntries");
            Assert.AreEqual("PanelGo", report.ConflictEntries[0].Key, "Conflict key 应为 PanelGo");
        }

        // ──────────────────────── 辅助方法 ────────────────────────

        /// <summary>
        /// 创建带 Canvas 的临时根节点。
        /// </summary>
        private static GameObject CreateCanvasRoot()
        {
            var root = new GameObject("CanvasRoot", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            return root;
        }

        /// <summary>
        /// 创建指定尺寸的临时 Texture2D，用于叠加层测试。
        /// </summary>
        private static Texture2D CreateDummyTexture(int width, int height)
        {
            var tex = new Texture2D(width, height);
            tex.name = "DummyDraft";
            return tex;
        }
    }
}
#endif