using System.Collections.Generic;
using GameConfig.card;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic
{
    /// <summary>
    /// 渲染玩家与怪物共用的 Buff 图标列表。
    /// </summary>
    internal static class BuffIconRenderer
    {
        private static readonly Color DotColor = new Color(0.66f, 0.22f, 0.86f, 1f);
        private static readonly Color DefaultColor = new Color(0.18f, 0.24f, 0.32f, 1f);

        /// <summary>
        /// 清空容器并按 Buff 顺序实例化图标。
        /// </summary>
        public static void Render(RectTransform container, GameObject template, IEnumerable<BuffRuntime> buffs)
        {
            if (container == null)
            {
                return;
            }

            UguiViewUtil.ClearChildren(container, template);

            if (template == null || buffs == null)
            {
                return;
            }

            int i = 0;
            foreach (BuffRuntime buff in buffs)
            {
                if (buff == null)
                {
                    continue;
                }

                GameObject icon = UguiViewUtil.InstantiateTemplate(template, container, $"BuffIcon_{i}");
                RenderIcon(icon, $"{buff.Value}×{buff.RemainingTurns}", buff.Kind);
                i++;
            }
        }

        /// <summary>
        /// 渲染单个图标文本与颜色。
        /// </summary>
        public static void RenderIcon(GameObject icon, string text, EffectKind kind)
        {
            if (icon == null)
            {
                return;
            }

            var label = icon.GetComponentInChildren<TextMeshProUGUI>(true);
            UguiViewUtil.SetText(label, text);

            var image = icon.GetComponent<Image>();
            if (image != null)
            {
                image.color = kind == EffectKind.DamageDot ? DotColor : DefaultColor;
            }
        }
    }
}
