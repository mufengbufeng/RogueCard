using System.Collections.Generic;
using System.Reflection;
using GameConfig.card;
using NUnit.Framework;

namespace GameLogic.Tests.EditMode.Game
{
    /// <summary>
    /// 验证 MonsterAiBrain.SelectIntent：
    /// - 第 N 回合命中剧本 → 直接返回该卡，不校验能量
    /// - 第 N 回合不命中剧本 → 按手牌 Cost 降序贪心
    /// </summary>
    [TestFixture]
    public class MonsterAiBrainTests
    {
        private static Card NewCard(int id, int cost)
        {
            var card = (Card)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Card));
            SetReadonly(card, "Id", id);
            SetReadonly(card, "Name", $"Card_{id}");
            SetReadonly(card, "Desc", string.Empty);
            SetReadonly(card, "Cost", cost);
            SetReadonly(card, "OwnerKind", OwnerKind.Monster);
            SetReadonly(card, "TargetMode", TargetMode.SingleAuto);
            SetReadonly(card, "IsBasic", true);
            SetReadonly(card, "AssetId", string.Empty);
            return card;
        }

        private static void SetReadonly(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field);
            field.SetValue(target, value);
        }

        [Test]
        public void SelectIntent_第1回合走剧本_不抽牌不校验能量()
        {
            var monster = new MonsterRuntime
            {
                MaxEnergy = 0, // 故意 0 能量
                CurrentEnergy = 0,
            };
            var script1 = NewCard(1006, 99); // 故意 Cost 99
            monster.ScriptedCards[1] = script1;

            var result = MonsterAiBrain.SelectIntent(monster, 1);

            Assert.AreEqual(1, result.Count);
            Assert.AreSame(script1, result[0]);
        }

        [Test]
        public void SelectIntent_剧本第3回合命中第3回合的卡()
        {
            var monster = new MonsterRuntime { MaxEnergy = 1, CurrentEnergy = 1 };
            var t1 = NewCard(1, 1);
            var t2 = NewCard(2, 1);
            var t3 = NewCard(3, 1);
            monster.ScriptedCards[1] = t1;
            monster.ScriptedCards[2] = t2;
            monster.ScriptedCards[3] = t3;

            var result = MonsterAiBrain.SelectIntent(monster, 3);

            Assert.AreEqual(1, result.Count);
            Assert.AreSame(t3, result[0]);
        }

        [Test]
        public void SelectIntent_无剧本走贪心_Cost降序选牌()
        {
            var monster = new MonsterRuntime { MaxEnergy = 3, CurrentEnergy = 3 };
            var c1 = NewCard(1, 2);
            var c2 = NewCard(2, 1);
            var c3 = NewCard(3, 3);
            monster.Hand.Add(new CardRuntime { Config = c1 });
            monster.Hand.Add(new CardRuntime { Config = c2 });
            monster.Hand.Add(new CardRuntime { Config = c3 });

            var result = MonsterAiBrain.SelectIntent(monster, 5);

            // Cost 降序：3, 2, 1。3+2+1=6 > 3 能量。先选 cost=3 (剩 0)，再选 cost=2 不够，再选 cost=1 不够 → 只选 1 张
            Assert.AreEqual(1, result.Count);
            Assert.AreSame(c3, result[0]);
        }

        [Test]
        public void SelectIntent_贪心可选多张直到能量耗尽()
        {
            var monster = new MonsterRuntime { MaxEnergy = 3, CurrentEnergy = 3 };
            var c1 = NewCard(1, 1);
            var c2 = NewCard(2, 1);
            var c3 = NewCard(3, 1);
            monster.Hand.Add(new CardRuntime { Config = c1 });
            monster.Hand.Add(new CardRuntime { Config = c2 });
            monster.Hand.Add(new CardRuntime { Config = c3 });

            var result = MonsterAiBrain.SelectIntent(monster, 5);

            Assert.AreEqual(3, result.Count);
        }

        [Test]
        public void SelectIntent_无手牌也无剧本时返回空列表()
        {
            var monster = new MonsterRuntime { MaxEnergy = 1, CurrentEnergy = 1 };
            var result = MonsterAiBrain.SelectIntent(monster, 1);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void SelectIntent_第4回合超过剧本范围_走贪心()
        {
            var monster = new MonsterRuntime { MaxEnergy = 1, CurrentEnergy = 1 };
            monster.ScriptedCards[1] = NewCard(1, 1);
            monster.ScriptedCards[2] = NewCard(2, 1);
            monster.ScriptedCards[3] = NewCard(3, 1);
            var fallback = NewCard(99, 1);
            monster.Hand.Add(new CardRuntime { Config = fallback });

            var result = MonsterAiBrain.SelectIntent(monster, 4);

            Assert.AreEqual(1, result.Count);
            Assert.AreSame(fallback, result[0]);
        }

        [Test]
        public void SelectIntent_能量不足时跳过该卡()
        {
            var monster = new MonsterRuntime { MaxEnergy = 1, CurrentEnergy = 1 };
            var expensive = NewCard(1, 5);
            monster.Hand.Add(new CardRuntime { Config = expensive });

            var result = MonsterAiBrain.SelectIntent(monster, 5);

            Assert.AreEqual(0, result.Count);
        }
    }
}
