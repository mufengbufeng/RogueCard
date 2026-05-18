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
        /// 从根对象按约定名称解析绑定。
        /// </summary>
        public static MonsterItemBindings From(GameObject root, GameObject buffTemplate, GameObject intentTemplate)
        {
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
}
