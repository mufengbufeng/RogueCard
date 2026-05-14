using System;
using System.Collections.Generic;
using GameConfig.card;

namespace GameLogic
{
    /// <summary>
    /// 卡牌释放随机源接口，便于测试中提供确定性随机。
    /// </summary>
    public interface ICardReleaseRandom
    {
        /// <summary>
        /// 返回 [0, maxExclusive) 范围内的随机整数。
        /// </summary>
        int Next(int maxExclusive);
    }

    /// <summary>
    /// 默认卡牌释放随机源，包装 System.Random。
    /// </summary>
    public sealed class DefaultCardReleaseRandom : ICardReleaseRandom
    {
        private readonly Random _random = new();

        /// <inheritdoc />
        public int Next(int maxExclusive) => _random.Next(maxExclusive);
    }

    /// <summary>
    /// 卡牌释放调度服务。负责按 CardReleaseKind 解析目标、按 TriggerTiming 拆分即时与延迟效果。
    /// </summary>
    public class CardReleaseResolver
    {
        private readonly Func<Card, IList<CardEffect>> _effectResolver;
        private readonly ICardReleaseRandom _random;
        private readonly List<DelayedCardEffect> _delayedEffects = new();

        /// <summary>
        /// 创建卡牌释放调度服务。
        /// </summary>
        public CardReleaseResolver(
            Func<Card, IList<CardEffect>> effectResolver = null,
            ICardReleaseRandom random = null)
        {
            _effectResolver = effectResolver ?? ResolveEffectsFromConfig;
            _random = random ?? new DefaultCardReleaseRandom();
        }

        /// <summary>
        /// 当前待结算延迟效果数量。
        /// </summary>
        public int PendingCount => _delayedEffects.Count;

        /// <summary>
        /// 判断怪物是否具有攻击意图。
        /// </summary>
        public bool HasAttackIntent(MonsterRuntime monster)
        {
            if (monster == null || monster.IsDead || monster.PendingCards == null) return false;

            foreach (var card in monster.PendingCards)
            {
                var effects = ResolveEffects(card);
                foreach (var effect in effects)
                {
                    if (effect == null) continue;
                    if (effect.Kind == EffectKind.Damage || effect.Kind == EffectKind.DamageDot)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 按卡牌释放策略解析候选目标列表。
        /// </summary>
        public IList<IBattleActor> ResolveTargets(
            Card card,
            IReadOnlyList<MonsterRuntime> monsters,
            int manualMonsterIndex)
        {
            if (card == null) return Array.Empty<IBattleActor>();

            if (card.TargetMode == TargetMode.Self)
            {
                return Array.Empty<IBattleActor>();
            }

            if (card.TargetMode == TargetMode.SingleManual && card.CardReleaseKind != CardReleaseKind.Spell)
            {
                return ResolveManualTarget(monsters, manualMonsterIndex);
            }

            var alive = CollectAliveMonsters(monsters);
            if (alive.Count == 0) return Array.Empty<IBattleActor>();

            return card.CardReleaseKind switch
            {
                CardReleaseKind.Melee => ResolveMeleeTargets(alive),
                CardReleaseKind.Projectile => ResolveProjectileTargets(card, alive),
                CardReleaseKind.Spell when card.TargetMode == TargetMode.SingleManual => ResolveMeleeTargets(alive),
                _ => ResolveDefaultTargets(card, alive),
            };
        }

        /// <summary>
        /// 释放一张卡牌：立即效果同步执行，延迟效果登记等待对应结算点。
        /// </summary>
        public void Release(
            Card card,
            IBattleActor caster,
            IReadOnlyList<MonsterRuntime> monsters,
            int manualMonsterIndex,
            IBattleEventSink events)
        {
            if (card == null) throw new ArgumentNullException(nameof(card));
            if (caster == null) throw new ArgumentNullException(nameof(caster));

            var effects = ResolveEffects(card);
            if (effects.Count == 0) return;

            var targets = ResolveTargets(card, monsters, manualMonsterIndex);
            if (card.TargetMode != TargetMode.Self && (targets == null || targets.Count == 0)) return;

            var immediate = new List<CardEffect>();
            foreach (var effect in effects)
            {
                if (effect == null) continue;
                var timing = NormalizeTriggerTiming(effect.TriggerTiming);
                if (timing == EffectTriggerTiming.Immediate)
                {
                    immediate.Add(effect);
                }
                else
                {
                    RegisterDelayedEffect(card, effect, caster, targets);
                }
            }

            if (immediate.Count > 0)
            {
                CardEffectExecutor.Execute(
                    card,
                    immediate,
                    caster,
                    targets,
                    events,
                    splitDamageAcrossTargets: card.CardReleaseKind != CardReleaseKind.Projectile);
            }
        }

        /// <summary>
        /// 结算指定触发时机的延迟效果。
        /// </summary>
        public void ResolveDelayedEffects(EffectTriggerTiming timing, IBattleEventSink events)
        {
            for (int i = _delayedEffects.Count - 1; i >= 0; i--)
            {
                var pending = _delayedEffects[i];
                if (pending.Effect == null || NormalizeTriggerTiming(pending.Effect.TriggerTiming) != timing) continue;

                CardEffectExecutor.Execute(
                    pending.Card,
                    new List<CardEffect> { pending.Effect },
                    pending.Caster,
                    pending.Targets,
                    events);

                _delayedEffects.RemoveAt(i);
            }
        }

        /// <summary>
        /// 清空全部延迟效果。
        /// </summary>
        public void Clear() => _delayedEffects.Clear();

        private IList<CardEffect> ResolveEffects(Card card)
        {
            return card == null
                ? Array.Empty<CardEffect>()
                : _effectResolver(card) ?? Array.Empty<CardEffect>();
        }

        private static IList<CardEffect> ResolveEffectsFromConfig(Card card)
        {
            var tables = GameLogicEntry.Config?.Tables;
            if (tables == null || card == null) return Array.Empty<CardEffect>();

            var result = new List<CardEffect>();
            foreach (var effect in tables.TbCardEffect.DataList)
            {
                if (effect.CardId == card.Id) result.Add(effect);
            }

            return result;
        }

        private static IList<IBattleActor> ResolveManualTarget(
            IReadOnlyList<MonsterRuntime> monsters,
            int manualMonsterIndex)
        {
            if (monsters == null) return Array.Empty<IBattleActor>();
            if (manualMonsterIndex < 0 || manualMonsterIndex >= monsters.Count)
            {
                foreach (var candidate in monsters)
                {
                    if (candidate != null && !candidate.IsDead)
                    {
                        return new List<IBattleActor> { candidate };
                    }
                }

                return Array.Empty<IBattleActor>();
            }

            var monster = monsters[manualMonsterIndex];
            if (monster == null || monster.IsDead) return Array.Empty<IBattleActor>();
            return new List<IBattleActor> { monster };
        }

        private static List<MonsterRuntime> CollectAliveMonsters(IReadOnlyList<MonsterRuntime> monsters)
        {
            var alive = new List<MonsterRuntime>();
            if (monsters == null) return alive;

            foreach (var monster in monsters)
            {
                if (monster != null && !monster.IsDead) alive.Add(monster);
            }

            return alive;
        }

        private IList<IBattleActor> ResolveMeleeTargets(List<MonsterRuntime> alive)
        {
            foreach (var monster in alive)
            {
                if (HasAttackIntent(monster)) return new List<IBattleActor> { monster };
            }

            return alive.Count > 0
                ? new List<IBattleActor> { alive[0] }
                : Array.Empty<IBattleActor>();
        }

        private IList<IBattleActor> ResolveProjectileTargets(Card card, List<MonsterRuntime> alive)
        {
            var ordered = OrderByAttackIntent(alive);
            if (card.TargetCount <= 0)
            {
                var allTargets = new List<IBattleActor>();
                foreach (var monster in ordered)
                {
                    allTargets.Add(monster);
                }
                return allTargets;
            }

            int targetCount = Math.Min(card.TargetCount, ordered.Count);

            var result = new List<IBattleActor>();
            var fillers = new List<MonsterRuntime>();
            foreach (var monster in ordered)
            {
                if (HasAttackIntent(monster))
                {
                    if (result.Count < targetCount) result.Add(monster);
                }
                else
                {
                    fillers.Add(monster);
                }
            }

            while (result.Count < targetCount && fillers.Count > 0)
            {
                int index = _random.Next(fillers.Count);
                result.Add(fillers[index]);
                fillers.RemoveAt(index);
            }

            return result;
        }

        private IList<IBattleActor> ResolveDefaultTargets(Card card, List<MonsterRuntime> alive)
        {
            if (card.TargetMode == TargetMode.SingleAuto)
            {
                return alive.Count > 0
                    ? new List<IBattleActor> { alive[0] }
                    : Array.Empty<IBattleActor>();
            }

            var targets = new List<IBattleActor>();
            foreach (var monster in alive)
            {
                targets.Add(monster);
            }
            return targets;
        }

        private List<MonsterRuntime> OrderByAttackIntent(List<MonsterRuntime> alive)
        {
            var result = new List<MonsterRuntime>();
            foreach (var monster in alive)
            {
                if (HasAttackIntent(monster)) result.Add(monster);
            }

            foreach (var monster in alive)
            {
                if (!HasAttackIntent(monster)) result.Add(monster);
            }

            return result;
        }

        private void RegisterDelayedEffect(
            Card card,
            CardEffect effect,
            IBattleActor caster,
            IList<IBattleActor> targets)
        {
            if (effect.Kind == EffectKind.DamageDot)
            {
                var buffTargets = card.TargetMode == TargetMode.Self
                    ? new List<IBattleActor> { caster }
                    : targets;

                foreach (var target in buffTargets)
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
                return;
            }

            _delayedEffects.Add(new DelayedCardEffect(card, effect, caster, targets));
        }

        /// <summary>
        /// 兼容旧测试构造或旧配置缺省值：未设置 TriggerTiming 时按 Immediate 释放处理。
        /// </summary>
        private static EffectTriggerTiming NormalizeTriggerTiming(EffectTriggerTiming timing)
        {
            return timing == 0 ? EffectTriggerTiming.Immediate : timing;
        }

        private readonly struct DelayedCardEffect
        {
            public readonly Card Card;
            public readonly CardEffect Effect;
            public readonly IBattleActor Caster;
            public readonly IList<IBattleActor> Targets;

            public DelayedCardEffect(
                Card card,
                CardEffect effect,
                IBattleActor caster,
                IList<IBattleActor> targets)
            {
                Card = card;
                Effect = effect;
                Caster = caster;
                Targets = new List<IBattleActor>(targets);
            }
        }
    }
}
