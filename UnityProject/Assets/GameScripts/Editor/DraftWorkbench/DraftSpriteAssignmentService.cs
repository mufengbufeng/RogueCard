#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RogueCard.Editor.DraftWorkbench
{
    /// <summary>
    /// 负责将自动切图生成的 Sprite 与 Draft Workbench 预览节点匹配成可审查的应用计划。
    /// </summary>
    public static class DraftSpriteAssignmentService
    {
        /// <summary>
        /// 根据 marker、spriteHint、bbox IoU 和节点名称生成 Sprite 应用计划。
        /// </summary>
        /// <param name="prefabRoot">可编辑 prefab 根节点。</param>
        /// <param name="previewChanges">当前结构预览节点。</param>
        /// <param name="generatedSprites">自动切图生成的 Sprite 列表。</param>
        /// <param name="config">ComfyUI 配置。</param>
        /// <returns>候选应用计划。</returns>
        public static List<DraftSpriteAssignment> BuildAssignments(
            GameObject prefabRoot,
            IReadOnlyList<UguiNodeChange> previewChanges,
            IReadOnlyList<DraftGeneratedSprite> generatedSprites,
            ComfyUiServiceConfig config)
        {
            if (previewChanges == null) throw new ArgumentNullException(nameof(previewChanges));
            if (generatedSprites == null) throw new ArgumentNullException(nameof(generatedSprites));
            if (config == null) throw new ArgumentNullException(nameof(config));

            var assignments = new List<DraftSpriteAssignment>();
            var usedRegionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var change in previewChanges)
            {
                var descriptor = change?.Descriptor;
                if (descriptor == null || change.HasConflict || descriptor.Visuals == null)
                    continue;

                if (!UguiElementFactory.HasComponentType(descriptor, "UnityEngine.UI.Image"))
                    continue;

                DraftGeneratedSprite best = null;
                float bestScore = 0f;
                string bestReason = string.Empty;

                foreach (var sprite in generatedSprites)
                {
                    if (sprite == null || string.IsNullOrEmpty(sprite.SpriteAssetPath))
                        continue;

                    if (!string.IsNullOrEmpty(sprite.RegionId) && usedRegionIds.Contains(sprite.RegionId))
                        continue;

                    float score = ScoreMatch(descriptor, sprite, out string reason);
                    if (score > bestScore)
                    {
                        best = sprite;
                        bestScore = score;
                        bestReason = reason;
                    }
                }

                if (best == null || bestScore <= 0f)
                    continue;

                string nodePath = ResolveNodePath(prefabRoot, descriptor);
                Sprite existingSprite = GetExistingSprite(prefabRoot, nodePath);
                string existingPath = existingSprite != null ? AssetDatabase.GetAssetPath(existingSprite) : string.Empty;
                string existingGuid = !string.IsNullOrEmpty(existingPath) ? AssetDatabase.AssetPathToGUID(existingPath) : string.Empty;

                bool hasExistingSprite = existingSprite != null;
                bool lowMatchConfidence = bestScore < config.AssignmentAutoApproveConfidence;
                bool lowRegionConfidence = best.Confidence > 0f && best.Confidence < config.AssignmentAutoApproveConfidence;
                bool requiresReview = hasExistingSprite || lowMatchConfidence || lowRegionConfidence;
                string reviewReason = hasExistingSprite
                    ? "目标节点已有 Sprite，应用前需要确认覆盖风险。"
                    : lowRegionConfidence
                        ? $"区域置信度 {best.Confidence:0.00} 低于自动批准阈值 {config.AssignmentAutoApproveConfidence:0.00}。"
                        : lowMatchConfidence
                            ? $"匹配置信度 {bestScore:0.00} 低于自动批准阈值 {config.AssignmentAutoApproveConfidence:0.00}。"
                            : string.Empty;
                var assignment = new DraftSpriteAssignment
                {
                    NodePath = nodePath,
                    NodeName = descriptor.Name,
                    RegionId = best.RegionId,
                    Marker = best.Marker,
                    SpriteAssetPath = best.SpriteAssetPath,
                    SpriteGuid = best.SpriteGuid,
                    Confidence = Mathf.Clamp01(bestScore),
                    MatchReason = bestReason,
                    HasExistingSprite = hasExistingSprite,
                    ExistingSpriteAssetPath = existingPath,
                    ExistingSpriteGuid = existingGuid,
                    RequiresReview = requiresReview,
                    ReviewState = requiresReview ? DraftRegionReviewState.NeedsReview : DraftRegionReviewState.Included,
                    ReviewReason = reviewReason,
                    Approved = !requiresReview
                };

                assignments.Add(assignment);
                usedRegionIds.Add(best.RegionId);
            }

            return assignments;
        }

        /// <summary>
        /// 将已批准 assignment 的 Sprite 写入 descriptor visuals，便于后续常规 Apply 流程消费。
        /// </summary>
        /// <param name="previewChanges">当前结构预览节点。</param>
        /// <param name="assignments">应用计划列表。</param>
        public static void ApplyAssignmentsToDescriptors(
            IReadOnlyList<UguiNodeChange> previewChanges,
            IReadOnlyList<DraftSpriteAssignment> assignments)
        {
            if (previewChanges == null || assignments == null)
                return;

            var assignmentTargets = new HashSet<string>(
                assignments
                    .Where(a => a != null && !string.IsNullOrEmpty(a.NodePath))
                    .Select(a => a.NodePath),
                StringComparer.Ordinal);

            foreach (var change in previewChanges)
            {
                if (change?.Descriptor?.Visuals == null)
                    continue;

                string descriptorPath = PrefabDraftBuilder.GetDescriptorPath(change.Descriptor);
                if (!assignmentTargets.Contains(descriptorPath))
                    continue;

                change.Descriptor.Visuals.SpriteAssetPath = string.Empty;
                change.Descriptor.Visuals.SpriteGuid = string.Empty;
            }

            foreach (var assignment in assignments.Where(a => a != null && a.Approved))
            {
                var change = previewChanges.FirstOrDefault(c =>
                    c?.Descriptor != null && string.Equals(
                        PrefabDraftBuilder.GetDescriptorPath(c.Descriptor),
                        assignment.NodePath,
                        StringComparison.Ordinal));
                if (change?.Descriptor?.Visuals == null)
                    continue;

                change.Descriptor.Visuals.SpriteAssetPath = assignment.SpriteAssetPath;
                change.Descriptor.Visuals.SpriteGuid = assignment.SpriteGuid;
                change.Descriptor.Visuals.RegionId = assignment.RegionId;
                change.Descriptor.Visuals.AssetMarker = assignment.Marker;
            }
        }

        /// <summary>
        /// 计算单个节点与 Sprite 的匹配分数。
        /// </summary>
        private static float ScoreMatch(UguiNodeDescriptor descriptor, DraftGeneratedSprite sprite, out string reason)
        {
            float score = 0f;
            var reasons = new List<string>();
            string marker = NormalizeToken(sprite.Marker);
            string nodeName = NormalizeToken(descriptor.Name);
            string assetMarker = NormalizeToken(descriptor.Visuals.AssetMarker);
            string spriteHint = NormalizeToken(descriptor.Visuals.SpriteHint);
            string visualRegionId = NormalizeToken(descriptor.Visuals.RegionId);
            string spriteRegionId = NormalizeToken(sprite.RegionId);

            if (!string.IsNullOrEmpty(spriteRegionId) && spriteRegionId == visualRegionId)
            {
                score += 1.2f;
                reasons.Add("regionId 精确匹配");
            }

            if (!string.IsNullOrEmpty(marker) && marker == assetMarker)
            {
                score += 1f;
                reasons.Add("marker 精确匹配 asset");
            }

            if (!string.IsNullOrEmpty(marker) && marker == spriteHint)
            {
                score += 0.9f;
                reasons.Add("marker 精确匹配 spriteHint");
            }

            if (!string.IsNullOrEmpty(marker) && nodeName.Contains(marker))
            {
                score += 0.55f;
                reasons.Add("节点名包含 marker");
            }

            if (descriptor.HasSourceBounds)
            {
                float iou = CalculateIoU(descriptor.SourceBounds, sprite.SourceBounds);
                if (iou > 0f)
                {
                    score += Mathf.Clamp01(iou) * 0.8f;
                    reasons.Add($"bbox IoU={iou:0.00}");
                }
            }

            if (score <= 0f)
            {
                float tokenScore = TokenOverlapScore(nodeName, marker);
                if (tokenScore > 0f)
                {
                    score += tokenScore * 0.4f;
                    reasons.Add("节点名与 marker token 重叠");
                }
            }

            score *= Mathf.Clamp01(sprite.Confidence <= 0f ? 1f : sprite.Confidence);
            reason = reasons.Count > 0 ? string.Join("; ", reasons) : "未匹配";
            return score;
        }

        /// <summary>
        /// 解析 descriptor 对应的目标节点路径。
        /// </summary>
        private static string ResolveNodePath(GameObject prefabRoot, UguiNodeDescriptor descriptor)
        {
            if (prefabRoot != null)
            {
                string resolved = PrefabDraftBuilder.GetResolvedDescriptorPath(prefabRoot, descriptor);
                if (!string.IsNullOrEmpty(resolved))
                    return resolved;
            }

            return PrefabDraftBuilder.GetDescriptorPath(descriptor);
        }

        /// <summary>
        /// 读取目标节点当前 Sprite。
        /// </summary>
        private static Sprite GetExistingSprite(GameObject prefabRoot, string nodePath)
        {
            if (prefabRoot == null || string.IsNullOrEmpty(nodePath))
                return null;

            Transform target = prefabRoot.transform.Find(nodePath);
            var image = target != null ? target.GetComponent<Image>() : null;
            return image != null ? image.sprite : null;
        }

        /// <summary>
        /// 计算两个源图坐标系 Rect 的 IoU。
        /// </summary>
        private static float CalculateIoU(Rect a, Rect b)
        {
            float xMin = Mathf.Max(a.xMin, b.xMin);
            float yMin = Mathf.Max(a.yMin, b.yMin);
            float xMax = Mathf.Min(a.xMax, b.xMax);
            float yMax = Mathf.Min(a.yMax, b.yMax);
            float intersection = Mathf.Max(0f, xMax - xMin) * Mathf.Max(0f, yMax - yMin);
            float union = a.width * a.height + b.width * b.height - intersection;
            return union <= 0f ? 0f : intersection / union;
        }

        /// <summary>
        /// 计算简单 token 重叠分数。
        /// </summary>
        private static float TokenOverlapScore(string left, string right)
        {
            if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
                return 0f;

            var leftTokens = SplitTokens(left);
            var rightTokens = SplitTokens(right);
            int overlap = leftTokens.Count(token => rightTokens.Contains(token));
            return rightTokens.Count == 0 ? 0f : overlap / (float)rightTokens.Count;
        }

        /// <summary>
        /// 拆分 token。
        /// </summary>
        private static HashSet<string> SplitTokens(string value)
        {
            return new HashSet<string>(
                value.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries),
                StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 规范化字符串为小写 snake token。
        /// </summary>
        private static string NormalizeToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var builder = new StringBuilder(value.Length + 8);
            bool lastWasSeparator = false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsUpper(c) && i > 0 && !lastWasSeparator)
                    builder.Append('_');

                if (char.IsLetterOrDigit(c))
                {
                    builder.Append(char.ToLowerInvariant(c));
                    lastWasSeparator = false;
                }
                else if (!lastWasSeparator)
                {
                    builder.Append('_');
                    lastWasSeparator = true;
                }
            }

            return builder.ToString().Trim('_');
        }
    }
}
#endif
