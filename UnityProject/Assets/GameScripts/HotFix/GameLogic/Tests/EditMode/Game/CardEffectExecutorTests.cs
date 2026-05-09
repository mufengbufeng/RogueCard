using System;
using System.Collections.Generic;
using System.Reflection;
using GameConfig.card;
using NUnit.Framework;

namespace GameLogic.Tests.EditMode.Game
{
    /// <summary>
    /// 验证 CardEffectExecutor 在 5 种 TargetMode × 4 种 EffectKind 上的行为契约。
    /// 测试不依赖 GameLogicEntry/Tables，使用反射构造 Card 与 CardEffect 配置实例并直接传入 Executor。
    /// </summary>
    [TestFixture]
    public class CardEffectExecutorTests
    {
        // ── 测试 IBattleActor 实现 ──

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
                int remaining = amount;
                if (Armor > 0)
                {
                    int absorbed = Math.Min(Armor, remaining);
                    Armor -= absorbed;
                    remaining -= absorbed;
                }
                if (remaining > 0) Hp = Math.Max(0, Hp - remaining);
            }

            public void AddArmor(int amount) => Armor = Math.Max(0, Armor + amount);
            public void GainEnergy(int amount) => CurrentEnergy = Math.Max(0, CurrentEnergy + amount);
            public void AddBuff(BuffRuntime buff) { if (buff != null) Buffs.Add(buff); }
        }

        private class CapturingSink : IBattleEventSink
        {
            public List<IBattleActor> Died { get; } = new();
            public void OnActorDied(IBattleActor actor) => Died.Add(actor);
        }

        // ── 反射构造 Card / CardEffect ──

        private static Card NewCard(int id, TargetMode mode, OwnerKind owner = OwnerKind.Player, int cost = 1)
        {
            var card = (Card)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Card));
            SetReadonly(card, "Id", id);
            SetReadonly(card, "Name", $"Card_{id}");
            SetReadonly(card, "Desc", string.Empty);
            SetReadonly(card, "Cost", cost);
            SetReadonly(card, "OwnerKind", owner);
            SetReadonly(card, "TargetMode", mode);
            SetReadonly(card, "IsBasic", true);
            SetReadonly(card, "AssetId", string.Empty);
            return card;
        }

        private static CardEffect NewEffect(int id, int cardId, EffectKind kind, int value, int duration = 0)
        {
            var eff = (CardEffect)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(CardEffect));
            SetReadonly(eff, "Id", id);
            SetReadonly(eff, "CardId", cardId);
            SetReadonly(eff, "Kind", kind);
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

        // ── Damage 测试 ──

        [Test]
        public void Damage_SingleAuto_命中第一个存活敌方()
        {
            var card = NewCard(1, TargetMode.SingleAuto);
            var effects = new List<CardEffect> { NewEffect(1, 1, EffectKind.Damage, 6) };
            var caster = new FakeActor();
            var t1 = new FakeActor { Hp = 0 }; // 已死
            var t2 = new FakeActor { Hp = 20 };
            var t3 = new FakeActor { Hp = 20 };

            CardEffectExecutor.Execute(card, effects, caster, new List<IBattleActor> { t1, t2, t3 }, new CapturingSink());

            Assert.AreEqual(0, t1.Hp);
            Assert.AreEqual(14, t2.Hp);
            Assert.AreEqual(20, t3.Hp);
        }

        [Test]
        public void Damage_先扣盾再扣血()
        {
            var card = NewCard(1, TargetMode.SingleAuto);
            var effects = new List<CardEffect> { NewEffect(1, 1, EffectKind.Damage, 10) };
            var caster = new FakeActor();
            var target = new FakeActor { Hp = 20, Armor = 4 };

            CardEffectExecutor.Execute(card, effects, caster, new List<IBattleActor> { target }, new CapturingSink());

            Assert.AreEqual(0, target.Armor);
            Assert.AreEqual(14, target.Hp);
        }

        [Test]
        public void Damage_命中致死时通过Sink发布死亡()
        {
            var card = NewCard(1, TargetMode.SingleAuto);
            var effects = new List<CardEffect> { NewEffect(1, 1, EffectKind.Damage, 50) };
            var caster = new FakeActor();
            var target = new FakeActor { Hp = 10 };
            var sink = new CapturingSink();

            CardEffectExecutor.Execute(card, effects, caster, new List<IBattleActor> { target }, sink);

            Assert.IsTrue(target.IsDead);
            Assert.AreEqual(1, sink.Died.Count);
            Assert.AreSame(target, sink.Died[0]);
        }

        [Test]
        public void Damage_All_打中所有存活敌方()
        {
            var card = NewCard(1, TargetMode.All);
            var effects = new List<CardEffect> { NewEffect(1, 1, EffectKind.Damage, 5) };
            var caster = new FakeActor();
            var alive1 = new FakeActor { Hp = 20 };
            var dead = new FakeActor { Hp = 0 };
            var alive2 = new FakeActor { Hp = 20 };

            CardEffectExecutor.Execute(card, effects, caster, new List<IBattleActor> { alive1, dead, alive2 }, new CapturingSink());

            Assert.AreEqual(15, alive1.Hp);
            Assert.AreEqual(0, dead.Hp);
            Assert.AreEqual(15, alive2.Hp);
        }

        [Test]
        public void Damage_SplitAcrossAll_两目标各分一半()
        {
            var card = NewCard(1, TargetMode.SplitAcrossAll);
            var effects = new List<CardEffect> { NewEffect(1, 1, EffectKind.Damage, 6) };
            var caster = new FakeActor();
            var t1 = new FakeActor { Hp = 20 };
            var t2 = new FakeActor { Hp = 20 };

            CardEffectExecutor.Execute(card, effects, caster, new List<IBattleActor> { t1, t2 }, new CapturingSink());

            Assert.AreEqual(17, t1.Hp); // 6/2 = 3
            Assert.AreEqual(17, t2.Hp);
        }

        [Test]
        public void Damage_SplitAcrossAll_单目标拿全额()
        {
            var card = NewCard(1, TargetMode.SplitAcrossAll);
            var effects = new List<CardEffect> { NewEffect(1, 1, EffectKind.Damage, 6) };
            var caster = new FakeActor();
            var only = new FakeActor { Hp = 20 };

            CardEffectExecutor.Execute(card, effects, caster, new List<IBattleActor> { only }, new CapturingSink());

            Assert.AreEqual(14, only.Hp); // 6/1 = 6
        }

        [Test]
        public void Damage_SplitAcrossAll_四目标向下取整最少为一()
        {
            var card = NewCard(1, TargetMode.SplitAcrossAll);
            var effects = new List<CardEffect> { NewEffect(1, 1, EffectKind.Damage, 3) };
            var caster = new FakeActor();
            var t1 = new FakeActor { Hp = 20 };
            var t2 = new FakeActor { Hp = 20 };
            var t3 = new FakeActor { Hp = 20 };
            var t4 = new FakeActor { Hp = 20 };

            CardEffectExecutor.Execute(card, effects, caster, new List<IBattleActor> { t1, t2, t3, t4 }, new CapturingSink());

            // 3/4=0 → Math.Max(1, 0) = 1
            Assert.AreEqual(19, t1.Hp);
            Assert.AreEqual(19, t2.Hp);
            Assert.AreEqual(19, t3.Hp);
            Assert.AreEqual(19, t4.Hp);
        }

        // ── Shield 测试 ──

        [Test]
        public void Shield_Self_累加自身护甲()
        {
            var card = NewCard(1, TargetMode.Self);
            var effects = new List<CardEffect> { NewEffect(1, 1, EffectKind.Shield, 5) };
            var caster = new FakeActor { Armor = 2 };

            CardEffectExecutor.Execute(card, effects, caster, new List<IBattleActor>(), new CapturingSink());

            Assert.AreEqual(7, caster.Armor);
        }

        // ── DamageDot 测试 ──

        [Test]
        public void DamageDot_SingleManual_写入Buff不立即扣血()
        {
            var card = NewCard(1, TargetMode.SingleManual);
            var effects = new List<CardEffect> { NewEffect(1, 1, EffectKind.DamageDot, 2, 3) };
            var caster = new FakeActor();
            var target = new FakeActor { Hp = 20 };

            CardEffectExecutor.Execute(card, effects, caster, new List<IBattleActor> { target }, new CapturingSink());

            Assert.AreEqual(20, target.Hp); // 不立即扣血
            Assert.AreEqual(1, target.Buffs.Count);
            Assert.AreEqual(EffectKind.DamageDot, target.Buffs[0].Kind);
            Assert.AreEqual(2, target.Buffs[0].Value);
            Assert.AreEqual(3, target.Buffs[0].RemainingTurns);
            Assert.AreSame(caster, target.Buffs[0].SourceActor);
        }

        [Test]
        public void DamageInstantPlusDot_两条效果都生效()
        {
            // 法术卡：Damage 8 + DamageDot 2 Duration 3
            var card = NewCard(1, TargetMode.SingleManual);
            var effects = new List<CardEffect>
            {
                NewEffect(1, 1, EffectKind.Damage, 8),
                NewEffect(2, 1, EffectKind.DamageDot, 2, 3),
            };
            var caster = new FakeActor();
            var target = new FakeActor { Hp = 30 };

            CardEffectExecutor.Execute(card, effects, caster, new List<IBattleActor> { target }, new CapturingSink());

            Assert.AreEqual(22, target.Hp); // 立即扣 8
            Assert.AreEqual(1, target.Buffs.Count);
            Assert.AreEqual(2, target.Buffs[0].Value);
            Assert.AreEqual(3, target.Buffs[0].RemainingTurns);
        }

        // ── EnergyGain 测试 ──

        [Test]
        public void EnergyGain_Self_增加施法者能量允许超过上限()
        {
            var card = NewCard(1, TargetMode.Self, cost: 0);
            var effects = new List<CardEffect> { NewEffect(1, 1, EffectKind.EnergyGain, 2) };
            var caster = new FakeActor { CurrentEnergy = 2, MaxEnergy = 3 };

            CardEffectExecutor.Execute(card, effects, caster, new List<IBattleActor>(), new CapturingSink());

            Assert.AreEqual(4, caster.CurrentEnergy); // 设计语义：允许超过 MaxEnergy
        }

        // ── 边界测试 ──

        [Test]
        public void SingleAuto_全部敌方已死时无任何效果()
        {
            var card = NewCard(1, TargetMode.SingleAuto);
            var effects = new List<CardEffect> { NewEffect(1, 1, EffectKind.Damage, 6) };
            var caster = new FakeActor();
            var dead1 = new FakeActor { Hp = 0 };
            var dead2 = new FakeActor { Hp = 0 };
            var sink = new CapturingSink();

            CardEffectExecutor.Execute(card, effects, caster, new List<IBattleActor> { dead1, dead2 }, sink);

            Assert.AreEqual(0, sink.Died.Count);
        }

        [Test]
        public void NullEffects_早返回不抛异常()
        {
            var card = NewCard(1, TargetMode.Self);
            var caster = new FakeActor();
            Assert.DoesNotThrow(() => CardEffectExecutor.Execute(card, (IList<CardEffect>)null, caster, new List<IBattleActor>(), new CapturingSink()));
        }
    }
}
