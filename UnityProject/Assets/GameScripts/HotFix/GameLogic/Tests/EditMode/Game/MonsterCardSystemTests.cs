using System.Collections.Generic;
using System.Reflection;
using GameConfig.card;
using GameConfig.monster;
using NUnit.Framework;

namespace GameLogic.Tests.EditMode.Game
{
    /// <summary>
    /// 验证 MonsterCardSystem 的牌堆操作（不依赖 GameLogicEntry：Draw / DiscardAllHand / 洗牌可独立验证；
    /// InitDeck 依赖 Tables，这里通过反射跳过 InitDeck 的 Tables 部分，直接构造怪物运行时验证后续行为）。
    /// </summary>
    [TestFixture]
    public class MonsterCardSystemTests
    {
        private static MonsterRuntime NewMonster(int handLimit = 2)
        {
            return new MonsterRuntime
            {
                MaxHp = 30,
                Hp = 30,
                MaxEnergy = 1,
                CurrentEnergy = 1,
                HandLimit = handLimit,
            };
        }

        private static Card NewCard(int id, int cost = 1, TargetMode mode = TargetMode.SingleAuto, OwnerKind owner = OwnerKind.Monster)
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

        private static void SetReadonly(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field, $"未找到字段 {fieldName}");
            field.SetValue(target, value);
        }

        [Test]
        public void Draw_不超过HandLimit()
        {
            var sys = new MonsterCardSystem();
            var monster = NewMonster(handLimit: 2);
            for (int i = 0; i < 5; i++)
            {
                monster.DrawPile.Add(new CardRuntime { Config = NewCard(1000 + i) });
            }

            sys.Draw(monster, 5);

            Assert.AreEqual(2, monster.Hand.Count);
            Assert.AreEqual(3, monster.DrawPile.Count);
        }

        [Test]
        public void Draw_抽牌堆耗尽时洗弃牌堆回来()
        {
            var sys = new MonsterCardSystem();
            var monster = NewMonster(handLimit: 4);
            // 抽牌堆 1 张，弃牌堆 2 张
            monster.DrawPile.Add(new CardRuntime { Config = NewCard(1) });
            monster.DiscardPile.Add(new CardRuntime { Config = NewCard(2) });
            monster.DiscardPile.Add(new CardRuntime { Config = NewCard(3) });

            sys.Draw(monster, 4);

            Assert.AreEqual(3, monster.Hand.Count);
            Assert.AreEqual(0, monster.DrawPile.Count);
            Assert.AreEqual(0, monster.DiscardPile.Count);
        }

        [Test]
        public void DiscardAllHand_清空手牌入弃牌堆()
        {
            var sys = new MonsterCardSystem();
            var monster = NewMonster();
            monster.Hand.Add(new CardRuntime { Config = NewCard(1) });
            monster.Hand.Add(new CardRuntime { Config = NewCard(2) });

            sys.DiscardAllHand(monster);

            Assert.AreEqual(0, monster.Hand.Count);
            Assert.AreEqual(2, monster.DiscardPile.Count);
        }

        [Test]
        public void ShuffleDrawPile_保持元素数不变()
        {
            var sys = new MonsterCardSystem();
            var monster = NewMonster();
            for (int i = 0; i < 10; i++)
            {
                monster.DrawPile.Add(new CardRuntime { Config = NewCard(i) });
            }

            sys.ShuffleDrawPile(monster);

            Assert.AreEqual(10, monster.DrawPile.Count);
        }

        [Test]
        public void Draw_两堆都空时停止抽牌不抛异常()
        {
            var sys = new MonsterCardSystem();
            var monster = NewMonster(handLimit: 5);

            Assert.DoesNotThrow(() => sys.Draw(monster, 3));
            Assert.AreEqual(0, monster.Hand.Count);
        }
    }
}
