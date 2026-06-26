#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RogueCard.Editor.DraftWorkbench
{
    /// <summary>
    /// 根据 ComfyUI/SAM region 与 mask 从源图裁剪 PNG。
    /// </summary>
    public static class DraftSpriteCropper
    {
        /// <summary>
        /// 将指定 region 裁剪为 PNG，并写入 Assets 下的目标路径。
        /// </summary>
        /// <param name="sourceImage">源设计图。</param>
        /// <param name="manifest">分割结果清单。</param>
        /// <param name="region">待裁剪区域。</param>
        /// <param name="outputAssetPath">输出 PNG 的 Assets 相对路径。</param>
        /// <param name="config">ComfyUI 配置。</param>
        public static void CropRegionToPng(
            Texture2D sourceImage,
            DraftSegmentationManifest manifest,
            DraftSegmentRegion region,
            string outputAssetPath,
            ComfyUiServiceConfig config)
        {
            if (sourceImage == null) throw new ArgumentNullException(nameof(sourceImage));
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            if (region == null) throw new ArgumentNullException(nameof(region));
            if (string.IsNullOrEmpty(outputAssetPath)) throw new ArgumentException("输出路径为空", nameof(outputAssetPath));

            Texture2D sourceReadable = LoadTextureFromAsset(sourceImage);
            Texture2D maskReadable = LoadMaskTexture(manifest, region);
            Rect expanded = region.expandedBbox != null && region.expandedBbox.width > 0 && region.expandedBbox.height > 0
                ? region.expandedBbox.ToRect()
                : DraftImageSegmentationService.ExpandBounds(region.bbox.ToRect(), sourceReadable.width, sourceReadable.height, config);

            int x = Mathf.Clamp(Mathf.RoundToInt(expanded.x), 0, sourceReadable.width - 1);
            int yTop = Mathf.Clamp(Mathf.RoundToInt(expanded.y), 0, sourceReadable.height - 1);
            int width = Mathf.Clamp(Mathf.RoundToInt(expanded.width), 1, sourceReadable.width - x);
            int height = Mathf.Clamp(Mathf.RoundToInt(expanded.height), 1, sourceReadable.height - yTop);

            bool useSquareCanvas = region.alphaMode == DraftAlphaMode.TransparentForeground && region.makeSquare;
            int outputWidth = useSquareCanvas ? Mathf.Max(width, height) : width;
            int outputHeight = useSquareCanvas ? Mathf.Max(width, height) : height;
            int offsetX = useSquareCanvas ? Mathf.Max(0, (outputWidth - width) / 2) : 0;
            int offsetY = useSquareCanvas ? Mathf.Max(0, (outputHeight - height) / 2) : 0;
            region.rawOutputWidth = outputWidth;
            region.rawOutputHeight = outputHeight;
            region.rawCanvasOffsetX = offsetX;
            region.rawCanvasOffsetY = offsetY;

            var cropped = new Texture2D(outputWidth, outputHeight, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[outputWidth * outputHeight];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(0, 0, 0, 0);
            }

            Color32[] sourcePixels = sourceReadable.GetPixels32();
            Color32[] maskPixels = maskReadable != null ? maskReadable.GetPixels32() : null;

            for (int row = 0; row < height; row++)
            {
                int sourceYFromTop = yTop + row;
                int sourceY = sourceReadable.height - 1 - sourceYFromTop;
                for (int col = 0; col < width; col++)
                {
                    int sourceX = x + col;
                    int sourceIndex = sourceY * sourceReadable.width + sourceX;
                    Color32 color = sourcePixels[sourceIndex];

                    if (maskReadable != null && maskPixels != null)
                    {
                        int maskX = Mathf.Clamp(sourceX, 0, maskReadable.width - 1);
                        int maskY = Mathf.Clamp(sourceY, 0, maskReadable.height - 1);
                        Color32 mask = maskPixels[maskY * maskReadable.width + maskX];
                        byte alpha = Math.Max(mask.a, Math.Max(mask.r, Math.Max(mask.g, mask.b)));
                        color.a = (byte)Mathf.RoundToInt(color.a * (alpha / 255f));
                    }

                    int targetX = offsetX + col;
                    int targetY = outputHeight - 1 - (offsetY + row);
                    pixels[targetY * outputWidth + targetX] = color;
                }
            }

            cropped.SetPixels32(pixels);
            cropped.Apply(false, false);

            string absoluteOutputPath = DraftPathUtility.ToAbsolutePath(outputAssetPath);
            string directory = Path.GetDirectoryName(absoluteOutputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(absoluteOutputPath, cropped.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(cropped);
            if (maskReadable != null) UnityEngine.Object.DestroyImmediate(maskReadable);
            if (sourceReadable != sourceImage) UnityEngine.Object.DestroyImmediate(sourceReadable);
        }

        /// <summary>
        /// 计算 mask alpha 的有效区域，并转换为源图左上角坐标系 bbox。
        /// </summary>
        /// <param name="mask">可读 mask 贴图。</param>
        /// <param name="bounds">输出区域框；无有效像素时为 null。</param>
        /// <param name="alphaThreshold">alpha/亮度阈值。</param>
        /// <returns>存在有效区域时返回 true。</returns>
        internal static bool TryCalculateAlphaBounds(Texture2D mask, out DraftSegmentBounds bounds, byte alphaThreshold)
        {
            bounds = null;
            if (mask == null || mask.width <= 0 || mask.height <= 0)
                return false;

            Color32[] pixels = mask.GetPixels32();
            int minX = mask.width;
            int minY = mask.height;
            int maxX = -1;
            int maxY = -1;
            for (int y = 0; y < mask.height; y++)
            {
                for (int x = 0; x < mask.width; x++)
                {
                    Color32 pixel = pixels[y * mask.width + x];
                    byte alpha = Math.Max(pixel.a, Math.Max(pixel.r, Math.Max(pixel.g, pixel.b)));
                    if (alpha < alphaThreshold)
                        continue;

                    minX = Math.Min(minX, x);
                    minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x);
                    maxY = Math.Max(maxY, y);
                }
            }

            if (maxX < minX || maxY < minY)
                return false;

            bounds = new DraftSegmentBounds
            {
                x = minX,
                y = mask.height - 1 - maxY,
                width = maxX - minX + 1,
                height = maxY - minY + 1
            };
            return true;
        }

        /// <summary>
        /// 从项目资源文件读取源图，避免修改原图 TextureImporter 的 Read/Write 设置。
        /// </summary>
        private static Texture2D LoadTextureFromAsset(Texture2D sourceImage)
        {
            string assetPath = AssetDatabase.GetAssetPath(sourceImage);
            if (!string.IsNullOrEmpty(assetPath))
            {
                string absolutePath = DraftPathUtility.ToAbsolutePath(assetPath);
                if (File.Exists(absolutePath))
                {
                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (texture.LoadImage(File.ReadAllBytes(absolutePath)))
                        return texture;
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }

            return sourceImage;
        }

        /// <summary>
        /// 加载 region mask 贴图；没有 mask 时返回 null。
        /// </summary>
        private static Texture2D LoadMaskTexture(DraftSegmentationManifest manifest, DraftSegmentRegion region)
        {
            if (string.IsNullOrEmpty(region.maskPath))
                return null;

            string maskPath = region.maskPath;
            if (!Path.IsPathRooted(maskPath))
            {
                maskPath = Path.Combine(manifest.manifestDirectory ?? string.Empty, maskPath);
            }

            if (!File.Exists(maskPath))
            {
                Debug.LogWarning($"[DraftSpriteCropper] 找不到 mask 文件，改用矩形裁剪: {maskPath}");
                return null;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(File.ReadAllBytes(maskPath)))
            {
                UnityEngine.Object.DestroyImmediate(texture);
                return null;
            }

            return texture;
        }
    }
}
#endif
