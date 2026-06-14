using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ImageToUI.PrefabBuilder
{
    internal sealed class SpriteResolver
    {
        private readonly string assetRoot;
        private readonly Dictionary<string, Sprite> byKey = new Dictionary<string, Sprite>();
        private readonly Dictionary<string, List<Sprite>> byPath = new Dictionary<string, List<Sprite>>();
        private readonly Dictionary<string, List<Sprite>> byBasename = new Dictionary<string, List<Sprite>>();

        public SpriteResolver(string assetRoot)
        {
            this.assetRoot = NormalizeAssetPath(assetRoot);
            BuildIndex();
        }

        public Sprite Resolve(string assetGuid, string spriteName, string asset, string elementPath, PrefabBuildReport report)
        {
            if (!string.IsNullOrEmpty(assetGuid))
            {
                var spriteFromGuid = ResolveByGuid(assetGuid, spriteName, asset, elementPath, report);
                if (spriteFromGuid != null)
                {
                    return spriteFromGuid;
                }
            }
            return ResolveByAsset(asset, spriteName, elementPath, report);
        }

        private Sprite ResolveByGuid(string assetGuid, string spriteName, string asset, string elementPath,
            PrefabBuildReport report)
        {
            var guid = assetGuid.Trim();
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(assetPath))
            {
                report.Error(elementPath, "assetGuid not found in Unity AssetDatabase: " + assetGuid);
                return null;
            }

            var sprites = LoadSpritesAtPath(assetPath);
            if (sprites.Count == 0)
            {
                report.Error(elementPath, "assetGuid resolved to a non-Sprite asset: " + assetGuid);
                return null;
            }
            return ResolveFromMatches(sprites, spriteName, assetGuid, elementPath, report, "assetGuid");
        }

        private Sprite ResolveByAsset(string asset, string spriteName, string elementPath, PrefabBuildReport report)
        {
            if (string.IsNullOrEmpty(asset))
            {
                return null;
            }

            if (IsUnityAssetPath(asset))
            {
                return ResolveByUnityAssetPath(asset, spriteName, elementPath, report);
            }

            var key = NormalizeKey(asset);
            List<Sprite> pathMatches;
            if (byPath.TryGetValue(key, out pathMatches))
            {
                return ResolveFromMatches(pathMatches, spriteName, asset, elementPath, report, "asset path");
            }

            var basename = NormalizeKey(Path.GetFileName(asset));
            List<Sprite> matches;
            if (byBasename.TryGetValue(basename, out matches))
            {
                return ResolveFromMatches(matches, spriteName, asset, elementPath, report, "asset basename");
            }

            var withoutExtension = NormalizeKey(Path.GetFileNameWithoutExtension(asset));
            if (byBasename.TryGetValue(withoutExtension, out matches))
            {
                return ResolveFromMatches(matches, spriteName, asset, elementPath, report, "asset basename");
            }

            report.Error(elementPath, "asset not found: " + asset);
            return null;
        }

        private Sprite ResolveByUnityAssetPath(string asset, string spriteName, string elementPath, PrefabBuildReport report)
        {
            var assetPath = NormalizeAssetPath(asset);
            var sprites = LoadSpritesAtPath(assetPath);
            if (sprites.Count == 0)
            {
                report.Error(elementPath, "asset path resolved to a non-Sprite asset: " + asset);
                return null;
            }
            return ResolveFromMatches(sprites, spriteName, asset, elementPath, report, "asset path");
        }

        private static Sprite ResolveFromMatches(
            List<Sprite> sprites,
            string spriteName,
            string source,
            string elementPath,
            PrefabBuildReport report,
            string sourceLabel)
        {
            if (sprites == null || sprites.Count == 0)
            {
                return null;
            }
            if (sprites.Count == 1)
            {
                if (!string.IsNullOrEmpty(spriteName) && !SpriteNameMatches(sprites[0], spriteName))
                {
                    report.Error(elementPath, "spriteName does not match the resolved sprite: " + spriteName);
                    return null;
                }
                return sprites[0];
            }

            if (!string.IsNullOrEmpty(spriteName))
            {
                var named = PickSpriteByName(sprites, spriteName);
                if (named != null)
                {
                    return named;
                }
                report.Error(elementPath, "spriteName does not match any sub-sprite on " + sourceLabel + ": " + spriteName);
                return null;
            }

            report.Error(elementPath, sourceLabel + " resolves to multiple sprites; add spriteName to select a sub-sprite: " + source);
            return null;
        }

        private static Sprite PickSpriteByName(List<Sprite> sprites, string spriteName)
        {
            if (sprites == null || string.IsNullOrEmpty(spriteName))
            {
                return null;
            }

            var target = NormalizeKey(spriteName);
            foreach (var sprite in sprites)
            {
                if (sprite != null && SpriteNameMatches(sprite, target))
                {
                    return sprite;
                }
            }
            return null;
        }

        private static bool SpriteNameMatches(Sprite sprite, string spriteName)
        {
            return sprite != null && string.Equals(NormalizeKey(sprite.name), NormalizeKey(spriteName), StringComparison.OrdinalIgnoreCase);
        }

        private void BuildIndex()
        {
            if (string.IsNullOrEmpty(assetRoot) || !AssetDatabase.IsValidFolder(assetRoot))
            {
                return;
            }

            var guids = AssetDatabase.FindAssets("t:Sprite", new[] { assetRoot });
            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var sprites = LoadSpritesAtPath(assetPath);
                foreach (var sprite in sprites)
                {
                    IndexSprite(assetPath, sprite);
                }
            }
        }

        private static List<Sprite> LoadSpritesAtPath(string assetPath)
        {
            var output = new List<Sprite>();
            var primary = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (primary != null)
            {
                output.Add(primary);
            }

            var all = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            foreach (var obj in all)
            {
                var sprite = obj as Sprite;
                if (sprite != null && !output.Contains(sprite))
                {
                    output.Add(sprite);
                }
            }
            return output;
        }

        private void IndexSprite(string assetPath, Sprite sprite)
        {
            var rootRelative = assetPath;
            if (!string.IsNullOrEmpty(assetRoot)
                && assetPath.StartsWith(assetRoot, StringComparison.OrdinalIgnoreCase))
            {
                rootRelative = assetPath.Substring(assetRoot.Length).TrimStart('/');
            }

            AddKey(rootRelative, sprite);
            AddKey(assetPath, sprite);
            AddKey(Path.GetFileName(assetPath), sprite);
            AddKey(Path.ChangeExtension(rootRelative, null), sprite);
            AddPath(rootRelative, sprite);
            AddPath(assetPath, sprite);
            AddPath(Path.ChangeExtension(rootRelative, null), sprite);
            AddBasename(Path.GetFileName(assetPath), sprite);
            AddBasename(Path.GetFileNameWithoutExtension(assetPath), sprite);
            AddBasename(sprite.name, sprite);
        }

        private void AddKey(string key, Sprite sprite)
        {
            if (string.IsNullOrEmpty(key) || sprite == null)
            {
                return;
            }
            key = NormalizeKey(key);
            if (!byKey.ContainsKey(key))
            {
                byKey.Add(key, sprite);
            }
        }

        private void AddBasename(string key, Sprite sprite)
        {
            if (string.IsNullOrEmpty(key) || sprite == null)
            {
                return;
            }
            key = NormalizeKey(key);
            List<Sprite> list;
            if (!byBasename.TryGetValue(key, out list))
            {
                list = new List<Sprite>();
                byBasename.Add(key, list);
            }
            if (!list.Contains(sprite))
            {
                list.Add(sprite);
            }
        }

        private void AddPath(string key, Sprite sprite)
        {
            if (string.IsNullOrEmpty(key) || sprite == null)
            {
                return;
            }
            key = NormalizeKey(key);
            List<Sprite> list;
            if (!byPath.TryGetValue(key, out list))
            {
                list = new List<Sprite>();
                byPath.Add(key, list);
            }
            if (!list.Contains(sprite))
            {
                list.Add(sprite);
            }
        }

        private static string NormalizeAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }
            return path.Replace("\\", "/").TrimEnd('/');
        }

        private static string NormalizeKey(string key)
        {
            return NormalizeAssetPath(key).ToLowerInvariant();
        }

        private static bool IsUnityAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }
            var normalized = NormalizeAssetPath(path);
            return normalized == "Assets"
                || normalized.StartsWith("Assets/", StringComparison.Ordinal)
                || normalized == "Packages"
                || normalized.StartsWith("Packages/", StringComparison.Ordinal);
        }
    }
}
