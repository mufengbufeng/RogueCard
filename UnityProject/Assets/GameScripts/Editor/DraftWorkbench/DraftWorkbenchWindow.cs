#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RogueCard.Editor.DraftWorkbench
{
    /// <summary>
    /// 草稿工作台编辑器窗口，提供可视化的 UI 草稿叠加、节点生成、绑定应用及回滚操作。
    /// <para>通过菜单 <c>RogueCard/Draft Workbench</c> 打开。</para>
    /// </summary>
    public class DraftWorkbenchWindow : EditorWindow
    {
        /// <summary>
        /// GameView Prefab 中必需的 UGUI 节点名称列表，源自 <c>GameViewPrefabContractTests.RequiredKeys</c>。
        /// </summary>
        private static readonly string[] RequiredNodeNames =
        {
            "BgImage", "BattlePanel", "RewardPanel", "PlayerStatusPanel", "InfoText",
            "PlayerHpFill", "PlayerHpText", "PlayerArmorText", "PlayerEnergyFill",
            "PlayerEnergyText", "PlayerBuffBar", "MonsterRect", "CardSc", "DropZone",
            "PreviewLayer", "EndBtn", "FailToast", "RewardConfirmBtn", "HandCardTemplate",
            "MonsterItemTemplate", "BuffIconTemplate", "IntentIconTemplate"
        };

        /// <summary>
        /// 叠加层适配模式的显示名称数组，与 <see cref="OverlayFitMode"/> 枚举一一对应。
        /// </summary>
        private static readonly string[] FitModeNames =
        {
            "Fill（等比裁剪填满）",
            "FitWidth（等比宽度适配）",
            "FitHeight（等比高度适配）",
            "Stretch（拉伸填满）"
        };

        /// <summary>
        /// 持久化的草稿工作台元数据，保存源图、prefab 关联及 apply 历史。
        /// </summary>
        private DraftWorkbenchMetadata _metadata;

        /// <summary>
        /// 当前选中的 Prefab Asset 引用（仅用于 ObjectField 显示和资源路径查找）。
        /// </summary>
        private GameObject _prefabAsset;

        /// <summary>
        /// 通过 PrefabUtility.LoadPrefabContents 加载的可编辑实例，
        /// 所有层级操作（overlay、preview、apply、revert）均基于此实例。
        /// </summary>
        private GameObject _editableInstance;

        /// <summary>
        /// 用于叠加层显示的草稿图片纹理。
        /// </summary>
        private Texture2D _draftImage;

        /// <summary>
        /// 叠加层显示设置（不透明度、适配模式等）。
        /// </summary>
        private DraftOverlaySettings _overlaySettings = new DraftOverlaySettings();

        /// <summary>
        /// 节点列表区域的滚动位置。
        /// </summary>
        private Vector2 _scrollPos;

        /// <summary>
        /// 最近一次预览生成的节点变更列表。
        /// </summary>
        private List<UguiNodeChange> _previewChanges = new List<UguiNodeChange>();

        /// <summary>
        /// 最近一次绑定操作的报告。
        /// </summary>
        private DraftBindingReport _bindingReport;

        /// <summary>
        /// 叠加层适配模式 Popup 当前选中索引。
        /// </summary>
        private int _fitModeIndex = 1; // 默认 FitWidth

        /// <summary>
        /// 通过 Unity 菜单打开草稿工作台窗口。
        /// </summary>
        [MenuItem("RogueCard/Draft Workbench")]
        private static void OpenWindow()
        {
            var window = GetWindow<DraftWorkbenchWindow>("Draft Workbench");
            window.minSize = new Vector2(360f, 500f);
        }

        /// <summary>
        /// 窗口启用时尝试恢复上次的 metadata。
        /// </summary>
        private void OnEnable()
        {
            // 尝试从 EditorPrefs 恢复 metadata 路径
            var savedPath = EditorPrefs.GetString("DraftWorkbench_MetadataPath", "");
            if (!string.IsNullOrEmpty(savedPath))
            {
                _metadata = AssetDatabase.LoadAssetAtPath<DraftWorkbenchMetadata>(savedPath);
                if (_metadata != null)
                {
                    _overlaySettings = _metadata.OverlaySettings ?? new DraftOverlaySettings();
                    _fitModeIndex = (int)_overlaySettings.FitMode;

                    // 恢复 draft image
                    if (!string.IsNullOrEmpty(_metadata.SourceImageAssetGuid))
                    {
                        var imagePath = AssetDatabase.GUIDToAssetPath(_metadata.SourceImageAssetGuid);
                        _draftImage = AssetDatabase.LoadAssetAtPath<Texture2D>(imagePath);
                    }
                }
            }
        }

        /// <summary>
        /// 窗口禁用时清理临时对象，释放可编辑 prefab 实例。
        /// </summary>
        private void OnDisable()
        {
            UnloadEditableInstance();
        }

        /// <summary>
        /// 释放当前的可编辑 prefab 实例，同时移除叠加层。
        /// </summary>
        private void UnloadEditableInstance()
        {
            if (_editableInstance != null)
            {
                DraftOverlayService.RemoveOverlay(_editableInstance);
                PrefabUtility.UnloadPrefabContents(_editableInstance);
                _editableInstance = null;
            }
        }

        /// <summary>
        /// 绘制编辑器窗口 UI。
        /// </summary>
        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            DrawTopSection();
            DrawOverlaySection();
            DrawSeparator();
            DrawNodeSection();
            DrawActionButtons();
            DrawReportSection();

            EditorGUILayout.EndScrollView();
        }

        // ─────────────────────── UI 绘制区域 ───────────────────────

        /// <summary>
        /// 绘制顶部区域：Prefab 选择和 Draft Image 选择。
        /// </summary>
        private void DrawTopSection()
        {
            EditorGUILayout.LabelField("目标 Prefab", EditorStyles.boldLabel);

            // Prefab ObjectField — 始终显示 Asset 引用
            var newPrefab = (GameObject)EditorGUILayout.ObjectField(
                "GameView Prefab",
                _prefabAsset,
                typeof(GameObject),
                false);

            if (newPrefab != _prefabAsset)
            {
                // 释放旧的可编辑实例
                UnloadEditableInstance();
                _prefabAsset = newPrefab;

                if (_prefabAsset != null)
                {
                    LoadPrefab();
                }
                else
                {
                    _metadata = null;
                    _previewChanges.Clear();
                    _bindingReport = null;
                }
            }

            // Draft Image ObjectField
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("草稿图片", EditorStyles.boldLabel);

            _draftImage = (Texture2D)EditorGUILayout.ObjectField(
                "Draft Image",
                _draftImage,
                typeof(Texture2D),
                false);

            // 更新 metadata 中的源图 GUID
            if (_draftImage != null && _metadata != null)
            {
                var assetPath = AssetDatabase.GetAssetPath(_draftImage);
                _metadata.SourceImageAssetGuid = AssetDatabase.AssetPathToGUID(assetPath);
                _metadata.SourceImageSize = new Vector2(_draftImage.width, _draftImage.height);
                EditorUtility.SetDirty(_metadata);
            }

            EditorGUILayout.Space(4f);
        }

        /// <summary>
        /// 绘制叠加层设置区域：不透明度滑块、适配模式 Popup、显示/隐藏按钮。
        /// </summary>
        private void DrawOverlaySection()
        {
            EditorGUILayout.LabelField("叠加层设置", EditorStyles.boldLabel);

            // 不透明度滑块
            _overlaySettings.Opacity = EditorGUILayout.Slider(
                "不透明度",
                _overlaySettings.Opacity,
                0f, 1f);

            // 适配模式 Popup
            _fitModeIndex = EditorGUILayout.Popup(
                "适配模式",
                _fitModeIndex,
                FitModeNames);
            _overlaySettings.FitMode = (OverlayFitMode)_fitModeIndex;

            EditorGUILayout.Space(4f);

            // 显示/隐藏按钮
            EditorGUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("显示叠加层", GUILayout.Height(28f)))
                {
                    ShowOverlay();
                }

                if (GUILayout.Button("隐藏叠加层", GUILayout.Height(28f)))
                {
                    HideOverlay();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 绘制水平分隔线。
        /// </summary>
        private void DrawSeparator()
        {
            EditorGUILayout.Space(8f);
            var rect = GUILayoutUtility.GetRect(1f, 1f);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 1f));
            EditorGUILayout.Space(8f);
        }

        /// <summary>
        /// 绘制节点生成区域：显示已知 GameView 节点列表及其存在状态。
        /// </summary>
        private void DrawNodeSection()
        {
            EditorGUILayout.LabelField("UGUI 节点列表", EditorStyles.boldLabel);

            if (_editableInstance == null)
            {
                EditorGUILayout.HelpBox("请先选择 GameView Prefab。", MessageType.Info);
                return;
            }

            for (int i = 0; i < RequiredNodeNames.Length; i++)
            {
                var name = RequiredNodeNames[i];
                var exists = _editableInstance.transform.Find(name) != null;
                var icon = exists ? "✓" : "✗"; // ✓ / ✗
                var style = exists
                    ? new GUIStyle(EditorStyles.label) { normal = { textColor = Color.green } }
                    : new GUIStyle(EditorStyles.label) { normal = { textColor = Color.red } };

                EditorGUILayout.LabelField($"  {icon}  {name}", style);
            }

            EditorGUILayout.Space(4f);
        }

        /// <summary>
        /// 绘制操作按钮区域：Preview Changes、Apply All、Revert Last。
        /// </summary>
        private void DrawActionButtons()
        {
            EditorGUILayout.LabelField("操作", EditorStyles.boldLabel);

            EditorGUI.BeginDisabledGroup(_editableInstance == null);
            {
                if (GUILayout.Button("预览变更 (Preview Changes)", GUILayout.Height(28f)))
                {
                    PreviewChanges();
                }

                if (GUILayout.Button("应用全部 (Apply All)", GUILayout.Height(28f)))
                {
                    ApplyAll();
                }

                EditorGUI.BeginDisabledGroup(_metadata == null || _metadata.GetLatestApplyRecord() == null);
                {
                    if (GUILayout.Button("回滚上一次 (Revert Last)", GUILayout.Height(28f)))
                    {
                        RevertLast();
                    }
                }
                EditorGUI.EndDisabledGroup();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(4f);
        }

        /// <summary>
        /// 绘制底部报告区域：显示 Apply 报告和 Binding 报告。
        /// </summary>
        private void DrawReportSection()
        {
            // 预览变更报告
            if (_previewChanges != null && _previewChanges.Count > 0)
            {
                EditorGUILayout.LabelField("预览变更报告", EditorStyles.boldLabel);

                int newCount = _previewChanges.Count(c => c.IsNew);
                int updateCount = _previewChanges.Count(c => !c.IsNew && !c.HasConflict);
                int conflictCount = _previewChanges.Count(c => c.HasConflict);

                EditorGUILayout.LabelField($"  新建: {newCount}  |  更新: {updateCount}  |  冲突: {conflictCount}");

                foreach (var change in _previewChanges)
                {
                    string status = change.HasConflict
                        ? "[冲突]"
                        : change.IsNew
                            ? "[新建]"
                            : "[更新]";
                    EditorGUILayout.LabelField($"    {status} {change.Descriptor.Name}");
                }

                EditorGUILayout.Space(4f);
            }

            // 绑定报告
            if (_bindingReport != null)
            {
                EditorGUILayout.LabelField("绑定报告", EditorStyles.boldLabel);

                EditorGUILayout.LabelField($"  新增绑定: {_bindingReport.AddedEntries.Count}");
                EditorGUILayout.LabelField($"  跳过: {_bindingReport.SkippedEntries.Count}");
                EditorGUILayout.LabelField($"  冲突: {_bindingReport.ConflictEntries.Count}");

                if (_bindingReport.MissingComponentWarnings.Count > 0)
                {
                    EditorGUILayout.Space(2f);
                    EditorGUILayout.LabelField("  缺少组件警告:", EditorStyles.wordWrappedLabel);
                    foreach (var warning in _bindingReport.MissingComponentWarnings)
                    {
                        EditorGUILayout.LabelField($"    - {warning}", EditorStyles.wordWrappedLabel);
                    }
                }
            }
        }

        // ─────────────────────── 功能方法 ───────────────────────

        /// <summary>
        /// 加载 prefab 到可编辑实例，并创建或加载对应的 metadata。
        /// <para>使用 <see cref="PrefabUtility.LoadPrefabContents"/> 创建内存中的可编辑副本，
        /// 避免直接修改 Prefab Asset 导致 Unity 报错。</para>
        /// </summary>
        private void LoadPrefab()
        {
            if (_prefabAsset == null) return;

            // 获取 prefab 资源路径
            var prefabPath = AssetDatabase.GetAssetPath(_prefabAsset);
            if (string.IsNullOrEmpty(prefabPath)) return;

            // 释放旧实例
            UnloadEditableInstance();

            // 加载可编辑实例（内存中，不在场景中显示）
            _editableInstance = PrefabUtility.LoadPrefabContents(prefabPath);

            // 创建或加载 metadata
            _metadata = DraftWorkbenchMetadata.CreateOrLoad(prefabPath);
            if (_metadata != null)
            {
                _overlaySettings = _metadata.OverlaySettings ?? new DraftOverlaySettings();
                _fitModeIndex = (int)_overlaySettings.FitMode;

                // 保存 metadata 路径到 EditorPrefs 以便下次恢复
                var metaPath = _metadata.GetMetadataAssetPath();
                if (!string.IsNullOrEmpty(metaPath))
                {
                    EditorPrefs.SetString("DraftWorkbench_MetadataPath", metaPath);
                }

                // 恢复 draft image
                if (!string.IsNullOrEmpty(_metadata.SourceImageAssetGuid) && _draftImage == null)
                {
                    var imagePath = AssetDatabase.GUIDToAssetPath(_metadata.SourceImageAssetGuid);
                    _draftImage = AssetDatabase.LoadAssetAtPath<Texture2D>(imagePath);
                }
            }

            Repaint();
        }

        /// <summary>
        /// 显示草稿叠加层，调用 <see cref="DraftOverlayService.CreateOverlay"/>。
        /// </summary>
        private void ShowOverlay()
        {
            if (_editableInstance == null)
            {
                Debug.LogWarning("[DraftWorkbench] 请先选择 GameView Prefab。");
                return;
            }

            if (_draftImage == null)
            {
                Debug.LogWarning("[DraftWorkbench] 请先选择 Draft Image。");
                return;
            }

            DraftOverlayService.CreateOverlay(_editableInstance, _draftImage, _overlaySettings);
            Debug.Log("[DraftWorkbench] 叠加层已显示。");
        }

        /// <summary>
        /// 隐藏草稿叠加层，调用 <see cref="DraftOverlayService.RemoveOverlay"/>。
        /// </summary>
        private void HideOverlay()
        {
            if (_editableInstance == null) return;

            DraftOverlayService.RemoveOverlay(_editableInstance);
            Debug.Log("[DraftWorkbench] 叠加层已隐藏。");
        }

        /// <summary>
        /// 预览节点变更，调用 <see cref="GameViewDraftBuilder.BuildPreview"/>。
        /// </summary>
        private void PreviewChanges()
        {
            if (_editableInstance == null)
            {
                Debug.LogWarning("[DraftWorkbench] 请先选择 GameView Prefab。");
                return;
            }

            // 为缺失节点生成描述符
            var descriptors = new List<UguiNodeDescriptor>();
            for (int i = 0; i < RequiredNodeNames.Length; i++)
            {
                var name = RequiredNodeNames[i];
                var existing = _editableInstance.transform.Find(name);
                if (existing != null) continue;

                descriptors.Add(new UguiNodeDescriptor
                {
                    Name = name,
                    ParentPath = "",
                    AnchorMin = Vector2.zero,
                    AnchorMax = Vector2.one,
                    AnchoredPosition = Vector2.zero,
                    SizeDelta = Vector2.zero,
                });
            }

            _previewChanges = GameViewDraftBuilder.BuildPreview(_editableInstance, descriptors);
            Debug.Log($"[DraftWorkbench] 预览完成：{_previewChanges.Count} 个变更。");
            Repaint();
        }

        /// <summary>
        /// 应用全部变更：执行 builder + binding + 保存 metadata + 保存 prefab。
        /// <para>Apply 前先移除叠加层，确保草稿覆盖层不会被写入运行时 prefab。</para>
        /// </summary>
        private void ApplyAll()
        {
            if (_editableInstance == null)
            {
                Debug.LogWarning("[DraftWorkbench] 请先选择 GameView Prefab。");
                return;
            }

            // 先预览
            PreviewChanges();

            if (_previewChanges == null || _previewChanges.Count == 0)
            {
                Debug.Log("[DraftWorkbench] 没有需要应用的变更。");
                return;
            }

            // Apply 前移除叠加层，避免将草稿覆盖层写入运行时 prefab
            DraftOverlayService.RemoveOverlay(_editableInstance);

            // 应用变更
            var appliedPaths = GameViewDraftBuilder.ApplyChanges(_editableInstance, _previewChanges);

            // 构建 apply 记录
            var record = new DraftApplyRecord
            {
                RecordGuid = Guid.NewGuid().ToString(),
                AppliedAt = DateTime.Now,
                CreatedObjectPaths = new List<string>(),
                ModifiedRects = new List<RectChangeRecord>(),
                AddedCollectorKeys = new List<string>(),
                AddedScriptFields = new List<string>(),
            };

            // 区分新建和修改的路径
            foreach (var change in _previewChanges)
            {
                if (change.Descriptor == null) continue;
                if (change.HasConflict) continue;

                if (change.IsNew)
                {
                    record.CreatedObjectPaths.Add(change.Descriptor.Name);
                }
                else
                {
                    record.ModifiedRects.Add(new RectChangeRecord
                    {
                        ObjectPath = change.Descriptor.Name,
                        NewAnchoredPosition = change.Descriptor.AnchoredPosition,
                        NewSizeDelta = change.Descriptor.SizeDelta,
                        NewAnchorMin = change.Descriptor.AnchorMin,
                        NewAnchorMax = change.Descriptor.AnchorMax,
                    });
                }
            }

            // 执行绑定
            try
            {
                var collector = _editableInstance.GetComponent<ReferenceCollector>();
                if (collector != null)
                {
                    var nodes = new List<GameObject>();
                    foreach (var path in appliedPaths)
                    {
                        var t = _editableInstance.transform.Find(path);
                        if (t != null) nodes.Add(t.gameObject);
                    }

                    _bindingReport = DraftBindingService.BuildBindingPlan(collector, nodes);
                    DraftBindingService.ApplyBindings(collector, _bindingReport, nodes);

                    record.AddedCollectorKeys = _bindingReport.AddedEntries
                        .Select(e => e.Key)
                        .ToList();
                    record.AddedScriptFields = _bindingReport.AddedScriptFields;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DraftWorkbench] 绑定过程出错: {ex.Message}");
            }

            // 保存 metadata
            if (_metadata != null)
            {
                _metadata.AddApplyRecord(record);
                _metadata.LastBindingReport = _bindingReport;
                EditorUtility.SetDirty(_metadata);
                AssetDatabase.SaveAssets();
            }

            // 将可编辑实例写回 prefab asset
            var prefabPath = AssetDatabase.GetAssetPath(_prefabAsset);
            if (!string.IsNullOrEmpty(prefabPath))
            {
                PrefabUtility.SaveAsPrefabAsset(_editableInstance, prefabPath);
                Debug.Log($"[DraftWorkbench] Prefab 已保存: {prefabPath}");
            }

            Debug.Log($"[DraftWorkbench] 应用完成：{appliedPaths.Count} 个节点变更。");
            Repaint();
        }

        /// <summary>
        /// 回滚上一次应用操作，调用 <see cref="GameViewDraftBuilder.RevertChanges"/>。
        /// </summary>
        private void RevertLast()
        {
            if (_editableInstance == null)
            {
                Debug.LogWarning("[DraftWorkbench] 请先选择 GameView Prefab。");
                return;
            }

            if (_metadata == null)
            {
                Debug.LogWarning("[DraftWorkbench] 没有可回滚的记录。");
                return;
            }

            var record = _metadata.GetLatestApplyRecord();
            if (record == null)
            {
                Debug.LogWarning("[DraftWorkbench] 没有可回滚的记录。");
                return;
            }

            bool success = GameViewDraftBuilder.RevertChanges(_editableInstance, record);
            if (success)
            {
                _metadata.ClearLatestApplyRecord();
                EditorUtility.SetDirty(_metadata);
                AssetDatabase.SaveAssets();

                // 将回滚后的可编辑实例写回 prefab asset
                var prefabPath = AssetDatabase.GetAssetPath(_prefabAsset);
                if (!string.IsNullOrEmpty(prefabPath))
                {
                    PrefabUtility.SaveAsPrefabAsset(_editableInstance, prefabPath);
                }

                _previewChanges.Clear();
                _bindingReport = null;
                Debug.Log("[DraftWorkbench] 回滚成功。");
            }
            else
            {
                Debug.LogError("[DraftWorkbench] 回滚失败，存在冲突。请检查 Console 中的警告信息。");
            }

            Repaint();
        }
    }
}
#endif
