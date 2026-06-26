#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace RogueCard.Editor.DraftWorkbench
{
    /// <summary>
    /// 自动切图生成 PNG 的 Unity Sprite 导入服务。
    /// </summary>
    public static class DraftGeneratedSpriteImporter
    {
        /// <summary>
        /// 将指定 PNG 资产导入为 UI Sprite，并返回生成信息。
        /// </summary>
        /// <param name="assetPath">PNG 的 Assets 相对路径。</param>
        /// <param name="region">来源 region。</param>
        /// <param name="config">ComfyUI 配置。</param>
        /// <returns>生成的 Sprite 信息；导入失败时返回 null。</returns>
        public static DraftGeneratedSprite ImportSprite(
            string assetPath,
            DraftSegmentRegion region,
            ComfyUiServiceConfig config)
        {
            if (string.IsNullOrEmpty(assetPath)) throw new ArgumentException("资产路径为空", nameof(assetPath));
            if (region == null) throw new ArgumentNullException(nameof(region));
            if (config == null) throw new ArgumentNullException(nameof(config));

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[DraftGeneratedSpriteImporter] 无法获取 TextureImporter: {assetPath}");
                return null;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = config.SpritePixelsPerUnit;
            TrySetSpriteMeshType(importer, config.UseFullRectMesh);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null)
            {
                Debug.LogWarning($"[DraftGeneratedSpriteImporter] Sprite 导入失败: {assetPath}");
                return null;
            }

            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            return new DraftGeneratedSprite
            {
                RegionId = region.id,
                Marker = region.marker,
                Label = region.label,
                SpriteAssetPath = assetPath,
                SpriteGuid = guid,
                SourceBounds = region.bbox?.ToRect() ?? Rect.zero,
                ExpandedBounds = region.expandedBbox?.ToRect() ?? Rect.zero,
                Confidence = region.confidence,
                AssetKind = region.assetKind,
                AlphaMode = region.alphaMode,
                AlphaValidation = region.alphaValidation,
                GenerationStatus = DraftRegionGenerationStatus.Generated,
                GenerationMessage = "Sprite 导入成功。"
            };
        }

        /// <summary>
        /// 在当前 Unity 版本支持时设置 sprite mesh type；不支持时安全跳过。
        /// </summary>
        private static void TrySetSpriteMeshType(TextureImporter importer, bool useFullRectMesh)
        {
            var property = typeof(TextureImporter).GetProperty("spriteMeshType");
            if (property == null || !property.CanWrite)
                return;

            property.SetValue(importer, useFullRectMesh ? SpriteMeshType.FullRect : SpriteMeshType.Tight, null);
        }
    }
}
#endif
