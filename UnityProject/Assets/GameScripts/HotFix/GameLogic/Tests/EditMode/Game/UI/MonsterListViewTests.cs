using System.Collections.Generic;
using GameConfig.card;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace GameLogic.Tests
{
    /// <summary>
    /// UGUI MonsterItemView / MonsterListView 行为测试。
    /// </summary>
    [TestFixture]
    public sealed class MonsterListViewTests
    {
        private GameObject _root;
        private FakeMonsterListContext _context;
        private RectTransform _container;
        private GameObject _monsterTemplate;
        private GameObject _buffTemplate;
        private GameObject _intentTemplate;

        [SetUp]
        public void SetUp()
        {
            _root = UguiTestFactory.CreateRectObject("MonsterRoot");
            _context = new FakeMonsterListContext();
            _container = UguiTestFactory.CreateRectObject("MonsterRect", _root.transform).GetComponent<RectTransform>();
            _monsterTemplate = UguiTestFactory.CreateMonsterTemplate("MonsterItemTemplate", _container);
            _buffTemplate = UguiTestFactory.CreateIconTemplate("BuffIconTemplate", _root.transform);
            _intentTemplate = UguiTestFactory.CreateIconTemplate("IntentIconTemplate", _root.transform);
            MonsterItemView.EffectResolverOverride = card => new List<CardEffect> { UguiTestFactory.NewEffect(card.Id, EffectKind.Damage, 12) };
        }

        [TearDown]
        public void TearDown()
        {
            MonsterItemView.EffectResolverOverride = null;
            Object.DestroyImmediate(_root);
        }

        /// <summary>
        /// 列表只渲染存活怪物，并保留原始怪物索引。
        /// </summary>
        [Test]
        public void 刷新列表_只渲染存活怪物并保留索引()
        {
            var view = new MonsterListView(_container, _context, _monsterTemplate, _buffTemplate, _intentTemplate);

            _context.Monsters.Value = new[]
            {
                NewMonster("史莱姆", 10, 20),
                NewMonster("已死亡", 0, 20),
                NewMonster("蝙蝠", 8, 10),
            };

            Assert.AreEqual(2, view.Items.Count);
            Assert.AreEqual(0, view.Items[0].MonsterIndex);
            Assert.AreEqual(2, view.Items[1].MonsterIndex);
            view.Dispose();
        }

        /// <summary>
        /// 怪物项渲染名称、HP、护甲、意图和 Buff。
        /// </summary>
        [Test]
        public void 怪物项_渲染状态意图和Buff()
        {
            var dotCard = UguiTestFactory.NewCard(7, "毒击", 1, TargetMode.SplitAcrossAll);
            MonsterItemView.EffectResolverOverride = _ => new List<CardEffect> { UguiTestFactory.NewEffect(7, EffectKind.DamageDot, 3, 4) };
            var monster = NewMonster("毒蛛", 15, 30);
            monster.Armor = 2;
            monster.PendingCards.Add(dotCard);
            monster.Buffs.Add(new BuffRuntime { Kind = EffectKind.DamageDot, Value = 2, RemainingTurns = 3 });

            using var item = new MonsterItemView(_monsterTemplate, monster, 1, 4, _buffTemplate, _intentTemplate);

            Assert.AreEqual("毒蛛", FindText(_monsterTemplate, "NameText").text);
            Assert.That(FindText(_monsterTemplate, "HpText").text, Does.Contain("15/30"));
            Assert.AreEqual("3×4", FindText(_monsterTemplate.transform.Find("IntentBar").gameObject, "Text").text);
            Assert.AreEqual("2×3", FindText(_monsterTemplate.transform.Find("BuffBar").gameObject, "Text").text);
        }

        /// <summary>
        /// Dispose 后 Monsters 变化不再创建新项。
        /// </summary>
        [Test]
        public void Dispose后_不再刷新()
        {
            var view = new MonsterListView(_container, _context, _monsterTemplate, _buffTemplate, _intentTemplate);
            view.Dispose();

            _context.Monsters.Value = new[] { NewMonster("新怪", 10, 10) };

            Assert.AreEqual(1, _container.childCount, "容器中应只剩模板本体。");
        }

        private static MonsterRuntime NewMonster(string name, int hp, int maxHp)
        {
            return new MonsterRuntime
            {
                Config = UguiTestFactory.NewMonsterConfig(1, name),
                Hp = hp,
                MaxHp = maxHp,
            };
        }

        private static TextMeshProUGUI FindText(GameObject root, string name)
        {
            foreach (TextMeshProUGUI text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (text.gameObject.name == name)
                {
                    return text;
                }
            }

            return root.GetComponentInChildren<TextMeshProUGUI>(true);
        }
    }
}
