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

        /// <summary>
        /// 出牌后 Monsters.Value 被赋值为新数组（元素引用稳定）但 InstanceId 不变 → 位置必须保持稳定。
        /// 这是修 bug "每次出牌怪物位置都变" 的核心回归用例。
        /// </summary>
        [Test]
        public void Positions_StayStable_AfterMonstersValueReassigned()
        {
            var view = new MonsterListView(_container, _context, _monsterTemplate, _buffTemplate, _intentTemplate);
            var a = NewMonsterWithId(1, "A", 20, 20);
            var b = NewMonsterWithId(2, "B", 20, 20);
            var c = NewMonsterWithId(3, "C", 20, 20);

            var source1 = new[] { a, b, c };
            _context.Monsters.Value = source1;
            var firstPositions = SnapshotPositionsByInstanceId(view, source1);

            // 模拟出牌：怪物字段变化 + GameViewModel.SnapshotMonsters 新建数组（元素引用相同）
            a.Hp = 18;
            var source2 = new[] { a, b, c };
            _context.Monsters.Value = source2;

            var secondPositions = SnapshotPositionsByInstanceId(view, source2);
            Assert.AreEqual(firstPositions[1], secondPositions[1], "A 位置应稳定");
            Assert.AreEqual(firstPositions[2], secondPositions[2], "B 位置应稳定");
            Assert.AreEqual(firstPositions[3], secondPositions[3], "C 位置应稳定");
            view.Dispose();
        }

        /// <summary>
        /// 怪物死亡后该位置被回收，剩余存活怪物位置不变。
        /// </summary>
        [Test]
        public void Positions_DroppedForDeadMonsters_OthersUnchanged()
        {
            var view = new MonsterListView(_container, _context, _monsterTemplate, _buffTemplate, _intentTemplate);
            var a = NewMonsterWithId(1, "A", 20, 20);
            var b = NewMonsterWithId(2, "B", 20, 20);
            var c = NewMonsterWithId(3, "C", 20, 20);

            var source1 = new[] { a, b, c };
            _context.Monsters.Value = source1;
            var firstPositions = SnapshotPositionsByInstanceId(view, source1);

            b.Hp = 0; // 标记 B 死亡
            var source2 = new[] { a, b, c };
            _context.Monsters.Value = source2;

            Assert.AreEqual(2, view.Items.Count, "存活怪物渲染数应为 2");
            var secondPositions = SnapshotPositionsByInstanceId(view, source2);
            Assert.IsFalse(secondPositions.ContainsKey(2), "死亡的 B 不应再渲染");
            Assert.AreEqual(firstPositions[1], secondPositions[1], "A 位置应稳定");
            Assert.AreEqual(firstPositions[3], secondPositions[3], "C 位置应稳定");
            view.Dispose();
        }

        /// <summary>
        /// 切到下一波时 InstanceId 集合完全更换 → 新怪物获得新位置，旧 InstanceId 不再占空间冲突判定。
        /// </summary>
        [Test]
        public void Positions_AssignedFresh_OnNewWave()
        {
            var view = new MonsterListView(_container, _context, _monsterTemplate, _buffTemplate, _intentTemplate);
            var source1 = new[]
            {
                NewMonsterWithId(1, "Wave1A", 20, 20),
                NewMonsterWithId(2, "Wave1B", 20, 20),
            };
            _context.Monsters.Value = source1;
            var firstPositions = SnapshotPositionsByInstanceId(view, source1);
            Assert.IsTrue(firstPositions.ContainsKey(1));
            Assert.IsTrue(firstPositions.ContainsKey(2));

            // 换波
            var source2 = new[]
            {
                NewMonsterWithId(4, "Wave2A", 20, 20),
                NewMonsterWithId(5, "Wave2B", 20, 20),
            };
            _context.Monsters.Value = source2;

            var secondPositions = SnapshotPositionsByInstanceId(view, source2);
            Assert.AreEqual(2, secondPositions.Count);
            Assert.IsTrue(secondPositions.ContainsKey(4), "新波 InstanceId=4 应有位置");
            Assert.IsTrue(secondPositions.ContainsKey(5), "新波 InstanceId=5 应有位置");
            Assert.IsFalse(secondPositions.ContainsKey(1), "旧 InstanceId=1 不应残留");
            Assert.IsFalse(secondPositions.ContainsKey(2), "旧 InstanceId=2 不应残留");
            view.Dispose();
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

        /// <summary>
        /// 构造带显式 InstanceId 的怪物，用于位置稳定性测试。
        /// </summary>
        private static MonsterRuntime NewMonsterWithId(int instanceId, string name, int hp, int maxHp)
        {
            return new MonsterRuntime
            {
                InstanceId = instanceId,
                Config = UguiTestFactory.NewMonsterConfig(1, name),
                Hp = hp,
                MaxHp = maxHp,
            };
        }

        /// <summary>
        /// 读取 MonsterListView 当前所有 item 的 anchoredPosition，以 InstanceId 为 key。
        /// 通过 item.MonsterIndex 反查 source 数组取得 InstanceId。
        /// </summary>
        private static Dictionary<int, Vector2> SnapshotPositionsByInstanceId(MonsterListView view, IReadOnlyList<MonsterRuntime> source)
        {
            var result = new Dictionary<int, Vector2>();
            foreach (var item in view.Items)
            {
                var rt = item.Root.GetComponent<RectTransform>();
                int idx = item.MonsterIndex;
                if (idx < 0 || idx >= source.Count) continue;
                result[source[idx].InstanceId] = rt.anchoredPosition;
            }
            return result;
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
