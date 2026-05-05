using System;
using System.Linq;
using UnityEngine;

namespace GT
{
    internal static class ReferenceCollectorAutoCollectService
    {
        /// <summary>
        /// 按当前项目规则自动收集 ReferenceCollector 子节点引用。
        /// </summary>
        internal static int AutoCollect(ReferenceCollector collector)
        {
            if (collector == null)
            {
                return 0;
            }

            int collectedCount = 0;
            var allChildren = collector.GetComponentsInChildren<Transform>(true);

            foreach (var childTransform in allChildren)
            {
                if (childTransform == collector.transform)
                {
                    continue;
                }

                var childName = childTransform.name;
                var rule = ReferenceCollectorRuleService.FindFirstMatchingRule(childName);
                if (rule == null)
                {
                    continue;
                }

                if (collector.data.Any(item => item != null && string.Equals(item.key, childName, StringComparison.Ordinal)))
                {
                    continue;
                }

                if (!ReferenceCollectorRuleService.TryResolveTargetObject(childTransform, rule, out var targetObject, out var warning))
                {
                    Debug.LogWarning(warning, childTransform.gameObject);
                    continue;
                }

                collector.Add(childName, targetObject);
                collectedCount++;
                Debug.Log($"[ReferenceCollector] 自动收集：{childName} -> {targetObject.GetType().Name}", childTransform.gameObject);
            }

            if (collectedCount > 0)
            {
                collector.Sort();
                Debug.Log($"[ReferenceCollector] 自动收集完成，共收集 {collectedCount} 个引用", collector);
            }
            else
            {
                Debug.Log("[ReferenceCollector] 未找到符合项目规则的新引用", collector);
            }

            return collectedCount;
        }

        /// <summary>
        /// 按当前项目规则清除自动收集项。
        /// </summary>
        internal static int ClearAutoCollected(ReferenceCollector collector)
        {
            if (collector == null || collector.data == null)
            {
                return 0;
            }

            var keys = collector.data
                .Where(item => item != null && ReferenceCollectorRuleService.IsAutoCollectedKey(item.key))
                .Select(item => item.key)
                .ToList();

            foreach (var key in keys)
            {
                collector.Remove(key);
            }

            if (keys.Count > 0)
            {
                Debug.Log($"[ReferenceCollector] 清除自动收集引用完成，共清除 {keys.Count} 个引用", collector);
            }

            return keys.Count;
        }
    }
}
