using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ImageToUI.PrefabBuilder
{
    internal struct UiRect
    {
        public float X;
        public float Y;
        public float Width;
        public float Height;

        public UiRect(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public Vector2 Size
        {
            get { return new Vector2(Width, Height); }
        }
    }

    internal sealed class UiElement
    {
        public string Type = "container";
        public string Name = "element";
        public string Asset;
        public string AssetGuid;
        public string SpriteName;
        public string Text;
        public string Color;
        public string StrokeColor;
        public string FontFamily;
        public string Font;
        public string Alignment;
        public string TextVAlign;
        public string Align;
        public string VAlign;
        public float? FontSize;
        public float? LineHeight;
        public float? StrokeWidth;
        public float? Opacity;
        public UiRect AuthoredRect;
        public Vector2 Offset;
        public JObject Layout;
        public JToken NineSlice;
        public readonly List<UiElement> Children = new List<UiElement>();

        public static UiElement FromJson(JObject obj)
        {
            var elem = new UiElement();
            elem.Type = JsonUtil.GetString(obj, "type", "container");
            elem.Name = JsonUtil.GetString(obj, "name", elem.Type);
            elem.Asset = JsonUtil.GetString(obj, "asset", null);
            elem.AssetGuid = JsonUtil.GetString(obj, "assetGuid", null);
            elem.SpriteName = JsonUtil.GetString(obj, "spriteName", null);
            elem.Text = JsonUtil.GetString(obj, "text", null);
            elem.Color = JsonUtil.GetString(obj, "color", null);
            elem.StrokeColor = JsonUtil.GetString(obj, "strokeColor", null);
            elem.FontFamily = JsonUtil.GetString(obj, "fontFamily", null);
            elem.Font = JsonUtil.GetString(obj, "font", null);
            elem.Alignment = JsonUtil.GetString(obj, "alignment", "left");
            elem.TextVAlign = JsonUtil.GetString(obj, "textVAlign", "middle");
            elem.Align = JsonUtil.GetString(obj, "align", null);
            elem.VAlign = JsonUtil.GetString(obj, "vAlign", null);
            elem.FontSize = JsonUtil.GetFloat(obj, "fontSize");
            elem.LineHeight = JsonUtil.GetFloat(obj, "lineHeight");
            elem.StrokeWidth = JsonUtil.GetFloat(obj, "strokeWidth");
            elem.Opacity = JsonUtil.GetFloat(obj, "opacity");
            elem.AuthoredRect = new UiRect(
                JsonUtil.GetObjectFloat(obj, "position", "x", 0f),
                JsonUtil.GetObjectFloat(obj, "position", "y", 0f),
                JsonUtil.GetObjectFloat(obj, "size", "width", 0f),
                JsonUtil.GetObjectFloat(obj, "size", "height", 0f)
            );
            elem.Offset = new Vector2(
                JsonUtil.GetObjectFloat(obj, "offset", "x", 0f),
                JsonUtil.GetObjectFloat(obj, "offset", "y", 0f)
            );
            elem.Layout = obj["layout"] as JObject;
            elem.NineSlice = obj["nineSlice"];

            var children = obj["children"] as JArray;
            if (children != null)
            {
                foreach (var child in children)
                {
                    var childObj = child as JObject;
                    if (childObj != null)
                    {
                        elem.Children.Add(FromJson(childObj));
                    }
                }
            }
            return elem;
        }
    }

    internal sealed class UiStructureDocument
    {
        public int CanvasWidth;
        public int CanvasHeight;
        public string CanvasName;
        public UnityBuildOptions Unity;
        public UiElement Root;

        public static UiStructureDocument FromJson(string json)
        {
            var obj = JObject.Parse(json);
            var canvas = obj["canvas"] as JObject;
            var rootObj = obj["root"] as JObject;
            if (rootObj == null)
            {
                throw new InvalidOperationException("ui_structure.json has no root object.");
            }

            var doc = new UiStructureDocument();
            doc.CanvasWidth = Mathf.RoundToInt(JsonUtil.GetObjectFloat(obj, "canvas", "width", 0f));
            doc.CanvasHeight = Mathf.RoundToInt(JsonUtil.GetObjectFloat(obj, "canvas", "height", 0f));
            doc.CanvasName = canvas != null ? JsonUtil.GetString(canvas, "name", "ImageToUI") : "ImageToUI";
            doc.Unity = UnityBuildOptions.FromJson(obj["unity"] as JObject);
            doc.Root = UiElement.FromJson(rootObj);
            if (doc.Root.AuthoredRect.Width <= 0 && doc.CanvasWidth > 0)
            {
                doc.Root.AuthoredRect.Width = doc.CanvasWidth;
            }
            if (doc.Root.AuthoredRect.Height <= 0 && doc.CanvasHeight > 0)
            {
                doc.Root.AuthoredRect.Height = doc.CanvasHeight;
            }
            return doc;
        }
    }

    internal sealed class UnityBuildOptions
    {
        public int SchemaVersion = 1;
        public string OutputPrefabPath;
        public string SpriteRootFolder;

        public static UnityBuildOptions FromJson(JObject obj)
        {
            var options = new UnityBuildOptions();
            if (obj == null)
            {
                return options;
            }

            var version = JsonUtil.GetFloat(obj, "schemaVersion");
            if (version.HasValue)
            {
                options.SchemaVersion = Mathf.RoundToInt(version.Value);
            }
            options.OutputPrefabPath = JsonUtil.GetString(obj, "outputPrefabPath", null);
            options.SpriteRootFolder = JsonUtil.GetString(obj, "spriteRootFolder", null);
            return options;
        }
    }

    internal static class JsonUtil
    {
        public static string GetString(JObject obj, string key, string fallback)
        {
            if (obj == null)
            {
                return fallback;
            }
            var token = obj[key];
            if (token == null || token.Type == JTokenType.Null)
            {
                return fallback;
            }
            if (token.Type == JTokenType.String)
            {
                return token.Value<string>();
            }
            return token.ToString();
        }

        public static float? GetFloat(JObject obj, string key)
        {
            if (obj == null)
            {
                return null;
            }
            return TokenToFloat(obj[key]);
        }

        public static float GetObjectFloat(JObject obj, string objectKey, string key, float fallback)
        {
            if (obj == null)
            {
                return fallback;
            }
            var child = obj[objectKey] as JObject;
            if (child == null)
            {
                return fallback;
            }
            var value = TokenToFloat(child[key]);
            return value.HasValue ? value.Value : fallback;
        }

        public static float? TokenToFloat(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return null;
            }
            if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
            {
                return token.Value<float>();
            }
            float parsed;
            if (float.TryParse(token.ToString(), out parsed))
            {
                return parsed;
            }
            return null;
        }
    }

    internal static class UiLayoutResolver
    {
        public static List<UiRect> ResolveChildren(UiElement parent)
        {
            if (parent.Layout != null)
            {
                return ResolveLayout(parent, parent.Layout);
            }

            var rects = new List<UiRect>();
            foreach (var child in parent.Children)
            {
                var pos = ResolveAlignment(child, parent.AuthoredRect.Width, parent.AuthoredRect.Height);
                rects.Add(new UiRect(
                    pos.x,
                    pos.y,
                    child.AuthoredRect.Width,
                    child.AuthoredRect.Height
                ));
            }
            return rects;
        }

        private static Vector2 ResolveAlignment(UiElement elem, float parentWidth, float parentHeight)
        {
            var x = elem.AuthoredRect.X;
            var y = elem.AuthoredRect.Y;
            if (!string.IsNullOrEmpty(elem.Align))
            {
                x = AlignAxis(parentWidth, elem.AuthoredRect.Width, elem.Align, x);
            }
            if (!string.IsNullOrEmpty(elem.VAlign))
            {
                y = AlignAxis(parentHeight, elem.AuthoredRect.Height, elem.VAlign, y);
            }
            return new Vector2(x + elem.Offset.x, y + elem.Offset.y);
        }

        private static List<UiRect> ResolveLayout(UiElement parent, JObject layout)
        {
            var output = new List<UiRect>();
            var children = parent.Children;
            if (children.Count == 0)
            {
                return output;
            }

            var layoutType = JsonUtil.GetString(layout, "type", "row");
            var mainIsX = layoutType != "column";
            var padding = GetPadding(layout);
            var innerWidth = Mathf.Max(0f, parent.AuthoredRect.Width - 2f * padding.x);
            var innerHeight = Mathf.Max(0f, parent.AuthoredRect.Height - 2f * padding.y);
            var mainExtent = mainIsX ? innerWidth : innerHeight;
            var crossExtent = mainIsX ? innerHeight : innerWidth;

            var sizesMain = new List<float>();
            var sizesCross = new List<float>();
            var totalMain = 0f;
            foreach (var child in children)
            {
                var main = mainIsX ? child.AuthoredRect.Width : child.AuthoredRect.Height;
                var cross = mainIsX ? child.AuthoredRect.Height : child.AuthoredRect.Width;
                sizesMain.Add(main);
                sizesCross.Add(cross);
                totalMain += main;
            }

            var leftover = Mathf.Max(0f, mainExtent - totalMain);
            var spacingToken = layout["spacing"];
            var spacingString = spacingToken != null && spacingToken.Type == JTokenType.String
                ? spacingToken.Value<string>()
                : null;
            var spacingNumber = JsonUtil.TokenToFloat(spacingToken);
            var align = JsonUtil.GetString(layout, "align", "start");
            var crossAlign = JsonUtil.GetString(layout, "vAlign", "start");
            var count = children.Count;
            var gaps = new List<float>();
            var lead = 0f;

            if (spacingString == "even" || IsSpaceAlign(align))
            {
                if (count == 1)
                {
                    lead = AlignAxis(mainExtent, sizesMain[0], align, 0f);
                }
                else if (align == "space-between")
                {
                    lead = 0f;
                    FillGaps(gaps, count - 1, leftover / Mathf.Max(1, count - 1));
                }
                else if (align == "space-around")
                {
                    var half = leftover / Mathf.Max(1, 2 * count);
                    lead = half;
                    FillGaps(gaps, count - 1, (leftover - 2f * half) / Mathf.Max(1, count - 1));
                }
                else
                {
                    var gap = leftover / Mathf.Max(1, count + 1);
                    lead = gap;
                    FillGaps(gaps, count - 1, gap);
                }
            }
            else
            {
                var fixedSpacing = spacingNumber.HasValue ? spacingNumber.Value : 0f;
                var totalWithGaps = totalMain + fixedSpacing * Mathf.Max(0, count - 1);
                if (align == "center" || align == "middle")
                {
                    lead = Mathf.Max(0f, (mainExtent - totalWithGaps) * 0.5f);
                }
                else if (align == "end" || align == "right" || align == "bottom")
                {
                    lead = Mathf.Max(0f, mainExtent - totalWithGaps);
                }
                FillGaps(gaps, count - 1, fixedSpacing);
            }

            var cursor = lead;
            for (var i = 0; i < count; i++)
            {
                var child = children[i];
                var cross = AlignAxis(crossExtent, sizesCross[i], crossAlign, 0f);
                float x;
                float y;
                if (mainIsX)
                {
                    x = padding.x + cursor;
                    y = padding.y + cross;
                    if (!string.IsNullOrEmpty(child.VAlign))
                    {
                        y = padding.y + AlignAxis(crossExtent, sizesCross[i], child.VAlign, cross);
                    }
                }
                else
                {
                    x = padding.x + cross;
                    y = padding.y + cursor;
                    if (!string.IsNullOrEmpty(child.Align))
                    {
                        x = padding.x + AlignAxis(crossExtent, sizesCross[i], child.Align, cross);
                    }
                }

                output.Add(new UiRect(
                    x + child.Offset.x,
                    y + child.Offset.y,
                    child.AuthoredRect.Width,
                    child.AuthoredRect.Height
                ));
                cursor += sizesMain[i];
                if (i < gaps.Count)
                {
                    cursor += gaps[i];
                }
            }

            return output;
        }

        private static Vector2 GetPadding(JObject layout)
        {
            var padding = layout["padding"];
            var number = JsonUtil.TokenToFloat(padding);
            if (number.HasValue)
            {
                return new Vector2(number.Value, number.Value);
            }
            var paddingObj = padding as JObject;
            if (paddingObj == null)
            {
                return Vector2.zero;
            }
            return new Vector2(
                JsonUtil.GetObjectFloat(layout, "padding", "x", 0f),
                JsonUtil.GetObjectFloat(layout, "padding", "y", 0f)
            );
        }

        private static float AlignAxis(float parentExtent, float elemExtent,
            string align, float explicitValue)
        {
            if (align == "start" || align == "left" || align == "top")
            {
                return 0f;
            }
            if (align == "center" || align == "middle")
            {
                return (parentExtent - elemExtent) * 0.5f;
            }
            if (align == "end" || align == "right" || align == "bottom")
            {
                return parentExtent - elemExtent;
            }
            return explicitValue;
        }

        private static bool IsSpaceAlign(string align)
        {
            return align == "space-between"
                || align == "space-around"
                || align == "space-evenly";
        }

        private static void FillGaps(List<float> gaps, int count, float value)
        {
            for (var i = 0; i < count; i++)
            {
                gaps.Add(value);
            }
        }
    }
}
