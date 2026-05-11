using System;
using System.Collections.Generic;
using GameConfig.card;
using UnityEngine.UIElements;

namespace GameLogic
{
    /// <summary>
    /// 单只怪物的视图控制器。封装名称 / HP 进度条与文本（含护甲附加） / PendingCard 意图渲染 / Buff 状态条。
    /// 不订阅 ReactiveProperty —— MonsterRuntime 是一次性快照，调用方在外部刷新时整体重建。
    /// IDisposable 仅作为生命周期约定，留接口给后续 change 扩展（如 SetTargetSelectable）。
    /// </summary>
    public class MonsterItemView : IDisposable
    {
        /// <summary>
        /// EditMode 测试钩子：覆盖默认的 effects 解析逻辑。
        /// 默认从 `GameLogicEntry.Config.Tables.TbCardEffect.DataList` 按 CardId 收集；
        /// 测试中通过此委托注入手工构造的 effect 列表，避免依赖静态配置加载。
        /// 生产代码 SHALL NOT 设置此字段。
        /// </summary>
        public static Func<Card, List<CardEffect>> EffectResolverOverride { get; set; }

        private VisualElement _root;
        private bool _disposed;

        /// <summary>
        /// 怪物项根节点（CloneTree 后从 TemplateContainer 中分离的真正容器）。
        /// 由 MonsterListView 添加到 monster-container，对应 UXML 的 monster-item 模板。
        /// </summary>
        public VisualElement Root => _root;

        /// <summary>
        /// 构造单只怪物视图并完成全部渲染。
        /// </summary>
        /// <param name="root">已经 CloneTree 的怪物项根节点（含 name-text / hp-bar / hp-text / intent-container / buff-bar 子元素）。</param>
        /// <param name="monster">怪物运行时数据；null 时不做任何渲染（防御性）。</param>
        /// <param name="aliveMonsterCount">当前存活怪物数；供 SplitAcrossAll 类卡牌平分伤害文本计算。</param>
        public MonsterItemView(VisualElement root, MonsterRuntime monster, int aliveMonsterCount)
        {
            _root = root;
            if (_root == null || monster == null) return;

            Render(monster, aliveMonsterCount);
        }

        /// <summary>
        /// 渲染怪物所有视觉元素。HP 进度条按百分比，文本含护甲附加，intent-container 与 buff-bar 全量重建。
        /// </summary>
        private void Render(MonsterRuntime monster, int aliveMonsterCount)
        {
            // 兼容旧 intent-text 标签（清空，避免遗留文本影响新意图区显示）
            var legacyIntent = _root.Q<Label>("intent-text");
            if (legacyIntent != null) legacyIntent.text = string.Empty;

            // 名称
            var nameLabel = _root.Q<Label>("name-text");
            if (nameLabel != null && monster.Config != null)
            {
                nameLabel.text = monster.Config.Name;
            }

            // HP 进度条
            var hpBar = _root.Q("hp-bar");
            if (hpBar != null && monster.MaxHp > 0)
            {
                float hpPercent = (float)monster.Hp / monster.MaxHp;
                hpBar.style.width = new StyleLength(new Length(hpPercent * 100, LengthUnit.Percent));
            }

            // HP 文本（含护甲附加）
            var hpText = _root.Q<Label>("hp-text");
            if (hpText != null)
            {
                string hpStr = $"HP:{monster.Hp}/{monster.MaxHp}";
                if (monster.Armor > 0) hpStr += $" 护甲:{monster.Armor}";
                hpText.text = hpStr;
            }

            // PendingCards 意图：每张卡一个 .intent-card，内部按 effects 渲染 .intent-icon
            var intentContainer = _root.Q("intent-container");
            if (intentContainer != null)
            {
                intentContainer.Clear();
                if (monster.PendingCards != null)
                {
                    foreach (var card in monster.PendingCards)
                    {
                        RenderIntentCard(intentContainer, card, aliveMonsterCount);
                    }
                }
            }

            // Buff 状态条（共享渲染规则）
            var buffBar = _root.Q("buff-bar");
            BuffBarRenderer.Render(buffBar, monster.Buffs);
        }

        /// <summary>
        /// 把一张 PendingCard 的 effects 列表渲染到 intent-container 中：
        /// 每张卡一个 .intent-card，每条 effect 一个 .intent-icon，按 EffectKind 加颜色类与文本格式。
        /// SplitAcrossAll + Damage 时按 max(1, value/aliveCount) 显示平分伤害。
        /// </summary>
        private static void RenderIntentCard(VisualElement intentContainer, Card card, int aliveMonsterCount)
        {
            if (intentContainer == null || card == null) return;

            var effects = ResolveCardEffects(card);
            if (effects.Count == 0) return;

            var intentCard = new VisualElement
            {
                pickingMode = PickingMode.Ignore
            };
            intentCard.AddToClassList("intent-card");

            bool isSplit = card.TargetMode == TargetMode.SplitAcrossAll;

            foreach (var effect in effects)
            {
                var icon = new Label
                {
                    pickingMode = PickingMode.Ignore
                };
                icon.AddToClassList("intent-icon");

                int displayValue = effect.Value;
                if (isSplit && effect.Kind == EffectKind.Damage && aliveMonsterCount > 0)
                {
                    displayValue = Math.Max(1, effect.Value / aliveMonsterCount);
                }

                switch (effect.Kind)
                {
                    case EffectKind.Damage:
                        icon.AddToClassList("intent-icon-damage");
                        icon.text = displayValue.ToString();
                        break;
                    case EffectKind.Shield:
                        icon.AddToClassList("intent-icon-shield");
                        icon.text = displayValue.ToString();
                        break;
                    case EffectKind.DamageDot:
                        icon.AddToClassList("intent-icon-dot");
                        icon.text = $"{displayValue}×{effect.Duration}";
                        break;
                    case EffectKind.EnergyGain:
                        icon.AddToClassList("intent-icon-energy");
                        icon.text = $"+{displayValue}";
                        break;
                    default:
                        icon.text = displayValue.ToString();
                        break;
                }

                intentCard.Add(icon);
            }

            intentContainer.Add(intentCard);
        }

        /// <summary>
        /// 收集指定卡的全部 CardEffect 行（按表中顺序）。
        /// 配置表缺失时（编辑器/测试边界）返回空列表，调用方按"无意图"处理。
        /// </summary>
        private static List<CardEffect> ResolveCardEffects(Card card)
        {
            if (card == null) return new List<CardEffect>();

            // 测试钩子优先于默认解析（仅 EditMode 测试场景设置）
            var overrideFunc = EffectResolverOverride;
            if (overrideFunc != null) return overrideFunc(card);

            var result = new List<CardEffect>();
            var tables = GameLogicEntry.Config?.Tables;
            if (tables == null) return result;

            foreach (var effect in tables.TbCardEffect.DataList)
            {
                if (effect.CardId == card.Id) result.Add(effect);
            }
            return result;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _root = null;
        }
    }
}
