using System;
using System.Reflection;
using GameConfig.player;
using NUnit.Framework;

namespace GameLogic.Tests.EditMode.Game
{
    /// <summary>
    /// 验证 BattleSystem 按 TbPlayerLevel 初始化玩家属性的逻辑：
    /// - 1 级玩家进入战斗 → HP=100 / MaxEnergy=3 / HandLimit=10
    /// - 5 级玩家进入战斗 → HP=140
    /// - 缺 1 级数据时抛 InvalidOperationException
    /// 通过 ResolvePlayerLevel + ApplyPlayerLevelAttributes 可测入口直接验证，避开 GameLogicEntry.Config 静态依赖。
    /// </summary>
    [TestFixture]
    public class BattleSystemPlayerLevelTests
    {
        private static PlayerLevel NewLevel(int id, int requiredExp, int baseHp, int baseEnergy, int handLimit)
        {
            var lv = (PlayerLevel)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(PlayerLevel));
            SetReadonly(lv, "Id", id);
            SetReadonly(lv, "RequiredExp", requiredExp);
            SetReadonly(lv, "BaseHp", baseHp);
            SetReadonly(lv, "BaseEnergy", baseEnergy);
            SetReadonly(lv, "HandLimit", handLimit);
            return lv;
        }

        private static TbPlayerLevel NewTable(params PlayerLevel[] levels)
        {
            var tb = (TbPlayerLevel)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(TbPlayerLevel));
            var dataMap = new System.Collections.Generic.Dictionary<int, PlayerLevel>();
            var dataList = new System.Collections.Generic.List<PlayerLevel>();
            foreach (var lv in levels)
            {
                dataMap[lv.Id] = lv;
                dataList.Add(lv);
            }
            SetField(tb, "_dataMap", dataMap);
            SetField(tb, "_dataList", dataList);
            return tb;
        }

        private static void SetReadonly(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field, $"未找到字段 {fieldName}");
            field.SetValue(target, value);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field, $"未找到字段 {fieldName}");
            field.SetValue(target, value);
        }

        [Test]
        public void Init_1级玩家_HP100_Energy3_Hand10()
        {
            var tb = NewTable(
                NewLevel(1, 0, 100, 3, 10),
                NewLevel(5, 350, 140, 3, 10));
            var model = new GameModel();
            model.SetPlayerLevelId(1);

            var data = BattleSystem.ResolvePlayerLevel(tb, model.PlayerLevelId);
            BattleSystem.ApplyPlayerLevelAttributes(model, data);

            Assert.AreEqual(100, model.PlayerHp);
            Assert.AreEqual(100, model.PlayerMaxHp);
            Assert.AreEqual(3, model.MaxEnergy);
            Assert.AreEqual(10, model.HandLimit);
        }

        [Test]
        public void Init_5级玩家_HP140()
        {
            var tb = NewTable(
                NewLevel(1, 0, 100, 3, 10),
                NewLevel(5, 350, 140, 3, 10));
            var model = new GameModel();
            model.SetPlayerLevelId(5);

            var data = BattleSystem.ResolvePlayerLevel(tb, model.PlayerLevelId);
            BattleSystem.ApplyPlayerLevelAttributes(model, data);

            Assert.AreEqual(140, model.PlayerHp);
            Assert.AreEqual(140, model.PlayerMaxHp);
        }

        [Test]
        public void Init_未知等级回退到1级()
        {
            var tb = NewTable(NewLevel(1, 0, 100, 3, 10));
            var data = BattleSystem.ResolvePlayerLevel(tb, level: 99);
            Assert.AreEqual(1, data.Id);
            Assert.AreEqual(100, data.BaseHp);
        }

        [Test]
        public void Init_缺1级数据时抛异常()
        {
            var tb = NewTable(NewLevel(2, 50, 110, 3, 10));
            var ex = Assert.Throws<InvalidOperationException>(() =>
                BattleSystem.ResolvePlayerLevel(tb, level: 1));
            StringAssert.Contains("缺少 1 级 PlayerLevel 数据", ex.Message);
        }

        [Test]
        public void Init_TbPlayerLevel为null时抛异常()
        {
            Assert.Throws<InvalidOperationException>(() =>
                BattleSystem.ResolvePlayerLevel(null, level: 1));
        }
    }
}
