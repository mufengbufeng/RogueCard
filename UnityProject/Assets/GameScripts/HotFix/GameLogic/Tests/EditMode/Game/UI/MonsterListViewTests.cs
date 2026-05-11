using System.Collections.Generic;
using System.Reflection;
using GameConfig.monster;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameLogic.Tests
{
    /// <summary>
    /// MonsterListView 单元测试：通过 FakeMonsterListContext + 手工构造的 VisualTreeAsset
    /// 验证空列表 / 含死亡怪物过滤 / Items 数量 / Dispose 行为。
    /// 怪物模板用 ScriptableObject.CreateInstance<VisualTreeAsset>() 创建空模板，
    /// MonsterItemView 内的查询会找不到子元素，但不会抛异常 —— 测试关注 List 本身的契约。
    /// </summary>
    [TestFixture]
    public class MonsterListViewTests
    {
        private VisualElement _container;
        private VisualTreeAsset _template;
        private FakeMonsterListContext _ctx;

        [SetUp]
        public void SetUp()
        {
            _container = new VisualElement { name = "monster-container" };
            _template = ScriptableObject.CreateInstance<VisualTreeAsset>();
            _ctx = new FakeMonsterListContext();
        }

        [TearDown]
        public void TearDown()
        {
            if (_template != null) Object.DestroyImmediate(_template);
        }

        /// <summary>反射构造一个最小 Monster 配置（仅 Name 字段）。</summary>
        private static Monster NewMonsterConfig(string name)
        {
            var cfg = (Monster)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Monster));
            var nameField = typeof(Monster).GetField("Name",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (nameField != null) nameField.SetValue(cfg, name);
            return cfg;
        }

        private static MonsterRuntime NewMonster(int hp, int maxHp, string name = "M")
        {
            return new MonsterRuntime
            {
                Hp = hp,
                MaxHp = maxHp,
                Config = NewMonsterConfig(name),
            };
        }

        [Test]
        public void 空列表_容器无子元素()
        {
            var view = new MonsterListView(_container, _ctx, _template);
            Assert.AreEqual(0, view.Items.Count);
            view.Dispose();
        }

        [Test]
        public void 含死亡怪物_过滤后仅保留存活项()
        {
            var view = new MonsterListView(_container, _ctx, _template);
            _ctx.Monsters.Value = new[]
            {
                NewMonster(10, 10, "A"),
                NewMonster(0, 10, "B"),  // dead
                NewMonster(5, 10, "C"),
            };

            Assert.AreEqual(2, view.Items.Count);
            view.Dispose();
        }

        [Test]
        public void Monsters变化_全量重建项()
        {
            var view = new MonsterListView(_container, _ctx, _template);
            _ctx.Monsters.Value = new[] { NewMonster(10, 10), NewMonster(10, 10), NewMonster(10, 10) };
            Assert.AreEqual(3, view.Items.Count);

            // 重新发布列表（含 1 只死亡）→ 全量重建为 2
            _ctx.Monsters.Value = new[]
            {
                NewMonster(10, 10),
                NewMonster(10, 10),
                NewMonster(0, 10),
            };

            Assert.AreEqual(2, view.Items.Count);
            view.Dispose();
        }

        [Test]
        public void Dispose后_Monsters变化不再触发渲染()
        {
            var view = new MonsterListView(_container, _ctx, _template);
            _ctx.Monsters.Value = new[] { NewMonster(10, 10) };
            int countBefore = view.Items.Count;

            view.Dispose();
            _ctx.Monsters.Value = new[] { NewMonster(10, 10), NewMonster(10, 10) };

            // Items 仍是 Dispose 时清空后的状态（0），原有项已被释放
            Assert.AreEqual(0, view.Items.Count);
            Assert.AreEqual(0, _container.childCount);
        }

        [Test]
        public void 重复Dispose_安全()
        {
            var view = new MonsterListView(_container, _ctx, _template);
            view.Dispose();
            Assert.DoesNotThrow(() => view.Dispose());
        }
    }
}
