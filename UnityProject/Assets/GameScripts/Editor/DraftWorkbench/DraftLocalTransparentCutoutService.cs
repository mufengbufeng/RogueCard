#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace RogueCard.Editor.DraftWorkbench
{
    /// <summary>
    /// 透明前景的本地背景抠除兜底服务。
    /// </summary>
    public static class DraftLocalTransparentCutoutService
    {
        private const int MaxPaletteColors = 8;

        /// <summary>
        /// 从 raw crop 生成带透明 alpha 的本地 refined PNG。
        /// </summary>
        public static bool TryCreateCutoutPng(
            string inputAbsolutePath,
            string outputAbsolutePath,
            ComfyUiServiceConfig config,
            out DraftAlphaValidationResult validation,
            out string message)
        {
            validation = new DraftAlphaValidationResult();
            message = string.Empty;

            if (string.IsNullOrWhiteSpace(inputAbsolutePath) || !File.Exists(inputAbsolutePath))
            {
                validation.Status = DraftAlphaValidationStatus.Failed;
                validation.Message = "local cutout failed: raw PNG 文件不存在。";
                message = validation.Message;
                return false;
            }

            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Texture2D cutout = null;
            try
            {
                if (!source.LoadImage(File.ReadAllBytes(inputAbsolutePath)))
                {
                    validation.Status = DraftAlphaValidationStatus.Failed;
                    validation.Message = "local cutout failed: raw PNG 无法读取。";
                    message = validation.Message;
                    return false;
                }

                cutout = CreateCutoutTexture(source, config, out message);
                if (cutout == null)
                {
                    validation.Status = DraftAlphaValidationStatus.Failed;
                    validation.Message = string.IsNullOrWhiteSpace(message)
                        ? "local cutout failed: 无法识别边缘背景。"
                        : message;
                    return false;
                }

                validation = DraftAlphaValidationService.Validate(cutout, true, config);
                if (validation.Status != DraftAlphaValidationStatus.Passed)
                {
                    message = "local cutout alpha validation failed: " + validation.Message;
                    return false;
                }

                string directory = Path.GetDirectoryName(outputAbsolutePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllBytes(outputAbsolutePath, cutout.EncodeToPNG());
                message = "local background cutout refinement 成功。";
                return true;
            }
            catch (Exception ex)
            {
                validation.Status = DraftAlphaValidationStatus.Failed;
                validation.Message = "local cutout failed: " + ex.Message;
                message = validation.Message;
                return false;
            }
            finally
            {
                if (cutout != null) UnityEngine.Object.DestroyImmediate(cutout);
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        /// <summary>
        /// 基于边缘背景色连通区域创建透明抠图纹理。
        /// </summary>
        internal static Texture2D CreateCutoutTexture(Texture2D source, ComfyUiServiceConfig config, out string message)
        {
            message = string.Empty;
            if (source == null || source.width <= 1 || source.height <= 1)
            {
                message = "local cutout failed: raw crop 尺寸过小。";
                return null;
            }

            config?.Normalize();
            float threshold = config != null ? config.LocalCutoutColorDistance : 72f;
            Color32[] pixels = source.GetPixels32();
            List<Color32> palette = BuildBorderPalette(pixels, source.width, source.height);
            if (palette.Count == 0)
            {
                message = "local cutout failed: 边缘背景色采样为空。";
                return null;
            }

            bool[] background = BuildBackgroundMaskFromBorder(pixels, source.width, source.height, palette, threshold);
            bool[] foreground = BuildForegroundMask(pixels, background, config);
            foreground = KeepSignificantForegroundComponents(foreground, source.width, source.height, out int foregroundCount);
            int total = pixels.Length;

            if (foreground == null || foregroundCount <= 0 || foregroundCount >= total * 0.98f)
            {
                message = $"local cutout failed: 无法提取前景，source={source.width}x{source.height}。";
                return null;
            }

            var outputPixels = new Color32[total];
            for (int i = 0; i < total; i++)
            {
                Color32 color = pixels[i];
                if (foreground[i])
                {
                    color.a = 255;
                }
                else
                {
                    color.a = 0;
                }

                outputPixels[i] = color;
            }

            var cutout = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            cutout.SetPixels32(outputPixels);
            cutout.Apply(false, false);
            return cutout;
        }

        /// <summary>
        /// 从 raw crop 边缘选取主要背景色调色板。
        /// </summary>
        private static List<Color32> BuildBorderPalette(Color32[] pixels, int width, int height)
        {
            var buckets = new Dictionary<int, ColorBucket>();
            int borderCount = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!IsBorder(x, y, width, height))
                        continue;

                    borderCount++;
                    Color32 color = pixels[y * width + x];
                    int key = Quantize(color);
                    ColorBucket bucket;
                    buckets.TryGetValue(key, out bucket);
                    bucket.Count++;
                    bucket.R += color.r;
                    bucket.G += color.g;
                    bucket.B += color.b;
                    buckets[key] = bucket;
                }
            }

            int minCount = Mathf.Max(2, Mathf.RoundToInt(borderCount * 0.025f));
            var palette = buckets
                .Where(pair => pair.Value.Count >= minCount)
                .OrderByDescending(pair => pair.Value.Count)
                .Take(MaxPaletteColors)
                .Select(pair => pair.Value.ToColor())
                .ToList();

            if (palette.Count == 0 && borderCount > 0)
            {
                ColorBucket bucket = buckets.Values.OrderByDescending(value => value.Count).First();
                palette.Add(bucket.ToColor());
            }

            return palette;
        }

        /// <summary>
        /// 从边缘种子按相邻颜色连续性 flood fill 背景。
        /// </summary>
        private static bool[] BuildBackgroundMaskFromBorder(
            Color32[] pixels,
            int width,
            int height,
            List<Color32> palette,
            float threshold)
        {
            var background = new bool[pixels.Length];
            var queue = new Queue<int>();
            for (int y = 0; y < height; y++)
            {
                EnqueueBackgroundSeed(0, y, width, background, queue);
                EnqueueBackgroundSeed(width - 1, y, width, background, queue);
            }

            for (int x = 1; x < width - 1; x++)
            {
                EnqueueBackgroundSeed(x, 0, width, background, queue);
                EnqueueBackgroundSeed(x, height - 1, width, background, queue);
            }

            float neighborThreshold = Mathf.Max(16f, threshold * 1.12f);
            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                int x = index % width;
                int y = index / width;
                TryVisitBackgroundNeighbor(x - 1, y, index, pixels, width, height, palette, threshold, neighborThreshold, background, queue);
                TryVisitBackgroundNeighbor(x + 1, y, index, pixels, width, height, palette, threshold, neighborThreshold, background, queue);
                TryVisitBackgroundNeighbor(x, y - 1, index, pixels, width, height, palette, threshold, neighborThreshold, background, queue);
                TryVisitBackgroundNeighbor(x, y + 1, index, pixels, width, height, palette, threshold, neighborThreshold, background, queue);
            }

            return background;
        }

        /// <summary>
        /// 加入边缘背景种子。
        /// </summary>
        private static void EnqueueBackgroundSeed(int x, int y, int width, bool[] background, Queue<int> queue)
        {
            int index = y * width + x;
            if (background[index])
                return;

            background[index] = true;
            queue.Enqueue(index);
        }

        /// <summary>
        /// 判断并访问一个候选背景邻居。
        /// </summary>
        private static void TryVisitBackgroundNeighbor(
            int x,
            int y,
            int sourceIndex,
            Color32[] pixels,
            int width,
            int height,
            List<Color32> palette,
            float paletteThreshold,
            float neighborThreshold,
            bool[] background,
            Queue<int> queue)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
                return;

            int index = y * width + x;
            if (background[index])
                return;

            Color32 source = pixels[sourceIndex];
            Color32 candidate = pixels[index];
            bool closeToCurrentBackground = ColorDistance(source, candidate) <= neighborThreshold;
            bool closeToBorderPalette = DistanceToPalette(candidate, palette) <= paletteThreshold;
            if (!closeToCurrentBackground && !closeToBorderPalette)
                return;

            background[index] = true;
            queue.Enqueue(index);
        }

        /// <summary>
        /// 从背景 mask 反推出候选前景。
        /// </summary>
        private static bool[] BuildForegroundMask(Color32[] pixels, bool[] background, ComfyUiServiceConfig config)
        {
            int transparentThreshold = config != null ? config.AlphaTransparentThreshold : 5;
            var foreground = new bool[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                foreground[i] = !background[i] && pixels[i].a > transparentThreshold;
            }

            return foreground;
        }

        /// <summary>
        /// 保留主要前景连通块并清掉小噪声。
        /// </summary>
        private static bool[] KeepSignificantForegroundComponents(bool[] foreground, int width, int height, out int keptCount)
        {
            keptCount = 0;
            if (foreground == null)
                return null;

            int total = foreground.Length;
            var visited = new bool[total];
            var components = new List<List<int>>();
            for (int i = 0; i < total; i++)
            {
                if (!foreground[i] || visited[i])
                    continue;

                components.Add(CollectForegroundComponent(i, foreground, visited, width, height));
            }

            if (components.Count == 0)
                return null;

            int largest = components.Max(component => component.Count);
            int minArea = Mathf.Max(2, Mathf.RoundToInt(total * 0.0015f));
            int keepArea = Mathf.Max(minArea, Mathf.RoundToInt(largest * 0.06f));
            var cleaned = new bool[total];
            bool keptAny = false;
            foreach (var component in components)
            {
                if (component.Count < keepArea && component.Count < largest)
                    continue;

                keptAny = true;
                for (int i = 0; i < component.Count; i++)
                {
                    int index = component[i];
                    cleaned[index] = true;
                    keptCount++;
                }
            }

            if (keptAny)
                return cleaned;

            var largestComponent = components.OrderByDescending(component => component.Count).First();
            for (int i = 0; i < largestComponent.Count; i++)
            {
                int index = largestComponent[i];
                cleaned[index] = true;
                keptCount++;
            }

            return cleaned;
        }

        /// <summary>
        /// 收集一个四邻域前景连通块。
        /// </summary>
        private static List<int> CollectForegroundComponent(
            int startIndex,
            bool[] foreground,
            bool[] visited,
            int width,
            int height)
        {
            var component = new List<int>();
            var queue = new Queue<int>();
            visited[startIndex] = true;
            queue.Enqueue(startIndex);
            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                component.Add(index);
                int x = index % width;
                int y = index / width;
                EnqueueForegroundNeighbor(x - 1, y, foreground, visited, width, height, queue);
                EnqueueForegroundNeighbor(x + 1, y, foreground, visited, width, height, queue);
                EnqueueForegroundNeighbor(x, y - 1, foreground, visited, width, height, queue);
                EnqueueForegroundNeighbor(x, y + 1, foreground, visited, width, height, queue);
            }

            return component;
        }

        /// <summary>
        /// 加入前景连通块候选邻居。
        /// </summary>
        private static void EnqueueForegroundNeighbor(
            int x,
            int y,
            bool[] foreground,
            bool[] visited,
            int width,
            int height,
            Queue<int> queue)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
                return;

            int index = y * width + x;
            if (visited[index] || !foreground[index])
                return;

            visited[index] = true;
            queue.Enqueue(index);
        }

        /// <summary>
        /// 计算颜色到背景调色板的最小距离。
        /// </summary>
        private static float DistanceToPalette(Color32 color, List<Color32> palette)
        {
            float min = float.MaxValue;
            for (int i = 0; i < palette.Count; i++)
            {
                min = Mathf.Min(min, ColorDistance(color, palette[i]));
            }

            return min;
        }

        /// <summary>
        /// RGB 欧氏距离。
        /// </summary>
        private static float ColorDistance(Color32 a, Color32 b)
        {
            int dr = a.r - b.r;
            int dg = a.g - b.g;
            int db = a.b - b.b;
            return Mathf.Sqrt(dr * dr + dg * dg + db * db);
        }

        /// <summary>
        /// 判断坐标是否位于纹理边缘。
        /// </summary>
        private static bool IsBorder(int x, int y, int width, int height)
        {
            return x == 0 || y == 0 || x == width - 1 || y == height - 1;
        }

        /// <summary>
        /// 将颜色量化为调色板桶 key。
        /// </summary>
        private static int Quantize(Color32 color)
        {
            return (color.r / 32) << 16 | (color.g / 32) << 8 | color.b / 32;
        }

        private struct ColorBucket
        {
            public int Count;
            public int R;
            public int G;
            public int B;

            public Color32 ToColor()
            {
                int count = Mathf.Max(1, Count);
                return new Color32(
                    (byte)Mathf.Clamp(R / count, 0, 255),
                    (byte)Mathf.Clamp(G / count, 0, 255),
                    (byte)Mathf.Clamp(B / count, 0, 255),
                    255);
            }
        }
    }
}

#endif
