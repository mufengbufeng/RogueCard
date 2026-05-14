using System;
using System.Collections.Generic;
using GameConfig.card;

namespace GameLogic
{
    /// <summary>
    /// 卡牌效果执行器，作为玩家出牌和怪物行动的统一执行入口。无内部状态，纯函数式。
    /// </summary>
    public static class CardEffectExecutor
    {
        /// <summary>
        /// 执行一张卡的全部效果。
        /// </summary>
        /// <param name="cardConfig">卡牌静态配置。</param>
        /// <param name="caster">施法者。</param>
        /// <param name="targets">候选目标列表（敌方候选，或 SingleManual 模式下的单一指定目标）。</param>
        /// <param name="events">事件接收器，用于发布怪物死亡等事件。</param>
        public static void Execute(
            Card cardConfig,
            IBattleActor caster,
            IList<IBattleActor> targets,
            IBattleEventSink events)
        {
            if (cardConfig == null) throw new ArgumentNullException(nameof(cardConfig));
            if (caster == null) throw new ArgumentNullException(nameof(caster));

            var effects = ResolveEffects(cardConfig);
            Execute(cardConfig, effects, caster, targets, events);
        }

        /// <summary>
        /// 测试友好的执行重载：调用方直接传入 effect 列表，避免依赖 GameLogicEntry。
        /// </summary>
        public static void Execute(
            Card cardConfig,
            IList<CardEffect> effects,
            IBattleActor caster,
            IList<IBattleActor> targets,
            IBattleEventSink events,
            bool splitDamageAcrossTargets = true)
        {
            if (cardConfig == null) throw new ArgumentNullException(nameof(cardConfig));
            if (caster == null) throw new ArgumentNullException(nameof(caster));
            if (effects == null || effects.Count == 0) return;

            var finalTargets = ResolveTargets(cardConfig.TargetMode, caster, targets);
            if (finalTargets == null || finalTargets.Count == 0) return;

            int targetCount = finalTargets.Count;
            foreach (var effect in effects)
            {
                ApplyEffect(effect, caster, finalTargets, cardConfig.TargetMode, targetCount, events, splitDamageAcrossTargets);
            }
        }

        /// <summary>
        /// 从 GameLogicEntry.Config.Tables 中筛选出该卡牌挂载的 effect 行。
        /// </summary>
        private static IList<CardEffect> ResolveEffects(Card cardConfig)
        {
            var tables = GameLogicEntry.Config?.Tables;
            if (tables == null) return Array.Empty<CardEffect>();

            var result = new List<CardEffect>();
            foreach (var effect in tables.TbCardEffect.DataList)
            {
                if (effect.CardId == cardConfig.Id)
                {
                    result.Add(effect);
                }
            }
            return result;
        }

        /// <summary>
        /// 根据 TargetMode 解析最终目标列表。
        /// </summary>
        private static IList<IBattleActor> ResolveTargets(
            TargetMode mode,
            IBattleActor caster,
            IList<IBattleActor> rawTargets)
        {
            switch (mode)
            {
                case TargetMode.Self:
                    return new List<IBattleActor> { caster };

                case TargetMode.SingleAuto:
                {
                    if (rawTargets == null) return Array.Empty<IBattleActor>();
                    foreach (var t in rawTargets)
                    {
                        if (t != null && !t.IsDead) return new List<IBattleActor> { t };
                    }
                    return Array.Empty<IBattleActor>();
                }

                case TargetMode.SingleManual:
                {
                    if (rawTargets == null || rawTargets.Count == 0) return Array.Empty<IBattleActor>();
                    var first = rawTargets[0];
                    if (first == null) return Array.Empty<IBattleActor>();
                    return new List<IBattleActor> { first };
                }

                case TargetMode.All:
                case TargetMode.SplitAcrossAll:
                {
                    if (rawTargets == null) return Array.Empty<IBattleActor>();
                    var alive = new List<IBattleActor>();
                    foreach (var t in rawTargets)
                    {
                        if (t != null && !t.IsDead) alive.Add(t);
                    }
                    return alive;
                }

                default:
                    return Array.Empty<IBattleActor>();
            }
        }

        /// <summary>
        /// 对一条 Effect 在所有最终目标上应用。
        /// </summary>
        private static void ApplyEffect(
            CardEffect effect,
            IBattleActor caster,
            IList<IBattleActor> finalTargets,
            TargetMode mode,
            int targetCount,
            IBattleEventSink events,
            bool splitDamageAcrossTargets)
        {
            switch (effect.Kind)
            {
                case EffectKind.Damage:
                {
                    int dmg = effect.Value;
                    if (splitDamageAcrossTargets && mode == TargetMode.SplitAcrossAll && targetCount > 0)
                    {
                        dmg = Math.Max(1, effect.Value / targetCount);
                    }
                    foreach (var target in finalTargets)
                    {
                        if (target == null || target.IsDead) continue;
                        target.TakeDamage(dmg);
                        if (target.IsDead && events != null)
                        {
                            events.OnActorDied(target);
                        }
                    }
                    break;
                }

                case EffectKind.Shield:
                {
                    foreach (var target in finalTargets)
                    {
                        if (target == null) continue;
                        target.AddArmor(effect.Value);
                    }
                    break;
                }

                case EffectKind.DamageDot:
                {
                    foreach (var target in finalTargets)
                    {
                        if (target == null || target.IsDead) continue;
                        target.AddBuff(new BuffRuntime
                        {
                            Kind = EffectKind.DamageDot,
                            TriggerTiming = NormalizeTriggerTiming(effect.TriggerTiming),
                            Value = effect.Value,
                            RemainingTurns = effect.Duration,
                            SourceActor = caster,
                        });
                    }
                    break;
                }

                case EffectKind.EnergyGain:
                {
                    caster.GainEnergy(effect.Value);
                    break;
                }
            }
        }

        /// <summary>
        /// 兼容测试反射构造或旧数据未写入 TriggerTiming 的情况，默认按 EnemyTurnStart 的 DoT 语义处理。
        /// </summary>
        private static EffectTriggerTiming NormalizeTriggerTiming(EffectTriggerTiming timing)
        {
            return timing == 0 ? EffectTriggerTiming.EnemyTurnStart : timing;
        }
    }
}
