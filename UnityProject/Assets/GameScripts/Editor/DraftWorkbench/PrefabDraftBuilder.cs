#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace RogueCard.Editor.DraftWorkbench
{
    /// <summary>
    /// UGUI 节点描述符，定义一个目标节点的布局参数与需要挂载的组件。
    /// </summary>
    [Serializable]
    public class UguiNodeDescriptor
    {
        /// <summary>
        /// 节点名称。
        /// </summary>
        public string Name;

        /// <summary>
        /// 父节点在 hierarchy 中的路径。为空时表示挂接到根节点直接子级。
        /// </summary>
        public string ParentPath;

        /// <summary>
        /// 最小锚点。
        /// </summary>
        public Vector2 AnchorMin;

        /// <summary>
        /// 最大锚点。
        /// </summary>
        public Vector2 AnchorMax;

        /// <summary>
        /// 锚点位置。
        /// </summary>
        public Vector2 AnchoredPosition;

        /// <summary>
        /// 尺寸增量。
        /// </summary>
        public Vector2 SizeDelta;

        /// <summary>
        /// 要添加到该节点的组件类型全名列表，例如 "UnityEngine.UI.Image"。
        /// </summary>
        public List<string> ComponentTypeNames = new List<string>();

        /// <summary>
        /// 节点视觉信息（颜色/文本/不透明度），可为 null。
        /// </summary>
        public UiNodeVisuals Visuals;

        /// <summary>
        /// 布局组信息（HorizontalLayoutGroup / VerticalLayoutGroup），可为 null。
        /// </summary>
        public LayoutGroupInfo LayoutInfo;

        /// <summary>
        /// 是否包含原始设计图坐标系中的识别框。
        /// </summary>
        public bool HasSourceBounds;

        /// <summary>
        /// 原始设计图坐标系中的识别框，原点位于左上角。
        /// </summary>
        public Rect SourceBounds;

        /// <summary>
        /// 原始设计图坐标系的画布尺寸，用于预览框比例换算。
        /// </summary>
        public Vector2 SourceCanvasSize;
    }

    /// <summary>
    /// UGUI 节点变更条目，包含描述符及冲突/新建标记。
    /// </summary>
    [Serializable]
    public class UguiNodeChange
    {
        /// <summary>
        /// 目标节点描述符。
        /// </summary>
        public UguiNodeDescriptor Descriptor;

        /// <summary>
        /// 是否为新建节点。为 false 时表示已存在节点，仅更新布局。
        /// </summary>
        public bool IsNew;

        /// <summary>
        /// 是否与用户手动修改产生冲突。冲突条目在应用时会被跳过。
        /// </summary>
        public bool HasConflict;
    }

    /// <summary>
    /// Prefab 草稿构建器，负责将 <see cref="UguiNodeDescriptor"/> 列表预览/应用/回滚到 Prefab 根节点。
    /// <para>不引入任何运行时依赖，仅在编辑器环境下使用。</para>
    /// </summary>
    public static class PrefabDraftBuilder
    {
        /// <summary>
        /// 构建预览：遍历描述符，在 <paramref name="prefabRoot"/> 中查找现有节点，
        /// 判断每个条目是新建还是更新，并检测是否存在与手动修改的冲突。
        /// <para>本方法不会实际修改 prefab。</para>
        /// </summary>
        /// <param name="prefabRoot">Prefab 根节点。</param>
        /// <param name="descriptors">目标节点描述符集合。</param>
        /// <returns>每个描述符对应的变更预览列表。</returns>
        public static List<UguiNodeChange> BuildPreview(
            GameObject prefabRoot,
            IEnumerable<UguiNodeDescriptor> descriptors)
        {
            if (prefabRoot == null) throw new ArgumentNullException(nameof(prefabRoot));
            if (descriptors == null) throw new ArgumentNullException(nameof(descriptors));

            var changes = new List<UguiNodeChange>();

            foreach (var desc in descriptors)
            {
                if (desc == null || string.IsNullOrEmpty(desc.Name))
                    continue;

                var change = new UguiNodeChange { Descriptor = desc };

                Transform existing = FindChildByPath(prefabRoot.transform, desc.ParentPath, desc.Name);

                if (existing == null)
                {
                    change.IsNew = true;
                    change.HasConflict = false;
                }
                else
                {
                    change.IsNew = false;
                    change.HasConflict = DetectRectConflict(existing, desc);
                }

                changes.Add(change);
            }

            return changes;
        }

        /// <summary>
        /// 应用变更：根据 <paramref name="changes"/> 中每条记录的状态执行创建、更新或跳过。
        /// <list type="bullet">
        ///   <item><see cref="UguiNodeChange.IsNew"/>: 创建新 GameObject，设置 RectTransform，添加组件。</item>
        ///   <item>已存在且无冲突: 更新 RectTransform 参数。</item>
        ///   <item><see cref="UguiNodeChange.HasConflict"/>: 跳过，不做任何修改。</item>
        /// </list>
        /// </summary>
        /// <param name="prefabRoot">Prefab 根节点。</param>
        /// <param name="changes">变更预览列表。</param>
        /// <returns>实际创建或修改的对象相对 prefabRoot 的层级路径列表。</returns>
        public static List<string> ApplyChanges(
            GameObject prefabRoot,
            IEnumerable<UguiNodeChange> changes)
        {
            if (prefabRoot == null) throw new ArgumentNullException(nameof(prefabRoot));
            if (changes == null) throw new ArgumentNullException(nameof(changes));

            var appliedPaths = new List<string>();

            foreach (var change in changes)
            {
                if (change?.Descriptor == null)
                    continue;

                var desc = change.Descriptor;

                if (change.HasConflict)
                    continue;

                if (change.IsNew)
                {
                    Transform parent = string.IsNullOrEmpty(desc.ParentPath)
                        ? prefabRoot.transform
                        : prefabRoot.transform.Find(desc.ParentPath);

                    if (parent == null)
                    {
                        Debug.LogWarning($"[PrefabDraftBuilder] 找不到父节点路径 '{desc.ParentPath}'，跳过创建 '{desc.Name}'。");
                        continue;
                    }

                    var go = UguiElementFactory.CreateElement(desc.Name, parent, desc);
                    ApplyLayoutGroup(go, desc.LayoutInfo);

                    appliedPaths.Add(GetHierarchyPath(prefabRoot.transform, go.transform));
                }
                else
                {
                    Transform existing = FindChildByPath(prefabRoot.transform, desc.ParentPath, desc.Name);
                    if (existing == null)
                    {
                        Debug.LogWarning($"[PrefabDraftBuilder] 预期存在的节点 '{desc.Name}' 未找到，跳过更新。");
                        continue;
                    }

                    UguiElementFactory.EnsureComponents(existing.gameObject, desc);
                    var rect = existing as RectTransform ?? existing.gameObject.GetComponent<RectTransform>();
                    UguiElementFactory.ApplyRectTransform(rect, desc);
                    UguiElementFactory.ApplyVisuals(existing.gameObject, desc.Visuals);
                    ApplyLayoutGroup(existing.gameObject, desc.LayoutInfo);

                    appliedPaths.Add(GetHierarchyPath(prefabRoot.transform, existing));
                }
            }

            return appliedPaths;
        }

        /// <summary>
        /// 回滚变更：根据 <paramref name="record"/> 中记录的信息还原 prefab 至应用前状态。
        /// <list type="bullet">
        ///   <item>删除 <see cref="DraftApplyRecord.CreatedObjectPaths"/> 中记录的创建对象。</item>
        ///   <item>恢复 <see cref="DraftApplyRecord.ModifiedRects"/> 中记录的 RectTransform 参数。</item>
        /// </list>
        /// <para>回滚前会检查一致性——如果对象已被用户手动修改，则认为存在冲突并返回 false。</para>
        /// </summary>
        /// <param name="prefabRoot">Prefab 根节点。</param>
        /// <param name="record">应用操作记录。</param>
        /// <returns>回滚成功返回 true；存在冲突或对象缺失时返回 false。</returns>
        public static bool RevertChanges(GameObject prefabRoot, DraftApplyRecord record)
        {
            if (prefabRoot == null) throw new ArgumentNullException(nameof(prefabRoot));
            if (record == null) throw new ArgumentNullException(nameof(record));

            // 阶段一：验证一致性 — 检查 ModifiedRects / SpriteChanges 中对象是否仍处于"应用后"状态
            if (!DraftSpriteApplyService.ValidateSpriteRevertState(prefabRoot, record))
                return false;

            foreach (var rectRec in record.ModifiedRects)
            {
                if (string.IsNullOrEmpty(rectRec.ObjectPath))
                    continue;

                Transform t = prefabRoot.transform.Find(rectRec.ObjectPath);
                if (t == null)
                    return false;

                var rect = t as RectTransform;
                if (rect == null)
                    return false;

                // 如果当前值已偏离"应用后"的值，说明用户手动修改过，判定为冲突
                bool pivotDifferent = rectRec.HasPivot && !ApproximatelyEqual(rect.pivot, rectRec.NewPivot);

                if (!ApproximatelyEqual(rect.anchoredPosition, rectRec.NewAnchoredPosition) ||
                    !ApproximatelyEqual(rect.sizeDelta, rectRec.NewSizeDelta) ||
                    !ApproximatelyEqual(rect.anchorMin, rectRec.NewAnchorMin) ||
                    !ApproximatelyEqual(rect.anchorMax, rectRec.NewAnchorMax) ||
                    pivotDifferent)
                {
                    Debug.LogWarning($"[PrefabDraftBuilder] 回滚冲突: '{rectRec.ObjectPath}' 已被手动修改。");
                    return false;
                }
            }

            // 阶段二：验证 CreatedObjectPaths 中的对象仍然存在
            foreach (var path in record.CreatedObjectPaths)
            {
                if (string.IsNullOrEmpty(path))
                    continue;

                if (prefabRoot.transform.Find(path) == null)
                {
                    Debug.LogWarning($"[PrefabDraftBuilder] 回滚冲突: 创建的对象 '{path}' 已不存在。");
                    return false;
                }
            }

            // 阶段三：执行回滚 — 恢复 RectTransform
            foreach (var rectRec in record.ModifiedRects)
            {
                if (string.IsNullOrEmpty(rectRec.ObjectPath))
                    continue;

                Transform t = prefabRoot.transform.Find(rectRec.ObjectPath);
                if (t == null)
                    continue;

                var rect = t as RectTransform;
                if (rect == null)
                    continue;

                rect.anchorMin = rectRec.OldAnchorMin;
                rect.anchorMax = rectRec.OldAnchorMax;
                if (rectRec.HasPivot)
                {
                    rect.pivot = rectRec.OldPivot;
                }
                rect.anchoredPosition = rectRec.OldAnchoredPosition;
                rect.sizeDelta = rectRec.OldSizeDelta;
            }

            // 阶段四：恢复 Sprite 引用
            DraftSpriteApplyService.RestoreSprites(prefabRoot, record);

            // 阶段五：删除创建的对象（逆序删除避免层级路径失效）
            for (int i = record.CreatedObjectPaths.Count - 1; i >= 0; i--)
            {
                string path = record.CreatedObjectPaths[i];
                if (string.IsNullOrEmpty(path))
                    continue;

                Transform t = prefabRoot.transform.Find(path);
                if (t != null)
                {
                    UnityEngine.Object.DestroyImmediate(t.gameObject);
                }
            }

            return true;
        }

        // ─────────────────────────── 私有辅助方法 ───────────────────────────

        /// <summary>
        /// 根据 ParentPath + Name 在 prefabRoot 下查找子节点。
        /// </summary>
        private static Transform FindChildByPath(Transform root, string parentPath, string name)
        {
            string fullPath = string.IsNullOrEmpty(parentPath)
                ? name
                : parentPath + "/" + name;

            return root.Find(fullPath);
        }

        /// <summary>
        /// 根据布局组信息为 GameObject 添加 HorizontalLayoutGroup 或 VerticalLayoutGroup。
        /// </summary>
        private static void ApplyLayoutGroup(GameObject go, LayoutGroupInfo layoutInfo)
        {
            if (layoutInfo == null || layoutInfo.GroupType == LayoutGroupType.None)
                return;

            if (!layoutInfo.UseUnityLayoutGroup)
            {
                RemoveLayoutGroups(go);
                return;
            }

            HorizontalOrVerticalLayoutGroup layoutGroup;

            if (layoutInfo.GroupType == LayoutGroupType.Horizontal)
            {
                var vertical = go.GetComponent<VerticalLayoutGroup>();
                if (vertical != null) UnityEngine.Object.DestroyImmediate(vertical);
                layoutGroup = go.GetComponent<HorizontalLayoutGroup>() ?? go.AddComponent<HorizontalLayoutGroup>();
            }
            else
            {
                var horizontal = go.GetComponent<HorizontalLayoutGroup>();
                if (horizontal != null) UnityEngine.Object.DestroyImmediate(horizontal);
                layoutGroup = go.GetComponent<VerticalLayoutGroup>() ?? go.AddComponent<VerticalLayoutGroup>();
            }

            // 解析 Spacing：如果是 "even" 则使用 0 并启用 controlChildSize
            float spacingValue;
            bool isEven = string.Equals(layoutInfo.Spacing, "even", StringComparison.OrdinalIgnoreCase);

            if (isEven)
            {
                spacingValue = 0f;
                layoutGroup.childControlWidth = true;
                layoutGroup.childControlHeight = true;
            }
            else
            {
                float.TryParse(layoutInfo.Spacing, out spacingValue);
            }

            layoutGroup.spacing = spacingValue;

            // 设置内边距
            layoutGroup.padding = new RectOffset(
                (int)layoutInfo.Padding.x,
                (int)layoutInfo.Padding.x,
                (int)layoutInfo.Padding.y,
                (int)layoutInfo.Padding.y
            );

            layoutGroup.childControlWidth = isEven;
            layoutGroup.childControlHeight = isEven;

            // 设置子元素对齐方式
            layoutGroup.childAlignment = ParseChildAlignment(layoutInfo.ChildAlignment);
        }

        /// <summary>
        /// 移除已有的 HorizontalLayoutGroup / VerticalLayoutGroup，避免旧布局组件接管显式坐标子节点。
        /// </summary>
        /// <param name="go">目标 GameObject。</param>
        private static void RemoveLayoutGroups(GameObject go)
        {
            var horizontal = go.GetComponent<HorizontalLayoutGroup>();
            if (horizontal != null)
            {
                UnityEngine.Object.DestroyImmediate(horizontal);
            }

            var vertical = go.GetComponent<VerticalLayoutGroup>();
            if (vertical != null)
            {
                UnityEngine.Object.DestroyImmediate(vertical);
            }
        }

        /// <summary>
        /// 将字符串对齐方式转换为 TextAnchor 枚举值。
        /// </summary>
        private static TextAnchor ParseChildAlignment(string alignment)
        {
            if (string.IsNullOrEmpty(alignment))
                return TextAnchor.UpperLeft;

            switch (alignment.ToLowerInvariant())
            {
                case "center":
                    return TextAnchor.UpperCenter;
                case "end":
                    return TextAnchor.UpperRight;
                case "middle":
                    return TextAnchor.MiddleCenter;
                case "start":
                default:
                    return TextAnchor.UpperLeft;
            }
        }

        /// <summary>
        /// 检测现有节点的 RectTransform 是否与描述符存在冲突。
        /// <para>Unity 默认 RectTransform（中心锚点 + 100x100）视为旧生成残留，允许本次 JSON 覆盖。</para>
        /// </summary>
        private static bool DetectRectConflict(Transform existing, UguiNodeDescriptor desc)
        {
            var rect = existing as RectTransform;
            if (rect == null)
                return false;

            if (IsDefaultRectTransformState(rect))
                return false;

            // 如果四项属性全部不同，说明用户已手动调整过布局，标记冲突
            bool posDifferent = !ApproximatelyEqual(rect.anchoredPosition, desc.AnchoredPosition);
            bool sizeDifferent = !ApproximatelyEqual(rect.sizeDelta, desc.SizeDelta);
            bool anchorMinDifferent = !ApproximatelyEqual(rect.anchorMin, desc.AnchorMin);
            bool anchorMaxDifferent = !ApproximatelyEqual(rect.anchorMax, desc.AnchorMax);

            // 全部不同才视为冲突；部分不同说明可能是之前应用遗留，允许覆盖
            return posDifferent && sizeDifferent && anchorMinDifferent && anchorMaxDifferent;
        }

        /// <summary>
        /// 判断 RectTransform 是否仍处于 Unity 新建 UI 节点的默认布局状态。
        /// </summary>
        /// <param name="rect">目标 RectTransform。</param>
        /// <returns>处于中心锚点、零偏移、100x100 默认尺寸时返回 true。</returns>
        private static bool IsDefaultRectTransformState(RectTransform rect)
        {
            return ApproximatelyEqual(rect.anchorMin, new Vector2(0.5f, 0.5f))
                   && ApproximatelyEqual(rect.anchorMax, new Vector2(0.5f, 0.5f))
                   && ApproximatelyEqual(rect.anchoredPosition, Vector2.zero)
                   && ApproximatelyEqual(rect.sizeDelta, new Vector2(100f, 100f));
        }

        /// <summary>
        /// 获取描述符对应的相对 hierarchy 路径。
        /// </summary>
        internal static string GetDescriptorPath(UguiNodeDescriptor desc)
        {
            if (desc == null || string.IsNullOrEmpty(desc.Name))
                return string.Empty;

            return string.IsNullOrEmpty(desc.ParentPath)
                ? desc.Name
                : desc.ParentPath + "/" + desc.Name;
        }

        /// <summary>
        /// 获取描述符当前对应的 prefabRoot 相对 hierarchy 路径。
        /// </summary>
        internal static string GetResolvedDescriptorPath(GameObject prefabRoot, UguiNodeDescriptor desc)
        {
            if (prefabRoot == null || desc == null || string.IsNullOrEmpty(desc.Name))
                return string.Empty;

            Transform existing = FindChildByPath(prefabRoot.transform, desc.ParentPath, desc.Name);
            return existing == null
                ? string.Empty
                : GetHierarchyPath(prefabRoot.transform, existing);
        }

        /// <summary>
        /// 获取 Transform 相对于指定根节点的 hierarchy 路径。
        /// </summary>
        internal static string GetHierarchyPath(Transform root, Transform t)
        {
            if (root == null || t == null)
                return string.Empty;

            var parts = new List<string>();
            Transform current = t;
            while (current != null && current != root)
            {
                parts.Add(current.name);
                current = current.parent;
            }

            if (current != root)
                return string.Empty;

            parts.Reverse();
            return string.Join("/", parts);
        }

        /// <summary>
        /// 获取 Transform 相对于其所在层级根节点的 hierarchy 路径。
        /// </summary>
        internal static string GetHierarchyPath(Transform t)
        {
            var parts = new List<string>();
            Transform current = t;
            while (current != null)
            {
                parts.Add(current.name);
                current = current.parent;
            }

            parts.Reverse();
            // 移除根节点自身，只返回子节点路径
            if (parts.Count > 1)
                parts.RemoveAt(0);

            return string.Join("/", parts);
        }

        /// <summary>
        /// 浮点向量近似比较，阈值 0.001f。
        /// </summary>
        private static bool ApproximatelyEqual(Vector2 a, Vector2 b)
        {
            const float epsilon = 0.001f;
            return Mathf.Abs(a.x - b.x) < epsilon && Mathf.Abs(a.y - b.y) < epsilon;
        }
    }
}
#endif
