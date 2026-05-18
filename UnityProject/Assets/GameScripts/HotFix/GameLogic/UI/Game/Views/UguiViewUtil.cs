using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic
{
    /// <summary>
    /// 局内 UGUI 子视图共享工具，集中处理文本、销毁、模板实例化和进度条比例。
    /// </summary>
    internal static class UguiViewUtil
    {
        /// <summary>
        /// 设置 TMP 文本，目标为空时安全跳过。
        /// </summary>
        public static void SetText(TextMeshProUGUI text, string value)
        {
            if (text != null)
            {
                text.text = value ?? string.Empty;
            }
        }

        /// <summary>
        /// 设置 UGUI 图片填充比例，并把值限制在 0 到 1。
        /// </summary>
        public static void SetFillAmount(Image image, float ratio)
        {
            if (image != null)
            {
                image.fillAmount = Mathf.Clamp01(ratio);
            }
        }

        /// <summary>
        /// 设置 CanvasGroup 可见性，目标为空时回退到 GameObject active 状态。
        /// </summary>
        public static void SetVisible(GameObject root, CanvasGroup group, bool visible)
        {
            if (group != null)
            {
                group.alpha = visible ? 1f : 0f;
                group.blocksRaycasts = visible;
                group.interactable = visible;
                if (!group.gameObject.activeSelf)
                {
                    group.gameObject.SetActive(true);
                }
                return;
            }

            if (root != null)
            {
                root.SetActive(visible);
            }
        }

        /// <summary>
        /// 销毁运行时对象，兼容编辑器测试和运行时。
        /// </summary>
        public static void DestroyObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// 清空容器下除指定保留对象外的所有子物体。
        /// </summary>
        public static void ClearChildren(RectTransform container, GameObject keep = null)
        {
            if (container == null)
            {
                return;
            }

            for (int i = container.childCount - 1; i >= 0; i--)
            {
                Transform child = container.GetChild(i);
                if (keep != null && child.gameObject == keep)
                {
                    continue;
                }

                DestroyObject(child.gameObject);
            }
        }

        /// <summary>
        /// 从模板实例化 UGUI 项，并保证模板本体保持隐藏。
        /// </summary>
        public static GameObject InstantiateTemplate(GameObject template, RectTransform parent, string name)
        {
            if (template == null || parent == null)
            {
                return null;
            }

            template.SetActive(false);
            GameObject instance = UnityEngine.Object.Instantiate(template, parent, false);
            instance.name = string.IsNullOrEmpty(name) ? template.name : name;
            instance.SetActive(true);
            return instance;
        }

        /// <summary>
        /// 查找子节点中的 TMP 文本，优先按名称精确匹配，再回退到第一个 TMP。
        /// </summary>
        public static TextMeshProUGUI FindText(GameObject root, string name)
        {
            if (root == null)
            {
                return null;
            }

            foreach (TextMeshProUGUI text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (string.Equals(text.gameObject.name, name, StringComparison.Ordinal))
                {
                    return text;
                }
            }

            return root.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        /// <summary>
        /// 查找子节点中的 Image，优先按名称精确匹配。
        /// </summary>
        public static Image FindImage(GameObject root, string name)
        {
            if (root == null)
            {
                return null;
            }

            foreach (Image image in root.GetComponentsInChildren<Image>(true))
            {
                if (string.Equals(image.gameObject.name, name, StringComparison.Ordinal))
                {
                    return image;
                }
            }

            return root.GetComponentInChildren<Image>(true);
        }

        /// <summary>
        /// 按数值和总数计算比例，总数小于等于零时返回 null 表示不更新。
        /// </summary>
        public static float? SafeRatio(int value, int max)
        {
            if (max <= 0)
            {
                return null;
            }

            return (float)value / max;
        }
    }
}
