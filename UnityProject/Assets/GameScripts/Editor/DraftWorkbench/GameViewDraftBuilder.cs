#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
    /// GameView 草稿构建器，负责将 <see cref="UguiNodeDescriptor"/> 列表预览/应用/回滚到 Prefab 根节点。
    /// <para>不引入任何运行时依赖，仅在编辑器环境下使用。</para>
    /// </summary>
    public static class GameViewDraftBuilder
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
        /// <returns>实际创建或修改的对象层级路径列表。</returns>
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
                        Debug.LogWarning($"[GameViewDraftBuilder] 找不到父节点路径 '{desc.ParentPath}'，跳过创建 '{desc.Name}'。");
                        continue;
                    }

                    var go = new GameObject(desc.Name);
                    go.transform.SetParent(parent, false);

                    var rect = go.AddComponent<RectTransform>();
                    ApplyRectValues(rect, desc);

                    AddComponents(go, desc.ComponentTypeNames);

                    appliedPaths.Add(GetHierarchyPath(go.transform));
                }
                else
                {
                    Transform existing = FindChildByPath(prefabRoot.transform, desc.ParentPath, desc.Name);
                    if (existing == null)
                    {
                        Debug.LogWarning($"[GameViewDraftBuilder] 预期存在的节点 '{desc.Name}' 未找到，跳过更新。");
                        continue;
                    }

                    var rect = existing as RectTransform ?? existing.gameObject.AddComponent<RectTransform>();
                    ApplyRectValues(rect, desc);

                    appliedPaths.Add(GetHierarchyPath(existing));
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

            // 阶段一：验证一致性 — 检查 ModifiedRects 中对象是否仍处于"应用后"状态
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
                if (!ApproximatelyEqual(rect.anchoredPosition, rectRec.NewAnchoredPosition) ||
                    !ApproximatelyEqual(rect.sizeDelta, rectRec.NewSizeDelta) ||
                    !ApproximatelyEqual(rect.anchorMin, rectRec.NewAnchorMin) ||
                    !ApproximatelyEqual(rect.anchorMax, rectRec.NewAnchorMax))
                {
                    Debug.LogWarning($"[GameViewDraftBuilder] 回滚冲突: '{rectRec.ObjectPath}' 已被手动修改。");
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
                    Debug.LogWarning($"[GameViewDraftBuilder] 回滚冲突: 创建的对象 '{path}' 已不存在。");
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
                rect.anchoredPosition = rectRec.OldAnchoredPosition;
                rect.sizeDelta = rectRec.OldSizeDelta;
            }

            // 阶段四：删除创建的对象（逆序删除避免层级路径失效）
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
        /// 将描述符中的布局参数写入 RectTransform。
        /// </summary>
        private static void ApplyRectValues(RectTransform rect, UguiNodeDescriptor desc)
        {
            rect.anchorMin = desc.AnchorMin;
            rect.anchorMax = desc.AnchorMax;
            rect.anchoredPosition = desc.AnchoredPosition;
            rect.sizeDelta = desc.SizeDelta;
        }

        /// <summary>
        /// 检测现有节点的 RectTransform 是否与描述符存在冲突。
        /// <para>当现有值与描述符定义的值均不相同时，判定为用户手动修改产生了冲突。</para>
        /// </summary>
        private static bool DetectRectConflict(Transform existing, UguiNodeDescriptor desc)
        {
            var rect = existing as RectTransform;
            if (rect == null)
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
        /// 根据类型全名为 GameObject 添加组件。
        /// </summary>
        private static void AddComponents(GameObject go, List<string> componentTypeNames)
        {
            if (componentTypeNames == null)
                return;

            foreach (var typeName in componentTypeNames)
            {
                if (string.IsNullOrEmpty(typeName))
                    continue;

                Type compType = FindType(typeName);
                if (compType == null)
                {
                    Debug.LogWarning($"[GameViewDraftBuilder] 找不到组件类型 '{typeName}'，跳过。");
                    continue;
                }

                if (go.GetComponent(compType) == null)
                {
                    go.AddComponent(compType);
                }
            }
        }

        /// <summary>
        /// 通过全名查找类型，依次在 Unity 主程序集和所有已加载程序集中搜索。
        /// </summary>
        private static Type FindType(string fullName)
        {
            // 优先从主程序集查找（UnityEngine 核心类型在此）
            var type = Type.GetType(fullName);
            if (type != null)
                return type;

            // 遍历所有已加载程序集
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = asm.GetType(fullName);
                if (type != null)
                    return type;
            }

            return null;
        }

        /// <summary>
        /// 获取 Transform 相对于根节点的 hierarchy 路径。
        /// </summary>
        private static string GetHierarchyPath(Transform t)
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
