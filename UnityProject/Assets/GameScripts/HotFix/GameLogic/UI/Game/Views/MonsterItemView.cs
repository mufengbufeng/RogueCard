using System;
using System.Collections.Generic;
using GameConfig.card;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic
{
    /// <summary>
    /// 单只怪物的 UGUI 项视图，渲染名称、血量、护甲、意图和 Buff。
    /// </summary>
    public sealed class MonsterItemView : IDisposable
    {
        /// <summary>
        /// 测试钩子：覆盖默认卡牌效果解析，避免测试依赖配置表。
        /// </summary>
        public static Func<Card, List<CardEffect>> EffectResolverOverride { get; set; }

        private MonsterItemBindings _bindings;
        private bool _disposed;

        /// <summary>
        /// 怪物项根对象。
        /// </summary>
        public GameObject Root { get; private set; }

        /// <summary>
        /// 怪物在原始 Monsters 列表中的索引。
        /// </summary>
        public int MonsterIndex { get; }

        /// <summary>
        /// 是否处于可选择目标视觉态。
        /// </summary>
        public bool IsTargetSelectable { get; private set; }

        /// <summary>
        /// 怪物点击事件，参数为原始怪物索引。
        /// </summary>
        public event Action<int> Clicked;

        /// <summary>
        /// 创建怪物项视图并渲染。
        /// </summary>
        public MonsterItemView(GameObject root, MonsterRuntime monster, int monsterIndex, int aliveMonsterCount, GameObject buffTemplate, GameObject intentTemplate)
        {
            Root = root;
            MonsterIndex = monsterIndex;
            _bindings = MonsterItemBindings.From(root, buffTemplate, intentTemplate);
            Render(monster, aliveMonsterCount);

            if (_bindings.Button != null)
            {
                _bindings.Button.onClick.AddListener(OnClicked);
            }
        }

        /// <summary>
        /// 切换目标选择高亮。
        /// </summary>
        public void SetTargetSelectable(bool selectable)
        {
            IsTargetSelectable = selectable;
            if (_bindings.TargetHighlight != null)
            {
                _bindings.TargetHighlight.SetActive(selectable);
            }
        }

        /// <summary>
        /// 释放按钮事件。
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_bindings.Button != null)
            {
                _bindings.Button.onClick.RemoveListener(OnClicked);
            }

            Clicked = null;
            Root = null;
            _bindings = default;
        }

        private void Render(MonsterRuntime monster, int aliveMonsterCount)
        {
            if (monster == null)
            {
                return;
            }

            UguiViewUtil.SetText(_bindings.NameText, monster.Config != null ? monster.Config.Name : $"怪物{MonsterIndex + 1}");
            UguiViewUtil.SetText(_bindings.HpText, monster.Armor > 0 ? $"{monster.Hp}/{monster.MaxHp} 护甲:{monster.Armor}" : $"{monster.Hp}/{monster.MaxHp}");

            float? hpRatio = UguiViewUtil.SafeRatio(monster.Hp, monster.MaxHp);
            if (hpRatio.HasValue)
            {
                UguiViewUtil.SetFillAmount(_bindings.HpFill, hpRatio.Value);
            }

            RenderIntents(monster, aliveMonsterCount);
            BuffIconRenderer.Render(_bindings.BuffBar, _bindings.BuffIconTemplate, monster.Buffs);
            SetTargetSelectable(false);
        }

        private void RenderIntents(MonsterRuntime monster, int aliveMonsterCount)
        {
            if (_bindings.IntentBar == null)
            {
                return;
            }

            UguiViewUtil.ClearChildren(_bindings.IntentBar, _bindings.IntentIconTemplate);
            if (_bindings.IntentIconTemplate == null || monster.PendingCards == null)
            {
                return;
            }

            int iconIndex = 0;
            foreach (Card card in monster.PendingCards)
            {
                if (card == null)
                {
                    continue;
                }

                var effects = ResolveCardEffects(card);
                foreach (CardEffect effect in effects)
                {
                    GameObject icon = UguiViewUtil.InstantiateTemplate(_bindings.IntentIconTemplate, _bindings.IntentBar, $"IntentIcon_{iconIndex++}");
                    BuffIconRenderer.RenderIcon(icon, GetIntentText(card, effect, aliveMonsterCount), effect.Kind);
                }
            }
        }

        private static string GetIntentText(Card card, CardEffect effect, int aliveMonsterCount)
        {
            int displayValue = effect.Value;
            if (effect.Kind == EffectKind.Damage && card.TargetMode == TargetMode.SplitAcrossAll && aliveMonsterCount > 0)
            {
                displayValue = Math.Max(1, effect.Value / aliveMonsterCount);
            }

            return effect.Kind == EffectKind.DamageDot
                ? $"{displayValue}×{effect.Duration}"
                : displayValue.ToString();
        }

        private static List<CardEffect> ResolveCardEffects(Card card)
        {
            if (card == null)
            {
                return new List<CardEffect>();
            }

            Func<Card, List<CardEffect>> overrideFunc = EffectResolverOverride;
            if (overrideFunc != null)
            {
                return overrideFunc(card) ?? new List<CardEffect>();
            }

            var result = new List<CardEffect>();
            var tables = GameLogicEntry.Config?.Tables;
            if (tables == null)
            {
                return result;
            }

            foreach (CardEffect effect in tables.TbCardEffect.DataList)
            {
                if (effect.CardId == card.Id)
                {
                    result.Add(effect);
                }
            }

            return result;
        }

        private void OnClicked()
        {
            Clicked?.Invoke(MonsterIndex);
        }
    }

    /// <summary>
    /// 怪物项 UGUI 绑定。
    /// </summary>
    internal struct MonsterItemBindings
    {
        public TextMeshProUGUI NameText;
        public Image HpFill;
        public TextMeshProUGUI HpText;
        public RectTransform IntentBar;
        public RectTransform BuffBar;
        public Button Button;
        public GameObject TargetHighlight;
        public GameObject BuffIconTemplate;
        public GameObject IntentIconTemplate;

        /// <summary>
        /// 从根对象按约定名称解析绑定；模板若未提供 HP/名称等子节点则按默认布局自动补齐。
        /// </summary>
        public static MonsterItemBindings From(GameObject root, GameObject buffTemplate, GameObject intentTemplate)
        {
            if (root != null)
            {
                MonsterItemDefaultLayout.EnsureChildren(root);
            }

            return new MonsterItemBindings
            {
                NameText = UguiViewUtil.FindText(root, "NameText") ?? UguiViewUtil.FindText(root, "MonsterNameText"),
                HpFill = UguiViewUtil.FindImage(root, "HpFill") ?? UguiViewUtil.FindImage(root, "MonsterHpFill"),
                HpText = UguiViewUtil.FindText(root, "HpText") ?? UguiViewUtil.FindText(root, "MonsterHpText"),
                IntentBar = FindRect(root, "IntentBar"),
                BuffBar = FindRect(root, "BuffBar"),
                Button = root != null ? root.GetComponent<Button>() ?? root.GetComponentInChildren<Button>(true) : null,
                TargetHighlight = FindGameObject(root, "TargetHighlight"),
                BuffIconTemplate = buffTemplate,
                IntentIconTemplate = intentTemplate,
            };
        }

        private static RectTransform FindRect(GameObject root, string name)
        {
            GameObject target = FindGameObject(root, name);
            return target != null ? target.GetComponent<RectTransform>() : null;
        }

        private static GameObject FindGameObject(GameObject root, string name)
        {
            if (root == null)
            {
                return null;
            }

            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (string.Equals(transform.gameObject.name, name, StringComparison.Ordinal))
                {
                    return transform.gameObject;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// 怪物条目模板默认布局补齐工具。
    /// 当美术 Prefab 只提供根 Image 时，运行时按统一布局补齐名称、血量、意图、Buff 与目标高亮。
    /// 已存在同名子节点会被保留。
    /// </summary>
    internal static class MonsterItemDefaultLayout
    {
        public static void EnsureChildren(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            var rootRect = root.GetComponent<RectTransform>();
            if (rootRect == null)
            {
                return;
            }

            EnsureRootClickable(root);
            EnsureTargetHighlight(rootRect);
            EnsureNameText(rootRect);
            EnsureHpBar(rootRect);
            EnsureIntentBar(rootRect);
            EnsureBuffBar(rootRect);
        }

        private static void EnsureRootClickable(GameObject root)
        {
            if (root.GetComponent<Image>() == null)
            {
                root.AddComponent<Image>();
            }

            if (root.GetComponent<Button>() == null)
            {
                root.AddComponent<Button>();
            }
        }

        private static void EnsureTargetHighlight(RectTransform rootRect)
        {
            if (FindChild(rootRect, "TargetHighlight") != null)
            {
                return;
            }

            var go = CreateChild(rootRect, "TargetHighlight");
            go.transform.SetSiblingIndex(0);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            rt.offsetMin = new Vector2(-8f, -8f);
            rt.offsetMax = new Vector2(8f, 8f);
            var img = go.AddComponent<Image>();
            img.color = new Color(1f, 0.85f, 0.2f, 0.45f);
            img.raycastTarget = false;
            go.SetActive(false);
        }

        private static void EnsureNameText(RectTransform rootRect)
        {
            if (FindChild(rootRect, "NameText") != null || FindChild(rootRect, "MonsterNameText") != null)
            {
                return;
            }

            var go = CreateChild(rootRect, "NameText");
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 56f);
            rt.sizeDelta = new Vector2(-8f, 24f);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 22f;
            tmp.color = Color.white;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
        }

        private static void EnsureHpBar(RectTransform rootRect)
        {
            bool hasFill = FindChild(rootRect, "HpFill") != null || FindChild(rootRect, "MonsterHpFill") != null;
            bool hasText = FindChild(rootRect, "HpText") != null || FindChild(rootRect, "MonsterHpText") != null;
            if (hasFill && hasText)
            {
                return;
            }

            var bar = CreateChild(rootRect, "HpBar");
            var barRect = bar.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0f, 0f);
            barRect.anchorMax = new Vector2(1f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.anchoredPosition = new Vector2(0f, 28f);
            barRect.sizeDelta = new Vector2(-16f, 22f);
            var bg = bar.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.55f);
            bg.raycastTarget = false;

            if (!hasFill)
            {
                var fillGo = CreateChild(barRect, "HpFill");
                var fillRect = fillGo.GetComponent<RectTransform>();
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = Vector2.one;
                fillRect.pivot = new Vector2(0.5f, 0.5f);
                fillRect.anchoredPosition = Vector2.zero;
                fillRect.sizeDelta = Vector2.zero;
                var fill = fillGo.AddComponent<Image>();
                fill.color = new Color(0.78f, 0.18f, 0.18f, 1f);
                fill.raycastTarget = false;
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;
                fill.fillOrigin = (int)Image.OriginHorizontal.Left;
                fill.fillAmount = 1f;
            }

            if (!hasText)
            {
                var textGo = CreateChild(barRect, "HpText");
                var textRect = textGo.GetComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.pivot = new Vector2(0.5f, 0.5f);
                textRect.anchoredPosition = Vector2.zero;
                textRect.sizeDelta = Vector2.zero;
                var tmp = textGo.AddComponent<TextMeshProUGUI>();
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontSize = 16f;
                tmp.color = Color.white;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.raycastTarget = false;
            }
        }

        private static void EnsureIntentBar(RectTransform rootRect)
        {
            if (FindChild(rootRect, "IntentBar") != null)
            {
                return;
            }

            var go = CreateChild(rootRect, "IntentBar");
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -4f);
            rt.sizeDelta = new Vector2(-8f, 32f);
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        private static void EnsureBuffBar(RectTransform rootRect)
        {
            if (FindChild(rootRect, "BuffBar") != null)
            {
                return;
            }

            var go = CreateChild(rootRect, "BuffBar");
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 4f);
            rt.sizeDelta = new Vector2(-8f, 22f);
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        private static GameObject CreateChild(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Transform FindChild(Transform parent, string name)
        {
            foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
            {
                if (t == parent)
                {
                    continue;
                }
                if (string.Equals(t.gameObject.name, name, StringComparison.Ordinal))
                {
                    return t;
                }
            }

            return null;
        }
    }
}
