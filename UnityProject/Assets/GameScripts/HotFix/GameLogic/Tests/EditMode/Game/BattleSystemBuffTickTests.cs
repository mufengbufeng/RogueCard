using System;
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

        [Test]
        public void EndTurn_EnemyTurnStart杀死玩家_立即失败并跳过怪物行动和EnemyTurnEnd()
        {
            var (battle, model, bus, cardSystem, monsterSystem) = CreateBattleWithSystems();
            try
            {
                model.SetPhase(BattlePhase.PlayerTurn);
                model.PlayerBuffs.Add(new BuffRuntime
                {
                    Kind = EffectKind.DamageDot,
                    TriggerTiming = EffectTriggerTiming.EnemyTurnStart,
                    Value = model.PlayerHp,
                    RemainingTurns = 1,
                });
                model.PlayerBuffs.Add(new BuffRuntime
                {
                    Kind = EffectKind.DamageDot,
                    TriggerTiming = EffectTriggerTiming.EnemyTurnEnd,
                    Value = 99,
                    RemainingTurns = 1,
                });

                bool failed = false;
                bool turnEnded = false;
                bus.GetChannel<BattleEndedEvent>().Subscribe(e => failed = !e.IsVictory);
                bus.GetChannel<TurnEndedEvent>().Subscribe(_ => turnEnded = true);

                battle.EndTurn();

                Assert.IsTrue(failed);
                Assert.IsTrue(model.IsPlayerDead);
                Assert.IsFalse(turnEnded);
                Assert.AreEqual(0, monsterSystem.ExecuteTurnCallCount);
                Assert.AreEqual(1, model.PlayerBuffs.Count);
                Assert.AreEqual(EffectTriggerTiming.EnemyTurnEnd, model.PlayerBuffs[0].TriggerTiming);
            }
            finally
            {
                battle.Dispose();
                cardSystem.Dispose();
                bus.Dispose();
            }
        }

        [Test]
        public void EndTurn_EnemyTurnEnd在怪物行动后结算并进入Check()
        {
            var (battle, model, bus, cardSystem, monsterSystem) = CreateBattleWithSystems();
            try
            {
                var monster = new MonsterRuntime { Hp = 7, MaxHp = 30 };
                model.SetMonsters(new List<MonsterRuntime> { monster });
                monster.AddBuff(new BuffRuntime
                {
                    Kind = EffectKind.DamageDot,
                    TriggerTiming = EffectTriggerTiming.EnemyTurnEnd,
                    Value = 7,
                    RemainingTurns = 1,
                });
                model.SetPhase(BattlePhase.PlayerTurn);

                bool turnEnded = false;
                bus.GetChannel<TurnEndedEvent>().Subscribe(_ => turnEnded = true);

                battle.EndTurn();

                Assert.AreEqual(1, monsterSystem.ExecuteTurnCallCount);
                Assert.AreEqual(0, monster.Hp);
                Assert.AreEqual(0, monster.Buffs.Count);
                Assert.IsTrue(turnEnded);
                Assert.AreEqual(BattlePhase.Check, model.Phase);
            }
            finally
            {
                battle.Dispose();
                cardSystem.Dispose();
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

        private static (BattleSystem battle, GameModel model, LocalEventBus bus, CardSystem cardSystem, CountingMonsterSystem monsterSystem)
            CreateBattleWithSystems()
        {
            var model = new GameModel();
            model.InitBattleAttributes(maxEnergy: 3, handLimit: 5, maxHp: 50);
            var bus = new LocalEventBus();
            var cardSystem = new CardSystem();
            cardSystem.Init(model, bus);
            var monsterSystem = new CountingMonsterSystem();
            monsterSystem.Init(model, bus);
            var battle = new BattleSystem();
            battle.Init(model, bus);
            battle.Initialize(cardSystem, monsterSystem);
            return (battle, model, bus, cardSystem, monsterSystem);
        }

        private static void InvokeTick(BattleSystem battle)
        {
            var method = typeof(BattleSystem).GetMethod(
                "TickBuffs",
                BindingFlags.NonPublic | BindingFlags.Instance,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null);
            Assert.NotNull(method, "未找到私有方法 TickBuffs");
            method.Invoke(battle, null);
        }

        private sealed class CountingMonsterSystem : MonsterSystem
        {
            public int ExecuteTurnCallCount { get; private set; }

            /// <summary>
            /// 记录怪物行动调用次数，避免依赖 MonsterCardSystem 或配置表。
            /// </summary>
            public override void ExecuteTurn()
            {
                ExecuteTurnCallCount++;
            }
        }
    }
}
