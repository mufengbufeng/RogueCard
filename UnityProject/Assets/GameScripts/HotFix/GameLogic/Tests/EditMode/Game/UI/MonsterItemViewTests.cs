using System.Collections.Generic;
using System.Reflection;
using GameConfig.card;
using GameConfig.monster;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace GameLogic.Tests
{
    /// <summary>
    /// MonsterItemView 单元测试：手工构造怪物模板根元素 + 通过 EffectResolverOverride 注入 effect 配置，
    /// 覆盖 HP 文本、Buff bar、PendingCards 意图渲染（含 SplitAcrossAll 平分、DoT 文本、空列表）。
    /// 不依赖 GameLogicEntry.Config 静态状态。
    /// </summary>
    [TestFixture]
    public class MonsterItemViewTests
    {
        [TearDown]
        public void TearDown()
        {
            // 清理静态测试钩子，避免污染其它测试用例
            MonsterItemView.EffectResolverOverride = null;
        }

        /// <summary>
        /// 构造怪物项根元素：包含 name-text / hp-bar / hp-text / intent-text / intent-container / buff-bar
        /// 6 个子元素，模拟 MonsterItem.uxml 的 .monster-item 模板内部结构。
        /// </summary>
        private static VisualElement BuildMonsterItemRoot()
        {
            var root = new VisualElement();
            root.AddToClassList("monster-item");
            root.Add(new Label { name = "name-text" });
            root.Add(new VisualElement { name = "hp-bar" });
            root.Add(new Label { name = "hp-text" });
            root.Add(new Label { name = "intent-text" });
            root.Add(new VisualElement { name = "intent-container" });
            root.Add(new VisualElement { name = "buff-bar" });
            return root;
        }

        private static Monster NewMonsterConfig(string name)
        {
            var cfg = (Monster)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Monster));
            var nameField = typeof(Monster).GetField("Name",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (nameField != null) nameField.SetValue(cfg, name);
            return cfg;
        }

        private static Card NewCard(int id, TargetMode mode)
        {
            var card = (Card)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Card));
            SetField(card, "Id", id);
            SetField(card, "Name", $"Card_{id}");
            SetField(card, "Desc", string.Empty);
            SetField(card, "Cost", 1);
            SetField(card, "OwnerKind", OwnerKind.Player);
            SetField(card, "TargetMode", mode);
            SetField(card, "IsBasic", true);
            SetField(card, "AssetId", string.Empty);
            return card;
        }

        private static CardEffect NewEffect(int id, int cardId, EffectKind kind, int value, int duration = 0)
        {
            var eff = (CardEffect)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(CardEffect));
            SetField(eff, "Id", id);
            SetField(eff, "CardId", cardId);
            SetField(eff, "Kind", kind);
            SetField(eff, "Value", value);
            SetField(eff, "Duration", duration);
            SetField(eff, "Comment", string.Empty);
            return eff;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field, $"未找到字段 {fieldName}");
            field.SetValue(target, value);
        }

        // ── HP / 名称 / Armor 文本 ──

        [Test]
        public void HP文本_无护甲简洁()
        {
            var root = BuildMonsterItemRoot();
            var monster = new MonsterRuntime { Hp = 5, MaxHp = 20, Armor = 0, Config = NewMonsterConfig("Slime") };

            using var view = new MonsterItemView(root, monster, 1);

            Assert.AreEqual("HP:5/20", root.Q<Label>("hp-text").text);
        }

        [Test]
        public void HP文本_含护甲附加()
        {
            var root = BuildMonsterItemRoot();
            var monster = new MonsterRuntime { Hp = 5, MaxHp = 20, Armor = 3, Config = NewMonsterConfig("Slime") };

            using var view = new MonsterItemView(root, monster, 1);

            Assert.AreEqual("HP:5/20 护甲:3", root.Q<Label>("hp-text").text);
        }

        [Test]
        public void HP进度条_按百分比设置宽度()
        {
            var root = BuildMonsterItemRoot();
            var monster = new MonsterRuntime { Hp = 5, MaxHp = 20, Config = NewMonsterConfig("M") };

            using var view = new MonsterItemView(root, monster, 1);

            var hpBar = root.Q("hp-bar");
            Assert.AreEqual(LengthUnit.Percent, hpBar.style.width.value.unit);
            Assert.AreEqual(25f, hpBar.style.width.value.value, 0.01f);
        }

        [Test]
        public void 名称_从Config读取()
        {
            var root = BuildMonsterItemRoot();
            var monster = new MonsterRuntime { Hp = 10, MaxHp = 10, Config = NewMonsterConfig("史莱姆") };

            using var view = new MonsterItemView(root, monster, 1);

            Assert.AreEqual("史莱姆", root.Q<Label>("name-text").text);
        }

        [Test]
        public void 兼容旧IntentText_清空文本()
        {
            var root = BuildMonsterItemRoot();
            root.Q<Label>("intent-text").text = "stale";
            var monster = new MonsterRuntime { Hp = 10, MaxHp = 10, Config = NewMonsterConfig("M") };

            using var view = new MonsterItemView(root, monster, 1);

            Assert.AreEqual(string.Empty, root.Q<Label>("intent-text").text);
        }

        // ── Buff Bar ──

        [Test]
        public void Buff_DoT渲染_含dot类与文本()
        {
            var root = BuildMonsterItemRoot();
            var monster = new MonsterRuntime { Hp = 10, MaxHp = 10, Config = NewMonsterConfig("M") };
            monster.Buffs.Add(new BuffRuntime { Kind = EffectKind.DamageDot, Value = 2, RemainingTurns = 3 });

            using var view = new MonsterItemView(root, monster, 1);

            var bar = root.Q("buff-bar");
            Assert.AreEqual(1, bar.childCount);
            var icon = bar[0] as Label;
            Assert.NotNull(icon);
            Assert.IsTrue(icon.ClassListContains("buff-icon"));
            Assert.IsTrue(icon.ClassListContains("buff-icon-dot"));
            Assert.AreEqual("2×3", icon.text);
        }

        // ── 意图渲染（用 EffectResolverOverride 注入 effect 配置）──

        [Test]
        public void 空PendingCards_不创建IntentCard()
        {
            var root = BuildMonsterItemRoot();
            var monster = new MonsterRuntime { Hp = 10, MaxHp = 10, Config = NewMonsterConfig("M") };

            using var view = new MonsterItemView(root, monster, 1);

            Assert.AreEqual(0, root.Q("intent-container").childCount);
        }

        [Test]
        public void Damage意图_渲染单图标含值与damage类()
        {
            var card = NewCard(1, TargetMode.SingleAuto);
            MonsterItemView.EffectResolverOverride = c =>
                c.Id == 1 ? new List<CardEffect> { NewEffect(101, 1, EffectKind.Damage, 8) } : new List<CardEffect>();

            var root = BuildMonsterItemRoot();
            var monster = new MonsterRuntime { Hp = 10, MaxHp = 10, Config = NewMonsterConfig("M") };
            monster.PendingCards.Add(card);

            using var view = new MonsterItemView(root, monster, 1);

            var container = root.Q("intent-container");
            Assert.AreEqual(1, container.childCount);
            var intentCard = container[0];
            Assert.IsTrue(intentCard.ClassListContains("intent-card"));
            Assert.AreEqual(1, intentCard.childCount);
            var icon = intentCard[0] as Label;
            Assert.NotNull(icon);
            Assert.IsTrue(icon.ClassListContains("intent-icon"));
            Assert.IsTrue(icon.ClassListContains("intent-icon-damage"));
            Assert.AreEqual("8", icon.text);
        }

        [Test]
        public void SplitAcrossAll_平分_aliveCount4_value12()
        {
            var card = NewCard(2, TargetMode.SplitAcrossAll);
            MonsterItemView.EffectResolverOverride = c =>
                new List<CardEffect> { NewEffect(201, 2, EffectKind.Damage, 12) };

            var root = BuildMonsterItemRoot();
            var monster = new MonsterRuntime { Hp = 10, MaxHp = 10, Config = NewMonsterConfig("M") };
            monster.PendingCards.Add(card);

            using var view = new MonsterItemView(root, monster, 4);

            var icon = root.Q("intent-container")[0][0] as Label;
            Assert.AreEqual("3", icon.text);
        }

        [Test]
        public void SplitAcrossAll_至少1点_value2_aliveCount10()
        {
            var card = NewCard(3, TargetMode.SplitAcrossAll);
            MonsterItemView.EffectResolverOverride = c =>
                new List<CardEffect> { NewEffect(301, 3, EffectKind.Damage, 2) };

            var root = BuildMonsterItemRoot();
            var monster = new MonsterRuntime { Hp = 10, MaxHp = 10, Config = NewMonsterConfig("M") };
            monster.PendingCards.Add(card);

            using var view = new MonsterItemView(root, monster, 10);

            var icon = root.Q("intent-container")[0][0] as Label;
            Assert.AreEqual("1", icon.text);
        }

        [Test]
        public void DoT意图_文本格式valueXduration()
        {
            var card = NewCard(4, TargetMode.SingleAuto);
            MonsterItemView.EffectResolverOverride = c =>
                new List<CardEffect> { NewEffect(401, 4, EffectKind.DamageDot, 3, duration: 4) };

            var root = BuildMonsterItemRoot();
            var monster = new MonsterRuntime { Hp = 10, MaxHp = 10, Config = NewMonsterConfig("M") };
            monster.PendingCards.Add(card);

            using var view = new MonsterItemView(root, monster, 1);

            var icon = root.Q("intent-container")[0][0] as Label;
            Assert.IsTrue(icon.ClassListContains("intent-icon-dot"));
            Assert.AreEqual("3×4", icon.text);
        }
    }
}
