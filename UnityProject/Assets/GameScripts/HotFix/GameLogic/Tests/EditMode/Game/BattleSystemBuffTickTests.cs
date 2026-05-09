using System.Collections.Generic;
using System.Reflection;
using EF.UI;
using GameConfig.card;
using NUnit.Framework;

namespace GameLogic.Tests.EditMode.Game
{
    /// <summary>
    /// 验证 BattleSystem 在 MonsterTurn 入口对 Buffs 的 Tick 行为。
    /// 直接调用私有 TickBuffs 方法，验证 DoT 扣血、剩余回合数倒数、归零移除。
    /// </summary>
    [TestFixture]
    public class BattleSystemBuffTickTests
    {
        [Test]
        public void TickBuffs_DamageDot_玩家受DoT扣血并倒数()
        {
            var (battle, model, bus) = CreateBattle();
            try
            {
                model.PlayerBuffs.Add(new BuffRuntime
                {
                    Kind = EffectKind.DamageDot,
                    Value = 3,
                    RemainingTurns = 2,
                });

                int before = model.PlayerHp;
                InvokeTick(battle);

                Assert.AreEqual(before - 3, model.PlayerHp);
                Assert.AreEqual(1, model.PlayerBuffs.Count);
                Assert.AreEqual(1, model.PlayerBuffs[0].RemainingTurns);
            }
            finally
            {
                battle.Dispose();
                bus.Dispose();
            }
        }

        [Test]
        public void TickBuffs_RemainingTurns归零自动移除()
        {
            var (battle, model, bus) = CreateBattle();
            try
            {
                model.PlayerBuffs.Add(new BuffRuntime
                {
                    Kind = EffectKind.DamageDot,
                    Value = 1,
                    RemainingTurns = 1,
                });

                InvokeTick(battle);

                Assert.AreEqual(0, model.PlayerBuffs.Count);
            }
            finally
            {
                battle.Dispose();
                bus.Dispose();
            }
        }

        [Test]
        public void TickBuffs_怪物身上DoT扣血()
        {
            var (battle, model, bus) = CreateBattle();
            try
            {
                var monster = new MonsterRuntime { Hp = 30, MaxHp = 30 };
                model.SetMonsters(new List<MonsterRuntime> { monster });
                monster.AddBuff(new BuffRuntime
                {
                    Kind = EffectKind.DamageDot,
                    Value = 5,
                    RemainingTurns = 2,
                });

                InvokeTick(battle);

                Assert.AreEqual(25, monster.Hp);
                Assert.AreEqual(1, monster.Buffs[0].RemainingTurns);
            }
            finally
            {
                battle.Dispose();
                bus.Dispose();
            }
        }

        [Test]
        public void TickBuffs_仅DoT扣血其它Buff只倒数()
        {
            var (battle, model, bus) = CreateBattle();
            try
            {
                int before = model.PlayerHp;
                model.PlayerBuffs.Add(new BuffRuntime
                {
                    Kind = EffectKind.Shield, // 非 DoT
                    Value = 5,
                    RemainingTurns = 2,
                });

                InvokeTick(battle);

                Assert.AreEqual(before, model.PlayerHp); // 不扣血
                Assert.AreEqual(1, model.PlayerBuffs[0].RemainingTurns);
            }
            finally
            {
                battle.Dispose();
                bus.Dispose();
            }
        }

        // ── 测试基础设施 ──

        private static (BattleSystem battle, GameModel model, LocalEventBus bus) CreateBattle()
        {
            var model = new GameModel();
            model.InitBattleAttributes(maxEnergy: 3, handLimit: 5, maxHp: 50);
            var bus = new LocalEventBus();
            var battle = new BattleSystem();
            battle.Init(model, bus);
            return (battle, model, bus);
        }

        private static void InvokeTick(BattleSystem battle)
        {
            var method = typeof(BattleSystem).GetMethod(
                "TickBuffs",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method, "未找到私有方法 TickBuffs");
            method.Invoke(battle, null);
        }
    }
}
