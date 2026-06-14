#if UNITY_EDITOR

using System;
using System.IO;
using UnityEngine;

namespace RogueCard.Editor.DraftWorkbench
{
    /// <summary>
    /// refined PNG alpha 质量校验服务。
    /// </summary>
    public static class DraftAlphaValidationService
    {
        /// <summary>
        /// 从 PNG 文件读取并校验 alpha 质量。
        /// </summary>
        public static DraftAlphaValidationResult ValidatePng(string absolutePath, bool requiresTransparentAlpha, ComfyUiServiceConfig config)
        {
            if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
            {
                return new DraftAlphaValidationResult
                {
                    Status = requiresTransparentAlpha ? DraftAlphaValidationStatus.Failed : DraftAlphaValidationStatus.NotRequired,
                    Message = "alpha validation failed: PNG 文件不存在。"
                };
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(absolutePath)))
                {
                    return new DraftAlphaValidationResult
                    {
                        Status = requiresTransparentAlpha ? DraftAlphaValidationStatus.Failed : DraftAlphaValidationStatus.NotRequired,
                        Message = "alpha validation failed: PNG 无法读取。"
                    };
                }

                return Validate(texture, requiresTransparentAlpha, config);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        /// <summary>
        /// 校验贴图 alpha 质量。
        /// </summary>
        public static DraftAlphaValidationResult Validate(Texture2D texture, bool requiresTransparentAlpha, ComfyUiServiceConfig config)
        {
            config?.Normalize();
            if (texture == null || texture.width <= 0 || texture.height <= 0)
            {
                return new DraftAlphaValidationResult
                {
                    Status = requiresTransparentAlpha ? DraftAlphaValidationStatus.Failed : DraftAlphaValidationStatus.NotRequired,
                    Message = "alpha validation failed: Texture 为空。"
                };
            }

            int transparentThreshold = config != null ? config.AlphaTransparentThreshold : 5;
            int opaqueThreshold = config != null ? config.AlphaOpaqueThreshold : 250;
            float minimumTransparentRatio = config != null ? config.MinimumTransparentPixelRatio : 0.001f;
            float maximumBorderOpaqueRatio = config != null ? config.MaximumBorderOpaqueRatio : 0.95f;

            Color32[] pixels = texture.GetPixels32();
            int total = pixels.Length;
            int minAlpha = 255;
            int maxAlpha = 0;
            int transparentCount = 0;
            int nonOpaqueCount = 0;
            int borderCount = 0;
            int borderOpaqueCount = 0;

            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    byte alpha = pixels[y * texture.width + x].a;
                    minAlpha = Math.Min(minAlpha, alpha);
                    maxAlpha = Math.Max(maxAlpha, alpha);
                    if (alpha <= transparentThreshold)
                    {
                        transparentCount++;
                    }

                    if (alpha < opaqueThreshold)
                    {
                        nonOpaqueCount++;
                    }

                    bool isBorder = x == 0 || y == 0 || x == texture.width - 1 || y == texture.height - 1;
                    if (isBorder)
                    {
                        borderCount++;
                        if (alpha >= opaqueThreshold)
                        {
                            borderOpaqueCount++;
                        }
                    }
                }
            }

            var result = new DraftAlphaValidationResult
            {
                MinAlpha = minAlpha,
                MaxAlpha = maxAlpha,
                TransparentRatio = total > 0 ? transparentCount / (float)total : 0f,
                NonOpaquePixelRatio = total > 0 ? nonOpaqueCount / (float)total : 0f,
                BorderOpaqueRatio = borderCount > 0 ? borderOpaqueCount / (float)borderCount : 0f
            };

            if (!requiresTransparentAlpha)
            {
                result.Status = DraftAlphaValidationStatus.NotRequired;
                result.Message = "alpha validation not required for OpaqueRect region.";
                return result;
            }

            if (result.TransparentRatio <= minimumTransparentRatio)
            {
                result.Status = DraftAlphaValidationStatus.Failed;
                result.Message = "transparent alpha required, but output is opaque or has no transparent pixels.";
                return result;
            }

            if (result.BorderOpaqueRatio > maximumBorderOpaqueRatio)
            {
                result.Status = DraftAlphaValidationStatus.NeedsReview;
                result.Message = $"border opaque ratio {result.BorderOpaqueRatio:0.###} exceeds threshold {maximumBorderOpaqueRatio:0.###}.";
                return result;
            }

            result.Status = DraftAlphaValidationStatus.Passed;
            result.Message = "alpha validation passed.";
            return result;
        }
    }
}

#endif
