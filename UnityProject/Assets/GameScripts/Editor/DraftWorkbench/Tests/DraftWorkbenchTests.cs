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
    /// 叠加层创建/销毁、Prefab 节点预览/应用以及绑定计划构建。
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

        // ──────────────────────── PrefabDraftBuilder ────────────────────────

        /// <summary>
        /// BuildPreview 传入 prefabRoot 中不存在的节点名时，返回的 change.IsNew 应为 true。
        /// </summary>
        [Test]
        public void PrefabDraftBuilder_Preview_检测新节点()
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

            var changes = PrefabDraftBuilder.BuildPreview(_prefabRoot, descriptors);

            Assert.AreEqual(1, changes.Count, "应返回 1 条变更");
            Assert.IsTrue(changes[0].IsNew, "不存在的节点应标记为 IsNew=true");
            Assert.IsFalse(changes[0].HasConflict, "新节点不应有冲突");
        }

        /// <summary>
        /// ApplyChanges 创建新节点后，prefabRoot 下应存在对应子对象且 RectTransform 参数正确。
        /// </summary>
        [Test]
        public void PrefabDraftBuilder_Apply_创建节点()
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
            var appliedPaths = PrefabDraftBuilder.ApplyChanges(_prefabRoot, new[] { change });

            Assert.AreEqual(1, appliedPaths.Count, "应应用 1 条变更");
            Assert.AreEqual("TestChild", appliedPaths[0], "创建路径应相对 prefabRoot");

            var child = _prefabRoot.transform.Find("TestChild");
            Assert.IsNotNull(child, "prefabRoot 下应存在 TestChild 子节点");

            var rect = child as RectTransform;
            Assert.IsNotNull(rect, "子节点应有 RectTransform");
            Assert.AreEqual(new Vector2(0f, 0f), rect.anchorMin, "anchorMin 应匹配描述符");
            Assert.AreEqual(new Vector2(1f, 1f), rect.anchorMax, "anchorMax 应匹配描述符");
            Assert.AreEqual(new Vector2(10f, 20f), rect.anchoredPosition, "anchoredPosition 应匹配描述符");
            Assert.AreEqual(new Vector2(100f, 200f), rect.sizeDelta, "sizeDelta 应匹配描述符");
        }

        /// <summary>
        /// BuildPreview 遇到 Unity 默认中心锚点 + 100x100 的旧节点时，应允许覆盖而不是标记为冲突。
        /// </summary>
        [Test]
        public void PrefabDraftBuilder_Preview_默认100尺寸节点允许覆盖()
        {
            _prefabRoot = new GameObject("PrefabRoot");
            var child = new GameObject("LevelPreviewMenu", typeof(RectTransform));
            child.transform.SetParent(_prefabRoot.transform, false);
            var rect = child.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(100f, 100f);

            var desc = new UguiNodeDescriptor
            {
                Name = "LevelPreviewMenu",
                ParentPath = "",
                AnchorMin = new Vector2(0f, 1f),
                AnchorMax = new Vector2(0f, 1f),
                AnchoredPosition = new Vector2(384f, -688f),
                SizeDelta = new Vector2(768f, 1376f),
            };

            var changes = PrefabDraftBuilder.BuildPreview(_prefabRoot, new[] { desc });

            Assert.AreEqual(1, changes.Count, "应返回 1 条变更");
            Assert.IsFalse(changes[0].IsNew, "已有节点应标记为更新");
            Assert.IsFalse(changes[0].HasConflict, "默认 100x100 旧节点应允许覆盖");
        }

        /// <summary>
        /// ApplyChanges 更新已有默认 100x100 节点时，应覆盖为描述符中的布局值并补齐组件。
        /// </summary>
        [Test]
        public void PrefabDraftBuilder_Apply_覆盖默认100尺寸节点()
        {
            _prefabRoot = new GameObject("PrefabRoot");
            var child = new GameObject("StartGameButtonText", typeof(RectTransform));
            child.transform.SetParent(_prefabRoot.transform, false);
            var rect = child.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(100f, 100f);

            var desc = new UguiNodeDescriptor
            {
                Name = "StartGameButtonText",
                ParentPath = "",
                AnchorMin = new Vector2(0f, 1f),
                AnchorMax = new Vector2(0f, 1f),
                AnchoredPosition = new Vector2(180.5f, -88f),
                SizeDelta = new Vector2(213f, 58f),
                ComponentTypeNames = new List<string> { "TMPro.TextMeshProUGUI" },
                Visuals = new UiNodeVisuals
                {
                    TextContent = "开始游戏",
                    FontSize = 48,
                    HasTextAlignment = true,
                    TextAlignment = TextAnchor.MiddleCenter,
                }
            };

            var appliedPaths = PrefabDraftBuilder.ApplyChanges(_prefabRoot, new[]
            {
                new UguiNodeChange { Descriptor = desc, IsNew = false, HasConflict = false }
            });

            Assert.AreEqual(1, appliedPaths.Count, "应更新 1 个节点");
            Assert.AreEqual(desc.AnchorMin, rect.anchorMin, "anchorMin 应被描述符覆盖");
            Assert.AreEqual(desc.AnchorMax, rect.anchorMax, "anchorMax 应被描述符覆盖");
            Assert.AreEqual(desc.AnchoredPosition, rect.anchoredPosition, "anchoredPosition 应被描述符覆盖");
            Assert.AreEqual(desc.SizeDelta, rect.sizeDelta, "sizeDelta 应被描述符覆盖");
            var text = child.GetComponent<TMPro.TextMeshProUGUI>();
            Assert.IsNotNull(text, "应补齐 TextMeshProUGUI 组件");
            Assert.AreEqual("开始游戏", text.text, "文本内容应被写入");
            Assert.AreEqual(48f, text.fontSize, "字号应被写入");
        }

        /// <summary>
        /// prefabRoot 位于更大场景层级下时，应用记录应使用 prefabRoot 相对路径，RevertChanges 应能恢复旧布局并删除新建节点。
        /// </summary>
        [Test]
        public void PrefabDraftBuilder_Revert_嵌套PrefabRoot使用相对路径恢复()
        {
            _canvasRoot = new GameObject("SceneRoot");
            _prefabRoot = new GameObject("PrefabRoot");
            _prefabRoot.transform.SetParent(_canvasRoot.transform, false);

            var panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(_prefabRoot.transform, false);

            var title = new GameObject("Title", typeof(RectTransform));
            title.transform.SetParent(panel.transform, false);
            var titleRect = title.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(0f, 1f);
            titleRect.anchoredPosition = new Vector2(12f, -20f);
            titleRect.sizeDelta = new Vector2(240f, 48f);

            var updateDesc = new UguiNodeDescriptor
            {
                Name = "Title",
                ParentPath = "Panel",
                AnchorMin = new Vector2(0.5f, 0.5f),
                AnchorMax = new Vector2(0.5f, 0.5f),
                AnchoredPosition = new Vector2(30f, -16f),
                SizeDelta = new Vector2(300f, 64f),
            };
            var createDesc = new UguiNodeDescriptor
            {
                Name = "StartButton",
                ParentPath = "Panel",
                AnchorMin = new Vector2(1f, 0f),
                AnchorMax = new Vector2(1f, 0f),
                AnchoredPosition = new Vector2(-80f, 40f),
                SizeDelta = new Vector2(160f, 56f),
            };

            string objectPath = PrefabDraftBuilder.GetResolvedDescriptorPath(_prefabRoot, updateDesc);
            Assert.AreEqual("Panel/Title", objectPath, "修改记录路径应相对 prefabRoot，不应包含场景父节点或 prefabRoot 名称");

            var record = new DraftApplyRecord
            {
                ModifiedRects = new List<RectChangeRecord>
                {
                    new RectChangeRecord
                    {
                        ObjectPath = objectPath,
                        OldAnchoredPosition = titleRect.anchoredPosition,
                        NewAnchoredPosition = updateDesc.AnchoredPosition,
                        OldSizeDelta = titleRect.sizeDelta,
                        NewSizeDelta = updateDesc.SizeDelta,
                        OldAnchorMin = titleRect.anchorMin,
                        NewAnchorMin = updateDesc.AnchorMin,
                        OldAnchorMax = titleRect.anchorMax,
                        NewAnchorMax = updateDesc.AnchorMax,
                    }
                }
            };

            var appliedPaths = PrefabDraftBuilder.ApplyChanges(_prefabRoot, new[]
            {
                new UguiNodeChange { Descriptor = updateDesc, IsNew = false, HasConflict = false },
                new UguiNodeChange { Descriptor = createDesc, IsNew = true, HasConflict = false },
            });
            record.CreatedObjectPaths = appliedPaths.Where(path => path == "Panel/StartButton").ToList();

            CollectionAssert.Contains(appliedPaths, "Panel/Title", "更新路径应相对 prefabRoot");
            CollectionAssert.Contains(appliedPaths, "Panel/StartButton", "创建路径应相对 prefabRoot");
            CollectionAssert.DoesNotContain(appliedPaths, "PrefabRoot/Panel/Title", "路径不应包含 prefabRoot 自身");
            Assert.AreEqual(updateDesc.AnchoredPosition, titleRect.anchoredPosition, "应用后应写入新布局");
            Assert.IsNotNull(_prefabRoot.transform.Find("Panel/StartButton"), "应用后应存在新建节点");

            Assert.IsTrue(PrefabDraftBuilder.RevertChanges(_prefabRoot, record), "使用 apply 记录应可成功回滚");
            Assert.AreEqual(new Vector2(12f, -20f), titleRect.anchoredPosition, "回滚后 anchoredPosition 应恢复旧值");
            Assert.AreEqual(new Vector2(240f, 48f), titleRect.sizeDelta, "回滚后 sizeDelta 应恢复旧值");
            Assert.AreEqual(new Vector2(0f, 1f), titleRect.anchorMin, "回滚后 anchorMin 应恢复旧值");
            Assert.AreEqual(new Vector2(0f, 1f), titleRect.anchorMax, "回滚后 anchorMax 应恢复旧值");
            Assert.IsNull(_prefabRoot.transform.Find("Panel/StartButton"), "回滚后应删除本次新建节点");
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

        // ──────────────────────── StructurePreviewColorUtility ────────────────────────

        /// <summary>
        /// 结构框按控件类型着色时，应根据 ComponentTypeNames 选择颜色；冲突节点仍优先显示冲突色。
        /// </summary>
        [Test]
        public void StructurePreviewColorUtility_结构框颜色_控件类型模式优先控件类型并保留冲突色()
        {
            var change = new UguiNodeChange
            {
                Descriptor = new UguiNodeDescriptor
                {
                    ComponentTypeNames = new List<string>
                    {
                        "UnityEngine.UI.Image",
                        "UnityEngine.UI.Button"
                    }
                },
                IsNew = true,
                HasConflict = false
            };

            Color normalColor = StructurePreviewColorUtility.GetStructurePreviewColor(
                StructurePreviewColorMode.ComponentType, change);

            Assert.AreEqual(StructurePreviewColorUtility.ButtonColor, normalColor,
                "Button 节点在控件类型模式下应使用 Button 颜色");

            change.HasConflict = true;
            Color conflictColor = StructurePreviewColorUtility.GetStructurePreviewColor(
                StructurePreviewColorMode.ComponentType, change);

            Assert.AreEqual(StructurePreviewColorUtility.ConflictColor, conflictColor,
                "冲突节点应优先使用冲突色");
        }

        /// <summary>
        /// 结构框按变更状态着色时，应按 IsNew 切换新建/已存在颜色，而非控件类型颜色。
        /// </summary>
        [Test]
        public void StructurePreviewColorUtility_结构框颜色_变更状态模式按新建状态着色()
        {
            var change = new UguiNodeChange
            {
                Descriptor = new UguiNodeDescriptor
                {
                    ComponentTypeNames = new List<string> { "UnityEngine.UI.Button" }
                },
                IsNew = true,
                HasConflict = false
            };

            Color newColor = StructurePreviewColorUtility.GetStructurePreviewColor(
                StructurePreviewColorMode.ChangeStatus, change);

            Assert.AreEqual(StructurePreviewColorUtility.NewColor, newColor,
                "新建节点在变更状态模式下应使用新建色");

            change.IsNew = false;
            Color existingColor = StructurePreviewColorUtility.GetStructurePreviewColor(
                StructurePreviewColorMode.ChangeStatus, change);

            Assert.AreEqual(StructurePreviewColorUtility.ExistingColor, existingColor,
                "已存在节点在变更状态模式下应使用更新色");
        }

        /// <summary>
        /// 横屏 source 在横向 pane 中应按可用宽度优先自动适配，并保留 pane 高度作为滚动内容高度。
        /// </summary>
        [Test]
        public void StructurePreviewLayoutUtility_横屏自动适配()
        {
            StructurePreviewLayoutResult result = StructurePreviewLayoutUtility.CalculateLayout(
                new Vector2(480f, 360f),
                new Vector2(1920f, 1080f),
                1f);

            Assert.IsTrue(result.IsValid, "有效输入应返回有效布局结果");
            Assert.That(result.Scale, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(result.ImageWidth, Is.EqualTo(480f).Within(0.01f));
            Assert.That(result.ImageHeight, Is.EqualTo(270f).Within(0.01f));
            Assert.That(result.ContentWidth, Is.EqualTo(480f).Within(0.01f));
            Assert.That(result.ContentHeight, Is.EqualTo(360f).Within(0.01f));
        }

        /// <summary>
        /// 竖屏 source 在纵向 pane 中应按可用高度优先自动适配，并在较宽 pane 中保持水平留白。
        /// </summary>
        [Test]
        public void StructurePreviewLayoutUtility_竖屏自动适配()
        {
            StructurePreviewLayoutResult result = StructurePreviewLayoutUtility.CalculateLayout(
                new Vector2(420f, 620f),
                new Vector2(1080f, 1920f),
                1f);

            Assert.IsTrue(result.IsValid, "有效输入应返回有效布局结果");
            Assert.That(result.Scale, Is.EqualTo(620f / 1920f).Within(0.0001f));
            Assert.That(result.ImageWidth, Is.EqualTo(348.75f).Within(0.01f));
            Assert.That(result.ImageHeight, Is.EqualTo(620f).Within(0.01f));
            Assert.That(result.ContentWidth, Is.EqualTo(420f).Within(0.01f));
            Assert.That(result.ContentHeight, Is.EqualTo(620f).Within(0.01f));
        }

        /// <summary>
        /// 手动缩放倍率大于 1 时，应扩大绘制尺寸并让滚动内容超过 pane 可视区。
        /// </summary>
        [Test]
        public void StructurePreviewLayoutUtility_手动缩放会扩大滚动内容()
        {
            StructurePreviewLayoutResult result = StructurePreviewLayoutUtility.CalculateLayout(
                new Vector2(420f, 620f),
                new Vector2(1080f, 1920f),
                1.5f);

            Assert.IsTrue(result.IsValid, "有效输入应返回有效布局结果");
            Assert.That(result.Scale, Is.EqualTo(0.484375f).Within(0.0001f));
            Assert.That(result.ImageWidth, Is.EqualTo(523.125f).Within(0.01f));
            Assert.That(result.ImageHeight, Is.EqualTo(930f).Within(0.01f));
            Assert.That(result.ContentWidth, Is.GreaterThan(420f));
            Assert.That(result.ContentHeight, Is.GreaterThan(620f));
        }

        /// <summary>
        /// 当存在当前设计图尺寸时，即使节点带有 source canvas，也应优先使用设计图尺寸作为结构框预览坐标系。
        /// </summary>
        [Test]
        public void StructurePreviewLayoutUtility_优先使用设计图尺寸()
        {
            var drawableChanges = new List<UguiNodeChange>
            {
                new UguiNodeChange
                {
                    Descriptor = new UguiNodeDescriptor
                    {
                        SourceBounds = new Rect(10f, 20f, 100f, 200f),
                        SourceCanvasSize = new Vector2(1920f, 1080f)
                    }
                }
            };

            Vector2 sourceSize = StructurePreviewLayoutUtility.ResolveSourceSize(
                drawableChanges,
                new Vector2(720f, 1280f),
                new Vector2(1920f, 1080f));

            Assert.AreEqual(new Vector2(720f, 1280f), sourceSize,
                "结构框叠加在当前设计图上时，应优先匹配设计图尺寸");
        }

        /// <summary>
        /// 当节点未提供 source canvas 尺寸时，应优先回退到设计图尺寸。
        /// </summary>
        [Test]
        public void StructurePreviewLayoutUtility_无SourceCanvas时回退到设计图尺寸()
        {
            var drawableChanges = new List<UguiNodeChange>
            {
                new UguiNodeChange
                {
                    Descriptor = new UguiNodeDescriptor
                    {
                        SourceBounds = new Rect(10f, 20f, 100f, 200f),
                        SourceCanvasSize = Vector2.zero
                    }
                }
            };

            Vector2 sourceSize = StructurePreviewLayoutUtility.ResolveSourceSize(
                drawableChanges,
                new Vector2(720f, 1280f),
                new Vector2(1920f, 1080f));

            Assert.AreEqual(new Vector2(720f, 1280f), sourceSize,
                "缺少 source canvas 时应回退到设计图尺寸");
        }

        /// <summary>
        /// 当既没有 source canvas 也没有设计图尺寸时，应回退到当前画布尺寸。
        /// </summary>
        [Test]
        public void StructurePreviewLayoutUtility_无SourceCanvas和设计图时回退到画布尺寸()
        {
            var drawableChanges = new List<UguiNodeChange>
            {
                new UguiNodeChange
                {
                    Descriptor = new UguiNodeDescriptor
                    {
                        SourceBounds = new Rect(10f, 20f, 100f, 200f),
                        SourceCanvasSize = Vector2.zero
                    }
                }
            };

            Vector2 sourceSize = StructurePreviewLayoutUtility.ResolveSourceSize(
                drawableChanges,
                Vector2.zero,
                new Vector2(1920f, 1080f));

            Assert.AreEqual(new Vector2(1920f, 1080f), sourceSize,
                "当没有 source canvas 和设计图时应回退到当前画布尺寸");
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
