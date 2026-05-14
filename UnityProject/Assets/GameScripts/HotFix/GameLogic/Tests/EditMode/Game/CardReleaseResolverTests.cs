using System;
using System.Collections.Generic;
using System.Reflection;
using EF.Event;
using EF.UI;
using GameConfig.card;
using NUnit.Framework;

namespace GameLogic.Tests.EditMode.Game
{
    /// <summary>
    /// 验证卡牌释放调度层：CardReleaseKind 目标优先级、投射补足与法术触发时机。
    /// </summary>
    [TestFixture]
    public class CardReleaseResolverTests
    {
        private class FakeActor : IBattleActor
        {
            public int Hp { get; set; } = 100;
            public int MaxHp { get; set; } = 100;
            public int Armor { get; set; }
            public int CurrentEnergy { get; set; }
            public int MaxEnergy { get; set; } = 3;
            public IList<BuffRuntime> Buffs { get; } = new List<BuffRuntime>();
            public bool IsDead => Hp <= 0;

            public void TakeDamage(int amount)
            {
                if (amount <= 0) return;
                Hp = Math.Max(0, Hp - amount);
            }

            public void AddArmor(int amount) => Armor = Math.Max(0, Armor + amount);
            public void GainEnergy(int amount) => CurrentEnergy = Math.Max(0, CurrentEnergy + amount);
            public void AddBuff(BuffRuntime buff) { if (buff != null) Buffs.Add(buff); }
        }

        private class CapturingSink : IBattleEventSink
        {
            public readonly List<IBattleActor> Died = new();
            public void OnActorDied(IBattleActor actor) => Died.Add(actor);
        }

        private sealed class FixedRandom : ICardReleaseRandom
        {
            private readonly Queue<int> _values = new();

            public FixedRandom(params int[] values)
            {
                foreach (var value in values) _values.Enqueue(value);
            }

            public int Next(int maxExclusive)
            {
                if (_values.Count == 0) return 0;
                int value = _values.Dequeue();
                return Math.Max(0, Math.Min(maxExclusive - 1, value));
            }
        }

        [Test]
        public void HasAttackIntent_Damage或DamageDot算攻击意图()
        {
            var damageCard = NewCard(2001, CardReleaseKind.Melee, TargetMode.SingleAuto);
            var dotCard = NewCard(2002, CardReleaseKind.Spell, TargetMode.SingleAuto);
            var shieldCard = NewCard(2003, CardReleaseKind.Spell, TargetMode.Self);
            var resolver = NewResolver(new Dictionary<int, List<CardEffect>>
            {
                [damageCard.Id] = new() { NewEffect(1, damageCard.Id, EffectKind.Damage, EffectTriggerTiming.Immediate, 3) },
                [dotCard.Id] = new() { NewEffect(2, dotCard.Id, EffectKind.DamageDot, EffectTriggerTiming.EnemyTurnStart, 2, 3) },
                [shieldCard.Id] = new() { NewEffect(3, shieldCard.Id, EffectKind.Shield, EffectTriggerTiming.Immediate, 5) },
            });

            var damageMonster = NewMonster(10, damageCard);
            var dotMonster = NewMonster(10, dotCard);
            var shieldMonster = NewMonster(10, shieldCard);

            Assert.IsTrue(resolver.HasAttackIntent(damageMonster));
            Assert.IsTrue(resolver.HasAttackIntent(dotMonster));
            Assert.IsFalse(resolver.HasAttackIntent(shieldMonster));
        }

        [Test]
        public void ResolveTargets_Melee_优先选择攻击意图敌人()
        {
            var intentCard = NewCard(3001, CardReleaseKind.Melee, TargetMode.SingleAuto);
            var melee = NewCard(1001, CardReleaseKind.Melee, TargetMode.SingleAuto, targetCount: 1);
            var resolver = NewResolver(new Dictionary<int, List<CardEffect>>
            {
                [intentCard.Id] = new() { NewEffect(1, intentCard.Id, EffectKind.Damage, EffectTriggerTiming.Immediate, 3) },
                [melee.Id] = new() { NewEffect(2, melee.Id, EffectKind.Damage, EffectTriggerTiming.Immediate, 6) },
            });

            var noIntent = NewMonster(20);
            var attackIntent = NewMonster(20, intentCard);
            var targets = resolver.ResolveTargets(melee, new List<MonsterRuntime> { noIntent, attackIntent }, -1);

            Assert.AreEqual(1, targets.Count);
            Assert.AreSame(attackIntent, targets[0]);
        }

        [Test]
        public void ResolveTargets_Melee_无攻击意图时回退第一个存活敌人()
        {
            var melee = NewCard(1001, CardReleaseKind.Melee, TargetMode.SingleAuto, targetCount: 1);
            var resolver = NewResolver(new Dictionary<int, List<CardEffect>>());
            var first = NewMonster(20);
            var second = NewMonster(20);

            var targets = resolver.ResolveTargets(melee, new List<MonsterRuntime> { first, second }, -1);

            Assert.AreEqual(1, targets.Count);
            Assert.AreSame(first, targets[0]);
        }

        [Test]
        public void ResolveTargets_Projectile_攻击意图足够时只选择攻击意图敌人()
        {
            var intentCard = NewCard(3001, CardReleaseKind.Melee, TargetMode.SingleAuto);
            var projectile = NewCard(1002, CardReleaseKind.Projectile, TargetMode.SplitAcrossAll, targetCount: 2);
            var resolver = NewResolver(new Dictionary<int, List<CardEffect>>
            {
                [intentCard.Id] = new() { NewEffect(1, intentCard.Id, EffectKind.Damage, EffectTriggerTiming.Immediate, 3) },
            });

            var intentA = NewMonster(20, intentCard);
            var noIntent = NewMonster(20);
            var intentB = NewMonster(20, intentCard);
            var targets = resolver.ResolveTargets(projectile, new List<MonsterRuntime> { intentA, noIntent, intentB }, -1);

            Assert.AreEqual(2, targets.Count);
            Assert.AreSame(intentA, targets[0]);
            Assert.AreSame(intentB, targets[1]);
            CollectionAssert.DoesNotContain(targets, noIntent);
        }

        [Test]
        public void ResolveTargets_Projectile_攻击意图不足时随机补足其他存活敌人()
        {
            var intentCard = NewCard(3001, CardReleaseKind.Melee, TargetMode.SingleAuto);
            var projectile = NewCard(1002, CardReleaseKind.Projectile, TargetMode.SplitAcrossAll, targetCount: 3);
            var resolver = NewResolver(new Dictionary<int, List<CardEffect>>
            {
                [intentCard.Id] = new() { NewEffect(1, intentCard.Id, EffectKind.Damage, EffectTriggerTiming.Immediate, 3) },
            }, new FixedRandom(1, 0));

            var attackIntent = NewMonster(20, intentCard);
            var fillerA = NewMonster(20);
            var fillerB = NewMonster(20);
            var dead = NewMonster(0, intentCard);
            var targets = resolver.ResolveTargets(projectile, new List<MonsterRuntime> { attackIntent, fillerA, dead, fillerB }, -1);

            Assert.AreEqual(3, targets.Count);
            Assert.AreSame(attackIntent, targets[0]);
            Assert.AreSame(fillerB, targets[1]);
            Assert.AreSame(fillerA, targets[2]);
            CollectionAssert.DoesNotContain(targets, dead);
        }

        [Test]
        public void ResolveTargets_Projectile_TargetCount非正数命中所有存活并保持攻击意图在前()
        {
            var intentCard = NewCard(3001, CardReleaseKind.Melee, TargetMode.SingleAuto);
            var projectile = NewCard(1002, CardReleaseKind.Projectile, TargetMode.SplitAcrossAll, targetCount: 0);
            var resolver = NewResolver(new Dictionary<int, List<CardEffect>>
            {
                [intentCard.Id] = new() { NewEffect(1, intentCard.Id, EffectKind.Damage, EffectTriggerTiming.Immediate, 3) },
            });

            var firstNoIntent = NewMonster(20);
            var attackIntent = NewMonster(20, intentCard);
            var secondNoIntent = NewMonster(20);
            var targets = resolver.ResolveTargets(projectile, new List<MonsterRuntime> { firstNoIntent, attackIntent, secondNoIntent }, -1);

            Assert.AreEqual(3, targets.Count);
            Assert.AreSame(attackIntent, targets[0]);
            Assert.AreSame(firstNoIntent, targets[1]);
            Assert.AreSame(secondNoIntent, targets[2]);
        }

        [Test]
        public void Release_Spell_立即效果执行_Dot按触发时机登记为Buff()
        {
            var spell = NewCard(1003, CardReleaseKind.Spell, TargetMode.SingleManual, targetCount: 1);
            var immediate = NewEffect(1, spell.Id, EffectKind.Damage, EffectTriggerTiming.Immediate, 8);
            var dot = NewEffect(2, spell.Id, EffectKind.DamageDot, EffectTriggerTiming.EnemyTurnStart, 2, 3);
            var resolver = NewResolver(new Dictionary<int, List<CardEffect>>
            {
                [spell.Id] = new() { immediate, dot },
            });
            var caster = new FakeActor();
            var target = NewMonster(30);

            resolver.Release(spell, caster, new List<MonsterRuntime> { target }, 0, new CapturingSink());

            Assert.AreEqual(22, target.Hp);
            Assert.AreEqual(1, target.Buffs.Count);
            Assert.AreEqual(EffectKind.DamageDot, target.Buffs[0].Kind);
            Assert.AreEqual(EffectTriggerTiming.EnemyTurnStart, target.Buffs[0].TriggerTiming);
            Assert.AreEqual(3, target.Buffs[0].RemainingTurns);
        }

        [Test]
        public void Release_SpellSingleManual_未传手动目标时自动选择攻击意图敌人()
        {
            var intentCard = NewCard(3001, CardReleaseKind.Melee, TargetMode.SingleAuto);
            var spell = NewCard(1003, CardReleaseKind.Spell, TargetMode.SingleManual, targetCount: 1);
            var resolver = NewResolver(new Dictionary<int, List<CardEffect>>
            {
                [intentCard.Id] = new() { NewEffect(1, intentCard.Id, EffectKind.Damage, EffectTriggerTiming.Immediate, 1) },
                [spell.Id] = new() { NewEffect(2, spell.Id, EffectKind.Damage, EffectTriggerTiming.Immediate, 8) },
            });
            var caster = new FakeActor();
            var noIntent = NewMonster(30);
            var attackIntent = NewMonster(30, intentCard);

            resolver.Release(spell, caster, new List<MonsterRuntime> { noIntent, attackIntent }, -1, new CapturingSink());

            Assert.AreEqual(30, noIntent.Hp);
            Assert.AreEqual(22, attackIntent.Hp);
        }

        [Test]
        public void Release_Spell_EnemyTurnEnd效果等待对应结算点()
        {
            var spell = NewCard(1008, CardReleaseKind.Spell, TargetMode.SingleManual, targetCount: 1);
            var delayedDamage = NewEffect(1, spell.Id, EffectKind.Damage, EffectTriggerTiming.EnemyTurnEnd, 5);
            var resolver = NewResolver(new Dictionary<int, List<CardEffect>>
            {
                [spell.Id] = new() { delayedDamage },
            });
            var caster = new FakeActor();
            var target = NewMonster(30);

            resolver.Release(spell, caster, new List<MonsterRuntime> { target }, 0, new CapturingSink());

            Assert.AreEqual(30, target.Hp);
            Assert.AreEqual(1, resolver.PendingCount);

            resolver.ResolveDelayedEffects(EffectTriggerTiming.EnemyTurnStart, new CapturingSink());
            Assert.AreEqual(30, target.Hp);
            Assert.AreEqual(1, resolver.PendingCount);

            resolver.ResolveDelayedEffects(EffectTriggerTiming.EnemyTurnEnd, new CapturingSink());
            Assert.AreEqual(25, target.Hp);
            Assert.AreEqual(0, resolver.PendingCount);
        }

        [Test]
        public void Release_Projectile_完整释放链按TargetCount对每个目标造成完整伤害()
        {
            var intentCard = NewCard(3001, CardReleaseKind.Melee, TargetMode.SingleAuto);
            var projectile = NewCard(1002, CardReleaseKind.Projectile, TargetMode.SplitAcrossAll, targetCount: 2);
            var resolver = NewResolver(new Dictionary<int, List<CardEffect>>
            {
                [intentCard.Id] = new() { NewEffect(1, intentCard.Id, EffectKind.Damage, EffectTriggerTiming.Immediate, 1) },
                [projectile.Id] = new() { NewEffect(2, projectile.Id, EffectKind.Damage, EffectTriggerTiming.Immediate, 6) },
            });
            var caster = new FakeActor();
            var intentA = NewMonster(20, intentCard);
            var intentB = NewMonster(20, intentCard);
            var noIntent = NewMonster(20);

            resolver.Release(projectile, caster, new List<MonsterRuntime> { intentA, noIntent, intentB }, -1, new CapturingSink());

            Assert.AreEqual(14, intentA.Hp);
            Assert.AreEqual(14, intentB.Hp);
            Assert.AreEqual(20, noIntent.Hp);
        }

        [Test]
        public void CardSystem_Play_伤害怪物后发布Monsters变更供运行时UI刷新()
        {
            var damageCard = NewCard(1001, CardReleaseKind.Melee, TargetMode.SingleAuto, targetCount: 1);
            var resolver = NewResolver(new Dictionary<int, List<CardEffect>>
            {
                [damageCard.Id] = new() { NewEffect(1, damageCard.Id, EffectKind.Damage, EffectTriggerTiming.Immediate, 6) },
            });
            var model = new GameModel();
            model.InitBattleAttributes(maxEnergy: 3, handLimit: 5, maxHp: 30);
            model.SetPhase(BattlePhase.PlayerTurn);
            model.SetHand(new List<CardRuntime> { new CardRuntime { Config = damageCard } });
            var target = NewMonster(20);
            model.SetMonsters(new List<MonsterRuntime> { target });
            var events = new LocalEventBus();
            var cardSystem = new CardSystem(resolver);
            cardSystem.Init(model, events);
            var viewModel = new GameViewModel();
            viewModel.BindModel(model);
            int monsterNotifications = 0;
            int viewModelMonsterChanges = 0;
            int observedTargetHp = -1;
            model.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(GameModel.Monsters)) monsterNotifications++;
            };
            viewModel.Monsters.Changed += monsters =>
            {
                viewModelMonsterChanges++;
                observedTargetHp = monsters[0].Hp;
            };

            try
            {
                Assert.IsTrue(cardSystem.Play(0));

                Assert.AreEqual(14, target.Hp);
                Assert.GreaterOrEqual(monsterNotifications, 1);
                Assert.GreaterOrEqual(viewModelMonsterChanges, 1);
                Assert.AreEqual(14, observedTargetHp);
            }
            finally
            {
                viewModel.Dispose();
                cardSystem.Dispose();
                events.Dispose();
            }
        }

        private static CardReleaseResolver NewResolver(
            Dictionary<int, List<CardEffect>> effects,
            ICardReleaseRandom random = null)
        {
            return new CardReleaseResolver(card =>
                card != null && effects.TryGetValue(card.Id, out var list)
                    ? list
                    : new List<CardEffect>(), random);
        }

        private static MonsterRuntime NewMonster(int hp, params Card[] pending)
        {
            var monster = new MonsterRuntime { Hp = hp, MaxHp = 30 };
            foreach (var card in pending)
            {
                if (card != null) monster.PendingCards.Add(card);
            }
            return monster;
        }

        private static Card NewCard(
            int id,
            CardReleaseKind releaseKind,
            TargetMode mode,
            int targetCount = 1,
            OwnerKind owner = OwnerKind.Player,
            int cost = 1)
        {
            var card = (Card)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Card));
            SetReadonly(card, "Id", id);
            SetReadonly(card, "Name", $"Card_{id}");
            SetReadonly(card, "Desc", string.Empty);
            SetReadonly(card, "Cost", cost);
            SetReadonly(card, "OwnerKind", owner);
            SetReadonly(card, "CardReleaseKind", releaseKind);
            SetReadonly(card, "TargetMode", mode);
            SetReadonly(card, "TargetCount", targetCount);
            SetReadonly(card, "IsBasic", true);
            SetReadonly(card, "AssetId", string.Empty);
            return card;
        }

        private static CardEffect NewEffect(
            int id,
            int cardId,
            EffectKind kind,
            EffectTriggerTiming timing,
            int value,
            int duration = 0)
        {
            var eff = (CardEffect)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(CardEffect));
            SetReadonly(eff, "Id", id);
            SetReadonly(eff, "CardId", cardId);
            SetReadonly(eff, "Kind", kind);
            SetReadonly(eff, "TriggerTiming", timing);
            SetReadonly(eff, "Value", value);
            SetReadonly(eff, "Duration", duration);
            SetReadonly(eff, "Comment", string.Empty);
            return eff;
        }

        private static void SetReadonly(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field, $"未找到字段 {fieldName}");
            field.SetValue(target, value);
        }
    }
}
