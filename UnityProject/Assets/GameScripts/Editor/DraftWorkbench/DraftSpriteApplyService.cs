#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RogueCard.Editor.DraftWorkbench
{
    /// <summary>
    /// 将已确认的 Sprite assignment 应用到 prefab 实例，并生成可回滚记录。
    /// </summary>
    public static class DraftSpriteApplyService
    {
        /// <summary>
        /// 将已批准的 assignment 直接应用到已有 prefab Image 节点。
        /// </summary>
        /// <param name="prefabRoot">可编辑 prefab 根节点。</param>
        /// <param name="assignments">Sprite 应用计划。</param>
        /// <returns>实际写入的 Sprite 变更记录。</returns>
        public static List<SpriteChangeRecord> ApplyAssignments(
            GameObject prefabRoot,
            IReadOnlyList<DraftSpriteAssignment> assignments)
        {
            if (prefabRoot == null) throw new ArgumentNullException(nameof(prefabRoot));
            if (assignments == null) throw new ArgumentNullException(nameof(assignments));

            var records = new List<SpriteChangeRecord>();
            foreach (var assignment in assignments.Where(a => a != null && a.Approved))
            {
                if (string.IsNullOrEmpty(assignment.NodePath) || string.IsNullOrEmpty(assignment.SpriteAssetPath))
                    continue;

                Transform target = prefabRoot.transform.Find(assignment.NodePath);
                Image image = target != null ? target.GetComponent<Image>() : null;
                if (image == null)
                {
                    Debug.LogWarning($"[DraftSpriteApplyService] 找不到可应用 Sprite 的 Image 节点: {assignment.NodePath}");
                    continue;
                }

                Sprite newSprite = AssetDatabase.LoadAssetAtPath<Sprite>(assignment.SpriteAssetPath);
                if (newSprite == null && !string.IsNullOrEmpty(assignment.SpriteGuid))
                {
                    string path = AssetDatabase.GUIDToAssetPath(assignment.SpriteGuid);
                    if (!string.IsNullOrEmpty(path))
                    {
                        newSprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    }
                }

                if (newSprite == null)
                {
                    Debug.LogWarning($"[DraftSpriteApplyService] 找不到 Sprite 资源: {assignment.SpriteAssetPath}");
                    continue;
                }

                Sprite oldSprite = image.sprite;
                string oldPath = oldSprite != null ? AssetDatabase.GetAssetPath(oldSprite) : string.Empty;
                string oldGuid = !string.IsNullOrEmpty(oldPath) ? AssetDatabase.AssetPathToGUID(oldPath) : string.Empty;
                string newPath = AssetDatabase.GetAssetPath(newSprite);
                string newGuid = !string.IsNullOrEmpty(newPath) ? AssetDatabase.AssetPathToGUID(newPath) : assignment.SpriteGuid;

                image.sprite = newSprite;
                records.Add(new SpriteChangeRecord
                {
                    ObjectPath = assignment.NodePath,
                    OldSpriteAssetPath = oldPath,
                    OldSpriteGuid = oldGuid,
                    NewSpriteAssetPath = newPath,
                    NewSpriteGuid = newGuid,
                    RegionId = assignment.RegionId,
                    Marker = assignment.Marker
                });
            }

            return records;
        }

        /// <summary>
        /// 在常规 PrefabDraftBuilder.ApplyChanges 前记录即将发生的 Sprite 变更。
        /// </summary>
        /// <param name="prefabRoot">可编辑 prefab 根节点。</param>
        /// <param name="previewChanges">当前预览变更。</param>
        /// <returns>Sprite 变更记录。</returns>
        public static List<SpriteChangeRecord> BuildSpriteChangeRecords(
            GameObject prefabRoot,
            IReadOnlyList<UguiNodeChange> previewChanges)
        {
            if (prefabRoot == null) throw new ArgumentNullException(nameof(prefabRoot));
            if (previewChanges == null) throw new ArgumentNullException(nameof(previewChanges));

            var records = new List<SpriteChangeRecord>();
            foreach (var change in previewChanges)
            {
                var descriptor = change?.Descriptor;
                var visuals = descriptor?.Visuals;
                if (descriptor == null || visuals == null || change.HasConflict)
                    continue;

                Sprite newSprite = DraftSpriteResolver.Resolve(visuals, out string resolvedPath);
                if (newSprite == null)
                    continue;

                string objectPath = change.IsNew
                    ? PrefabDraftBuilder.GetDescriptorPath(descriptor)
                    : PrefabDraftBuilder.GetResolvedDescriptorPath(prefabRoot, descriptor);
                if (string.IsNullOrEmpty(objectPath))
                    objectPath = PrefabDraftBuilder.GetDescriptorPath(descriptor);

                Sprite oldSprite = null;
                if (!change.IsNew)
                {
                    var target = prefabRoot.transform.Find(objectPath);
                    var image = target != null ? target.GetComponent<Image>() : null;
                    oldSprite = image != null ? image.sprite : null;
                }

                string oldPath = oldSprite != null ? AssetDatabase.GetAssetPath(oldSprite) : string.Empty;
                string oldGuid = !string.IsNullOrEmpty(oldPath) ? AssetDatabase.AssetPathToGUID(oldPath) : string.Empty;
                string newPath = !string.IsNullOrEmpty(resolvedPath) ? resolvedPath : AssetDatabase.GetAssetPath(newSprite);
                string newGuid = !string.IsNullOrEmpty(newPath) ? AssetDatabase.AssetPathToGUID(newPath) : visuals.SpriteGuid;
                records.Add(new SpriteChangeRecord
                {
                    ObjectPath = objectPath,
                    OldSpriteAssetPath = oldPath,
                    OldSpriteGuid = oldGuid,
                    NewSpriteAssetPath = newPath,
                    NewSpriteGuid = newGuid,
                    RegionId = visuals.RegionId,
                    Marker = visuals.AssetMarker
                });
            }

            return records;
        }

        /// <summary>
        /// 检查当前 prefab 是否仍处于应用后的 Sprite 状态。
        /// </summary>
        /// <param name="prefabRoot">可编辑 prefab 根节点。</param>
        /// <param name="record">应用记录。</param>
        /// <returns>一致时返回 true。</returns>
        public static bool ValidateSpriteRevertState(GameObject prefabRoot, DraftApplyRecord record)
        {
            if (prefabRoot == null || record?.SpriteChanges == null)
                return true;

            foreach (var spriteChange in record.SpriteChanges)
            {
                if (spriteChange == null || string.IsNullOrEmpty(spriteChange.ObjectPath))
                    continue;

                var target = prefabRoot.transform.Find(spriteChange.ObjectPath);
                var image = target != null ? target.GetComponent<Image>() : null;
                if (image == null)
                {
                    Debug.LogWarning($"[DraftSpriteApplyService] 回滚冲突: 找不到 Image 节点 '{spriteChange.ObjectPath}'。 ");
                    return false;
                }

                string currentPath = image.sprite != null ? AssetDatabase.GetAssetPath(image.sprite) : string.Empty;
                string currentGuid = !string.IsNullOrEmpty(currentPath) ? AssetDatabase.AssetPathToGUID(currentPath) : string.Empty;
                if (!StringEquals(currentGuid, spriteChange.NewSpriteGuid) && !StringEquals(currentPath, spriteChange.NewSpriteAssetPath))
                {
                    Debug.LogWarning($"[DraftSpriteApplyService] 回滚冲突: '{spriteChange.ObjectPath}' 的 Sprite 已被手动修改。 ");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 根据应用记录恢复旧 Sprite。
        /// </summary>
        /// <param name="prefabRoot">可编辑 prefab 根节点。</param>
        /// <param name="record">应用记录。</param>
        public static void RestoreSprites(GameObject prefabRoot, DraftApplyRecord record)
        {
            if (prefabRoot == null || record?.SpriteChanges == null)
                return;

            foreach (var spriteChange in record.SpriteChanges)
            {
                if (spriteChange == null || string.IsNullOrEmpty(spriteChange.ObjectPath))
                    continue;

                var target = prefabRoot.transform.Find(spriteChange.ObjectPath);
                var image = target != null ? target.GetComponent<Image>() : null;
                if (image == null)
                    continue;

                Sprite oldSprite = null;
                if (!string.IsNullOrEmpty(spriteChange.OldSpriteAssetPath))
                {
                    oldSprite = AssetDatabase.LoadAssetAtPath<Sprite>(spriteChange.OldSpriteAssetPath);
                }

                if (oldSprite == null && !string.IsNullOrEmpty(spriteChange.OldSpriteGuid))
                {
                    string oldPath = AssetDatabase.GUIDToAssetPath(spriteChange.OldSpriteGuid);
                    if (!string.IsNullOrEmpty(oldPath))
                        oldSprite = AssetDatabase.LoadAssetAtPath<Sprite>(oldPath);
                }

                image.sprite = oldSprite;
            }
        }

        /// <summary>
        /// 判断两个字符串是否一致。
        /// </summary>
        private static bool StringEquals(string left, string right)
        {
            return string.Equals(left ?? string.Empty, right ?? string.Empty, StringComparison.Ordinal);
        }
    }
}
#endif
