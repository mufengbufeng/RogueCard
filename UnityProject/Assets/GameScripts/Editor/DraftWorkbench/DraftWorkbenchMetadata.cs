#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RogueCard.Editor.DraftWorkbench
{
    /// <summary>
    /// 草稿工作台元数据，持久化保存与目标 prefab 关联的源图信息、叠加层配置及 apply 历史。
    /// </summary>
    [CreateAssetMenu(fileName = "DraftWorkbenchMetadata", menuName = "Tools/RogueCard/Draft Workbench Metadata")]
    public class DraftWorkbenchMetadata : ScriptableObject
    {
        /// <summary>
        /// 源图片资源的 GUID。
        /// </summary>
        public string SourceImageAssetGuid;

        /// <summary>
        /// 目标 prefab 资源的 GUID。
        /// </summary>
        public string TargetPrefabAssetGuid;

        /// <summary>
        /// 源图尺寸（像素）。
        /// </summary>
        public Vector2 SourceImageSize;

        /// <summary>
        /// 叠加层显示配置。
        /// </summary>
        public DraftOverlaySettings OverlaySettings = new DraftOverlaySettings();

        /// <summary>
        /// 历史 apply 操作记录列表。
        /// </summary>
        public List<DraftApplyRecord> ApplyRecords = new List<DraftApplyRecord>();

        /// <summary>
        /// 最近一次绑定操作的结果报告。
        /// </summary>
        public DraftBindingReport LastBindingReport;

        /// <summary>
        /// 查找与 prefab 同目录的元数据 .asset 文件；若不存在则创建新的并返回。
        /// </summary>
        /// <param name="prefabAssetPath">目标 prefab 在项目中的资源路径（Assets/ 开头）。</param>
        /// <returns>加载或新建的 <see cref="DraftWorkbenchMetadata"/> 实例。</returns>
        public static DraftWorkbenchMetadata CreateOrLoad(string prefabAssetPath)
        {
            var metaPath = GetMetadataPathForPrefab(prefabAssetPath);
            var existing = AssetDatabase.LoadAssetAtPath<DraftWorkbenchMetadata>(metaPath);
            if (existing != null)
            {
                return existing;
            }

            var metadata = CreateInstance<DraftWorkbenchMetadata>();
            metadata.TargetPrefabAssetGuid = AssetDatabase.AssetPathToGUID(prefabAssetPath);
            AssetDatabase.CreateAsset(metadata, metaPath);
            AssetDatabase.SaveAssets();
            return metadata;
        }

        /// <summary>
        /// 返回与目标 prefab 同名但扩展名为 .draft.meta.asset 的资源路径。
        /// </summary>
        /// <returns>元数据资源路径。</returns>
        public string GetMetadataAssetPath()
        {
            if (string.IsNullOrEmpty(TargetPrefabAssetGuid))
            {
                return null;
            }

            var prefabPath = AssetDatabase.GUIDToAssetPath(TargetPrefabAssetGuid);
            return GetMetadataPathForPrefab(prefabPath);
        }

        /// <summary>
        /// 返回最近一条 apply 记录；若无任何记录则返回 null。
        /// </summary>
        /// <returns>最近一条 apply 记录，或 null。</returns>
        public DraftApplyRecord GetLatestApplyRecord()
        {
            if (ApplyRecords == null || ApplyRecords.Count == 0)
            {
                return null;
            }

            return ApplyRecords[ApplyRecords.Count - 1];
        }

        /// <summary>
        /// 追加一条 apply 记录并标记资源为已修改。
        /// </summary>
        /// <param name="record">要追加的 apply 记录。</param>
        public void AddApplyRecord(DraftApplyRecord record)
        {
            if (ApplyRecords == null)
            {
                ApplyRecords = new List<DraftApplyRecord>();
            }

            ApplyRecords.Add(record);
            EditorUtility.SetDirty(this);
        }

        /// <summary>
        /// 移除最后一条 apply 记录并标记资源为已修改。
        /// </summary>
        public void ClearLatestApplyRecord()
        {
            if (ApplyRecords == null || ApplyRecords.Count == 0)
            {
                return;
            }

            ApplyRecords.RemoveAt(ApplyRecords.Count - 1);
            EditorUtility.SetDirty(this);
        }

        /// <summary>
        /// 根据 prefab 路径推导对应的元数据 .asset 文件路径。
        /// </summary>
        /// <param name="prefabAssetPath">目标 prefab 的资源路径。</param>
        /// <returns>元数据文件路径。</returns>
        private static string GetMetadataPathForPrefab(string prefabAssetPath)
        {
            var ext = System.IO.Path.GetExtension(prefabAssetPath);
            var withoutExt = prefabAssetPath.Substring(0, prefabAssetPath.Length - ext.Length);
            return withoutExt + ".draft.meta.asset";
        }
    }
}
#endif
