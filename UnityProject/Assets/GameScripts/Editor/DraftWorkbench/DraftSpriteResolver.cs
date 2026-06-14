#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RogueCard.Editor.DraftWorkbench
{
    /// <summary>
    /// Draft Workbench 内部 Sprite 解析器，支持 Image-To-UI 的 assetGuid、spriteName、asset 与 spriteRootFolder。
    /// </summary>
    internal static class DraftSpriteResolver
    {
        /// <summary>
        /// 根据视觉字段解析目标 Sprite。
        /// </summary>
        /// <param name="visuals">转换后的视觉字段。</param>
        /// <param name="resolvedAssetPath">解析到的 Unity 资源路径。</param>
        /// <returns>解析成功时返回 Sprite；否则返回 null。</returns>
        public static Sprite Resolve(UiNodeVisuals visuals, out string resolvedAssetPath)
        {
            resolvedAssetPath = string.Empty;
            if (visuals == null)
                return null;

            Sprite sprite = ResolveByGuid(visuals.SpriteGuid, visuals.SpriteName, out resolvedAssetPath);
            if (sprite != null)
                return sprite;

            sprite = ResolveByAssetPath(visuals.SpriteAssetPath, visuals.SpriteName, out resolvedAssetPath);
            if (sprite != null)
                return sprite;

            sprite = ResolveByAssetMarker(visuals.AssetMarker, visuals.SpriteName, visuals.SpriteRootFolder, out resolvedAssetPath);
            if (sprite != null)
                return sprite;

            return null;
        }

        /// <summary>
        /// 从指定资源路径加载 Sprite，并按 spriteName 选择子 Sprite。
        /// </summary>
        public static Sprite ResolveAtPath(string assetPath, string spriteName, out string resolvedAssetPath)
        {
            resolvedAssetPath = string.Empty;
            if (string.IsNullOrWhiteSpace(assetPath))
                return null;

            string normalized = NormalizeAssetPath(assetPath);
            if (!IsUnityAssetPath(normalized))
                return null;

            var sprites = LoadSpritesAtPath(normalized);
            Sprite sprite = PickSprite(sprites, spriteName);
            if (sprite != null)
            {
                resolvedAssetPath = normalized;
            }

            return sprite;
        }

        private static Sprite ResolveByGuid(string spriteGuid, string spriteName, out string resolvedAssetPath)
        {
            resolvedAssetPath = string.Empty;
            if (string.IsNullOrWhiteSpace(spriteGuid))
                return null;

            string path = AssetDatabase.GUIDToAssetPath(spriteGuid.Trim());
            return ResolveAtPath(path, spriteName, out resolvedAssetPath);
        }

        private static Sprite ResolveByAssetPath(string assetPath, string spriteName, out string resolvedAssetPath)
        {
            return ResolveAtPath(assetPath, spriteName, out resolvedAssetPath);
        }

        private static Sprite ResolveByAssetMarker(string assetMarker, string spriteName, string spriteRootFolder, out string resolvedAssetPath)
        {
            resolvedAssetPath = string.Empty;
            if (string.IsNullOrWhiteSpace(assetMarker))
                return null;

            string normalizedAsset = NormalizeAssetPath(assetMarker);
            if (IsUnityAssetPath(normalizedAsset))
                return ResolveAtPath(normalizedAsset, spriteName, out resolvedAssetPath);

            string root = NormalizeAssetPath(spriteRootFolder);
            if (string.IsNullOrEmpty(root) || !AssetDatabase.IsValidFolder(root))
                return null;

            string basename = Path.GetFileName(normalizedAsset);
            string basenameWithoutExtension = Path.GetFileNameWithoutExtension(normalizedAsset);
            foreach (string guid in AssetDatabase.FindAssets("t:Sprite", new[] { root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = Path.GetFileName(path);
                string fileStem = Path.GetFileNameWithoutExtension(path);
                if (!StringEquals(fileName, basename)
                    && !StringEquals(fileStem, basename)
                    && !StringEquals(fileStem, basenameWithoutExtension))
                {
                    continue;
                }

                Sprite sprite = ResolveAtPath(path, spriteName, out resolvedAssetPath);
                if (sprite != null)
                    return sprite;
            }

            return null;
        }

        private static List<Sprite> LoadSpritesAtPath(string assetPath)
        {
            var sprites = new List<Sprite>();
            if (string.IsNullOrEmpty(assetPath))
                return sprites;

            Sprite primary = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (primary != null)
            {
                sprites.Add(primary);
            }

            UnityEngine.Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            foreach (UnityEngine.Object obj in allAssets)
            {
                var sprite = obj as Sprite;
                if (sprite != null && !sprites.Contains(sprite))
                {
                    sprites.Add(sprite);
                }
            }

            return sprites;
        }

        private static Sprite PickSprite(IReadOnlyList<Sprite> sprites, string spriteName)
        {
            if (sprites == null || sprites.Count == 0)
                return null;

            if (!string.IsNullOrWhiteSpace(spriteName))
            {
                foreach (Sprite sprite in sprites)
                {
                    if (sprite != null && StringEquals(sprite.name, spriteName))
                        return sprite;
                }

                return null;
            }

            return sprites[0];
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : path.Replace("\\", "/").Trim().TrimEnd('/');
        }

        private static bool IsUnityAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            return string.Equals(path, "Assets", StringComparison.Ordinal)
                   || path.StartsWith("Assets/", StringComparison.Ordinal)
                   || string.Equals(path, "Packages", StringComparison.Ordinal)
                   || path.StartsWith("Packages/", StringComparison.Ordinal);
        }

        private static bool StringEquals(string left, string right)
        {
            return string.Equals(left ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
    }
}
#endif
