using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ImageToUI.PrefabBuilder
{
    public sealed class PrefabBuildSettings
    {
        public string JsonPath;
        public string SpriteRootFolder;
        public string OutputPrefabPath;
    }

    public sealed class PrefabBuildReport
    {
        public int NodesCreated;
        public int ImagesCreated;
        public int TextsCreated;
        public int RectsCreated;
        public int ButtonsCreated;
        public string OutputPrefabPath;
        public string SpriteRootFolder;
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Errors = new List<string>();

        public bool HasErrors
        {
            get { return Errors.Count > 0; }
        }

        public void Warn(string path, string message)
        {
            Warnings.Add(path + ": " + message);
        }

        public void Error(string path, string message)
        {
            Errors.Add(path + ": " + message);
        }

        public string ToJson()
        {
            var obj = new JObject();
            obj["valid"] = Errors.Count == 0;
            obj["nodesCreated"] = NodesCreated;
            obj["imagesCreated"] = ImagesCreated;
            obj["textsCreated"] = TextsCreated;
            obj["rectsCreated"] = RectsCreated;
            obj["buttonsCreated"] = ButtonsCreated;
            obj["outputPrefabPath"] = OutputPrefabPath;
            obj["spriteRootFolder"] = SpriteRootFolder;
            obj["errorCount"] = Errors.Count;
            obj["warningCount"] = Warnings.Count;
            obj["errors"] = new JArray(Errors);
            obj["warnings"] = new JArray(Warnings);
            return obj.ToString();
        }
    }

    public static class ImageToUIPrefabBuilder
    {
        public static PrefabBuildReport GeneratePrefabFromJson(string jsonPath)
        {
            return GeneratePrefab(new PrefabBuildSettings
            {
                JsonPath = jsonPath
            });
        }

        public static PrefabBuildReport GeneratePrefab(PrefabBuildSettings settings)
        {
            var report = new PrefabBuildReport();
            if (settings == null)
            {
                report.Error("settings", "settings are required");
                return report;
            }
            if (string.IsNullOrEmpty(settings.JsonPath) || !File.Exists(settings.JsonPath))
            {
                report.Error("json", "ui_structure.json not found: " + settings.JsonPath);
                return report;
            }

            UiStructureDocument document;
            try
            {
                document = UiStructureDocument.FromJson(File.ReadAllText(settings.JsonPath));
            }
            catch (Exception ex)
            {
                report.Error("json", ex.Message);
                return report;
            }

            settings.OutputPrefabPath = ResolveOutputPrefabPath(settings, document, report);
            settings.SpriteRootFolder = ResolveSpriteRootFolder(settings, document);
            report.OutputPrefabPath = settings.OutputPrefabPath;
            report.SpriteRootFolder = settings.SpriteRootFolder;
            if (report.HasErrors)
            {
                return report;
            }

            var needsSpriteRoot = HasAssetNeedingRoot(document.Root);
            if (string.IsNullOrEmpty(settings.SpriteRootFolder) && needsSpriteRoot)
            {
                report.Warn("unity.spriteRootFolder", "spriteRootFolder is empty; assets without assetGuid or Unity asset paths will not resolve");
            }
            else if (!string.IsNullOrEmpty(settings.SpriteRootFolder)
                && !AssetDatabase.IsValidFolder(settings.SpriteRootFolder))
            {
                report.Warn("unity.spriteRootFolder", "spriteRootFolder is not a Unity asset folder: " + settings.SpriteRootFolder);
            }

            var spriteResolver = new SpriteResolver(settings.SpriteRootFolder);
            var rootRect = new UiRect(
                document.Root.AuthoredRect.X,
                document.Root.AuthoredRect.Y,
                document.Root.AuthoredRect.Width > 0 ? document.Root.AuthoredRect.Width : document.CanvasWidth,
                document.Root.AuthoredRect.Height > 0 ? document.Root.AuthoredRect.Height : document.CanvasHeight
            );

            GameObject prefabRoot;
            prefabRoot = CreateCanvasRoot(document);
            var child = CreateElement(
                document.Root,
                prefabRoot.transform,
                new UiRect(0f, 0f, rootRect.Width, rootRect.Height),
                rootRect,
                document.Root.Name,
                spriteResolver,
                report
            );
            child.name = SanitizeName(document.Root.Name);

            if (report.HasErrors)
            {
                UnityEngine.Object.DestroyImmediate(prefabRoot);
                return report;
            }

            if (!EnsureAssetFolder(settings.OutputPrefabPath, report))
            {
                UnityEngine.Object.DestroyImmediate(prefabRoot);
                return report;
            }

            bool success;
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, settings.OutputPrefabPath, out success);
            UnityEngine.Object.DestroyImmediate(prefabRoot);
            if (!success)
            {
                report.Error("output", "failed to save prefab: " + settings.OutputPrefabPath);
            }
            else
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            return report;
        }

        private static GameObject CreateCanvasRoot(UiStructureDocument document)
        {
            var canvasGo = new GameObject(SanitizeName(document.CanvasName + "_Canvas"));
            var rect = canvasGo.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.zero;

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(
                Mathf.Max(1, document.CanvasWidth),
                Mathf.Max(1, document.CanvasHeight)
            );

            canvasGo.AddComponent<GraphicRaycaster>();
            return canvasGo;
        }

        private static GameObject CreateElement(
            UiElement elem,
            Transform parent,
            UiRect rect,
            UiRect parentRect,
            string path,
            SpriteResolver spriteResolver,
            PrefabBuildReport report)
        {
            var go = new GameObject(SanitizeName(elem.Name));
            var rt = go.AddComponent<RectTransform>();
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }
            ApplyAnchoredRect(rt, elem, rect, parentRect);
            report.NodesCreated++;

            var type = (elem.Type ?? "container").ToLowerInvariant();
            if (type == "image")
            {
                ApplyImage(go, elem, path, spriteResolver, report);
            }
            else if (type == "rect")
            {
                ApplySolidImage(go, elem, path, report);
            }
            else if (type == "overlay")
            {
                if (HasSpriteReference(elem))
                {
                    ApplyImage(go, elem, path, spriteResolver, report);
                }
                else
                {
                    ApplySolidImage(go, elem, path, report);
                }
            }
            else if (type == "text")
            {
                ApplyText(go, elem, path, report);
            }
            else if (type == "button")
            {
                if (HasSpriteReference(elem))
                {
                    ApplyImage(go, elem, path, spriteResolver, report);
                }
                else if (!string.IsNullOrEmpty(elem.Color))
                {
                    ApplySolidImage(go, elem, path, report);
                }
                go.AddComponent<Button>();
                report.ButtonsCreated++;
            }

            var childRects = UiLayoutResolver.ResolveChildren(elem);
            for (var i = 0; i < elem.Children.Count; i++)
            {
                var child = elem.Children[i];
                var childRect = i < childRects.Count
                    ? childRects[i]
                    : new UiRect(0f, 0f, child.AuthoredRect.Width, child.AuthoredRect.Height);
                CreateElement(
                    child,
                    go.transform,
                    childRect,
                    rect,
                    path + "/" + child.Name,
                    spriteResolver,
                    report
                );
            }

            return go;
        }

        private static void ApplyAnchoredRect(RectTransform rt, UiElement elem, UiRect rect,
            UiRect parentRect)
        {
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;

            var anchorX = ResolveHorizontalAnchor(elem.Align);
            var anchorY = ResolveVerticalAnchor(elem.VAlign);
            var anchor = new Vector2(anchorX, anchorY);
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;

            var pivotXFromTopLeft = rect.X + anchorX * rect.Width;
            var pivotYFromTopLeft = rect.Y + (1f - anchorY) * rect.Height;
            var anchorXFromTopLeft = anchorX * parentRect.Width;
            var anchorYFromTopLeft = (1f - anchorY) * parentRect.Height;

            rt.anchoredPosition = new Vector2(
                pivotXFromTopLeft - anchorXFromTopLeft,
                -(pivotYFromTopLeft - anchorYFromTopLeft)
            );
            rt.sizeDelta = new Vector2(rect.Width, rect.Height);
        }

        private static float ResolveHorizontalAnchor(string align)
        {
            if (align == "center" || align == "middle")
            {
                return 0.5f;
            }
            if (align == "right" || align == "end")
            {
                return 1f;
            }
            return 0f;
        }

        private static float ResolveVerticalAnchor(string align)
        {
            if (align == "center" || align == "middle")
            {
                return 0.5f;
            }
            if (align == "bottom" || align == "end")
            {
                return 0f;
            }
            return 1f;
        }

        private static void ApplyImage(GameObject go, UiElement elem, string path,
            SpriteResolver spriteResolver, PrefabBuildReport report)
        {
            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            image.color = ParseColor(elem.Color, elem.Opacity, Color.white);

            var sprite = spriteResolver.Resolve(elem.AssetGuid, elem.SpriteName, elem.Asset, path, report);
            image.sprite = sprite;
            if (HasNineSlice(elem.NineSlice))
            {
                image.type = Image.Type.Sliced;
                if (sprite != null && sprite.border == Vector4.zero)
                {
                    report.Warn(path, "nineSlice requested, but Sprite border is zero");
                }
                if (IsExplicitNineSlice(elem.NineSlice))
                {
                    report.Warn(path, "explicit nineSlice margins ignored; Unity uses the Sprite's existing border");
                }
            }
            report.ImagesCreated++;
        }

        private static void ApplySolidImage(GameObject go, UiElement elem, string path,
            PrefabBuildReport report)
        {
            var image = go.AddComponent<Image>();
            image.sprite = null;
            image.raycastTarget = string.Equals(elem.Type, "overlay", StringComparison.OrdinalIgnoreCase);
            image.color = ParseColor(elem.Color, elem.Opacity, new Color(1f, 1f, 1f, 1f));
            report.RectsCreated++;
        }

        private static void ApplyText(GameObject go, UiElement elem, string path,
            PrefabBuildReport report)
        {
            var text = go.AddComponent<Text>();
            text.raycastTarget = false;
            text.text = elem.Text ?? string.Empty;
            var font = ResolveFont(elem, path, report);
            if (font != null)
            {
                text.font = font;
            }
            text.fontSize = Mathf.RoundToInt(elem.FontSize.HasValue ? elem.FontSize.Value : 20f);
            text.color = ParseColor(elem.Color, elem.Opacity, Color.black);
            text.alignment = MapTextAnchor(elem.Alignment, elem.TextVAlign);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            if (elem.LineHeight.HasValue && text.fontSize > 0)
            {
                text.lineSpacing = Mathf.Max(0.1f, elem.LineHeight.Value / text.fontSize);
            }
            if (!elem.FontSize.HasValue)
            {
                report.Warn(path, "text element has no fontSize; using 20");
            }

            var strokeWidth = elem.StrokeWidth.HasValue ? elem.StrokeWidth.Value : 0f;
            if (strokeWidth > 0f)
            {
                var outline = go.AddComponent<Outline>();
                outline.effectColor = ParseColor(elem.StrokeColor, elem.Opacity, Color.black);
                outline.effectDistance = new Vector2(strokeWidth, -strokeWidth);
                outline.useGraphicAlpha = true;
            }
            report.TextsCreated++;
        }

        private static Font ResolveFont(UiElement elem, string path, PrefabBuildReport report)
        {
            var requested = !string.IsNullOrEmpty(elem.FontFamily) ? elem.FontFamily : elem.Font;
            if (string.IsNullOrEmpty(requested))
            {
                return null;
            }

            requested = requested.Trim();
            if (string.IsNullOrEmpty(requested))
            {
                return null;
            }

            var direct = LoadFontByPathOrGuid(requested);
            if (direct != null)
            {
                return direct;
            }

            var matched = FindFontByName(requested);
            if (matched != null)
            {
                return matched;
            }

            report.Warn(path, "fontFamily not found in Unity Font assets: " + requested);
            return null;
        }

        private static Font LoadFontByPathOrGuid(string requested)
        {
            if (IsUnityGuid(requested))
            {
                var guidPath = AssetDatabase.GUIDToAssetPath(requested);
                if (!string.IsNullOrEmpty(guidPath))
                {
                    var guidFont = AssetDatabase.LoadAssetAtPath<Font>(guidPath);
                    if (guidFont != null)
                    {
                        return guidFont;
                    }
                }
            }

            var assetPath = requested.Replace("\\", "/");
            if (assetPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                assetPath.StartsWith("Packages/", StringComparison.Ordinal))
            {
                return AssetDatabase.LoadAssetAtPath<Font>(assetPath);
            }

            return null;
        }

        private static Font FindFontByName(string requested)
        {
            var guids = AssetDatabase.FindAssets("t:Font");
            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var font = AssetDatabase.LoadAssetAtPath<Font>(assetPath);
                if (font == null)
                {
                    continue;
                }

                if (FontNameMatches(font.name, requested) ||
                    FontNameMatches(Path.GetFileNameWithoutExtension(assetPath), requested))
                {
                    return font;
                }
            }

            return null;
        }

        private static bool FontNameMatches(string candidate, string requested)
        {
            return string.Equals(candidate, requested, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsUnityGuid(string value)
        {
            if (value.Length != 32)
            {
                return false;
            }

            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                var hex = (c >= '0' && c <= '9') ||
                          (c >= 'a' && c <= 'f') ||
                          (c >= 'A' && c <= 'F');
                if (!hex)
                {
                    return false;
                }
            }

            return true;
        }

        private static TextAnchor MapTextAnchor(string horizontal, string vertical)
        {
            horizontal = string.IsNullOrEmpty(horizontal) ? "left" : horizontal;
            vertical = string.IsNullOrEmpty(vertical) ? "middle" : vertical;

            var top = vertical == "top" || vertical == "start";
            var bottom = vertical == "bottom" || vertical == "end";
            var centerV = !top && !bottom;
            var left = horizontal == "left" || horizontal == "start";
            var right = horizontal == "right" || horizontal == "end";
            var centerH = !left && !right;

            if (top && left) return TextAnchor.UpperLeft;
            if (top && centerH) return TextAnchor.UpperCenter;
            if (top && right) return TextAnchor.UpperRight;
            if (centerV && left) return TextAnchor.MiddleLeft;
            if (centerV && centerH) return TextAnchor.MiddleCenter;
            if (centerV && right) return TextAnchor.MiddleRight;
            if (bottom && left) return TextAnchor.LowerLeft;
            if (bottom && centerH) return TextAnchor.LowerCenter;
            return TextAnchor.LowerRight;
        }

        private static Color ParseColor(string hex, float? opacity, Color fallback)
        {
            var color = fallback;
            if (!string.IsNullOrEmpty(hex))
            {
                Color parsed;
                if (ColorUtility.TryParseHtmlString(hex, out parsed))
                {
                    color = parsed;
                }
            }
            color.a *= opacity.HasValue ? Mathf.Clamp01(opacity.Value) : 1f;
            return color;
        }

        private static bool HasNineSlice(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return false;
            }
            if (token.Type == JTokenType.Boolean)
            {
                return token.Value<bool>();
            }
            if (token.Type == JTokenType.String)
            {
                var value = token.Value<string>();
                return !string.IsNullOrEmpty(value)
                    && !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
            }
            return true;
        }

        private static bool IsExplicitNineSlice(JToken token)
        {
            if (token == null)
            {
                return false;
            }
            return token.Type == JTokenType.Object
                || token.Type == JTokenType.Integer
                || token.Type == JTokenType.Float;
        }

        private static bool HasSpriteReference(UiElement elem)
        {
            return !string.IsNullOrEmpty(elem.AssetGuid)
                || !string.IsNullOrEmpty(elem.Asset);
        }

        private static bool HasAssetNeedingRoot(UiElement elem)
        {
            if (elem == null)
            {
                return false;
            }
            if (!string.IsNullOrEmpty(elem.Asset)
                && string.IsNullOrEmpty(elem.AssetGuid)
                && !IsUnityAssetPath(elem.Asset))
            {
                return true;
            }
            foreach (var child in elem.Children)
            {
                if (HasAssetNeedingRoot(child))
                {
                    return true;
                }
            }
            return false;
        }

        private static string ResolveOutputPrefabPath(PrefabBuildSettings settings,
            UiStructureDocument document, PrefabBuildReport report)
        {
            var output = FirstNonEmpty(settings.OutputPrefabPath, document.Unity.OutputPrefabPath);
            output = NormalizeProjectPath(output, false);
            if (string.IsNullOrEmpty(output))
            {
                output = GetDefaultOutputPrefabPath(document.CanvasName);
            }
            if (!output.StartsWith("Assets/", StringComparison.Ordinal))
            {
                report.Error("unity.outputPrefabPath", "prefab output path must be under Assets/: " + output);
            }
            if (!output.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                report.Error("unity.outputPrefabPath", "prefab output path must end with .prefab: " + output);
            }
            return output;
        }

        private static string ResolveSpriteRootFolder(PrefabBuildSettings settings,
            UiStructureDocument document)
        {
            return NormalizeProjectPath(FirstNonEmpty(
                settings.SpriteRootFolder,
                document.Unity.SpriteRootFolder
            ), true);
        }

        internal static string GetDefaultOutputPrefabPath(string canvasName)
        {
            var name = SanitizeName(string.IsNullOrWhiteSpace(canvasName) ? "ImageToUI" : canvasName);
            return "Assets/Image-To-UI/" + name + ".prefab";
        }

        private static string FirstNonEmpty(string first, string second)
        {
            return !string.IsNullOrWhiteSpace(first) ? first : second;
        }

        private static string NormalizeProjectPath(string path, bool trimTrailingSlash)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            var normalized = path.Replace("\\", "/").Trim();
            if (trimTrailingSlash)
            {
                normalized = normalized.TrimEnd('/');
            }
            if (IsUnityAssetPath(normalized))
            {
                return normalized;
            }

            try
            {
                if (Path.IsPathRooted(normalized))
                {
                    var full = Path.GetFullPath(normalized).Replace("\\", "/");
                    var projectRoot = Directory.GetParent(Application.dataPath).FullName
                        .Replace("\\", "/")
                        .TrimEnd('/');
                    if (full.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase))
                    {
                        var relative = full.Substring(projectRoot.Length + 1);
                        return trimTrailingSlash ? relative.TrimEnd('/') : relative;
                    }
                }
            }
            catch (Exception)
            {
                return normalized;
            }

            return normalized;
        }

        private static bool IsUnityAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }
            var normalized = path.Replace("\\", "/");
            return normalized == "Assets"
                || normalized.StartsWith("Assets/", StringComparison.Ordinal)
                || normalized == "Packages"
                || normalized.StartsWith("Packages/", StringComparison.Ordinal);
        }

        private static bool EnsureAssetFolder(string assetPath, PrefabBuildReport report)
        {
            var normalized = assetPath.Replace("\\", "/");
            var directory = Path.GetDirectoryName(normalized);
            if (string.IsNullOrEmpty(directory))
            {
                report.Error("output", "invalid output path: " + assetPath);
                return false;
            }
            directory = directory.Replace("\\", "/");
            if (directory != "Assets" && !directory.StartsWith("Assets/", StringComparison.Ordinal))
            {
                report.Error("output", "output folder must be under Assets/: " + assetPath);
                return false;
            }
            var fullPath = AssetPathToFullPath(directory);
            Directory.CreateDirectory(fullPath);
            AssetDatabase.Refresh();
            return true;
        }

        private static string AssetPathToFullPath(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, assetPath);
        }

        private static string SanitizeName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "Element";
            }
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name;
        }
    }
}
