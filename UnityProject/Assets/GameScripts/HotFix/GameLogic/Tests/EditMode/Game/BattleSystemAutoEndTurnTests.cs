using System.Collections.Generic;
using EF.UI;
using NUnit.Framework;

namespace GameLogic.Tests.EditMode.Game
{
    /// <summary>
    /// 验证 BattleSystem 在玩家回合内当前批次怪物全清时，自动跳过怪物回合直接进入检查阶段。
    /// 触发点是本地 <see cref="CardPlayedEvent"/>（卡牌一次完整结算结束）：测试通过手动 Publish 模拟卡牌结算完成。
    /// </summary>
    [TestFixture]
    public class BattleSystemAutoEndTurnTests
    {
        [Test]
        public void CardPlayed_当前批次怪物全清_自动进入Check并发布胜利()
        {
            var (battle, model, bus, _, monsterSystem) = CreateBattleWithSystems();
            try
            {
                model.SetMonsters(new List<MonsterRuntime>
                {
                    new MonsterRuntime { Hp = 0, MaxHp = 30 },
                    new MonsterRuntime { Hp = 0, MaxHp = 30 },
                });
                model.SetPhase(BattlePhase.PlayerTurn);

                bool victory = false;
                bool turnEnded = false;
                bus.GetChannel<BattleEndedEvent>().Subscribe(e => victory = e.IsVictory);
                bus.GetChannel<TurnEndedEvent>().Subscribe(_ => turnEnded = true);

                bus.GetChannel<CardPlayedEvent>().Publish(new CardPlayedEvent(1));

                Assert.AreEqual(BattlePhase.Check, model.Phase, "全清后应直接进入 Check 阶段");
                Assert.IsTrue(victory, "无后续批次时应发布 BattleEndedEvent(true)");
                Assert.IsFalse(turnEnded, "自动结束玩家回合不应发布 TurnEndedEvent（跳过了 MonsterTurn）");
                Assert.AreEqual(0, monsterSystem.ExecuteTurnCallCount, "自动结束玩家回合不应执行怪物行动");
            }
            finally
            {
                battle.Dispose();
                bus.Dispose();
            }
        }

        [Test]
        public void CardPlayed_仍有怪物存活_保持PlayerTurn()
        {
            var (battle, model, bus, _, monsterSystem) = CreateBattleWithSystems();
            try
            {
                model.SetMonsters(new List<MonsterRuntime>
                {
                    new MonsterRuntime { Hp = 5, MaxHp = 30 },
                    new MonsterRuntime { Hp = 0, MaxHp = 30 },
                });
                model.SetPhase(BattlePhase.PlayerTurn);

                bus.GetChannel<CardPlayedEvent>().Publish(new CardPlayedEvent(1));

                Assert.AreEqual(BattlePhase.PlayerTurn, model.Phase, "尚有存活怪物时应保持在 PlayerTurn");
                Assert.AreEqual(0, monsterSystem.ExecuteTurnCallCount);
            }
            finally
            {
                battle.Dispose();
                bus.Dispose();
            }
        }

        [Test]
        public void CardPlayed_多次发布_只触发一次相位切换()
        {
            var (battle, model, bus, _, monsterSystem) = CreateBattleWithSystems();
            try
            {
                model.SetMonsters(new List<MonsterRuntime>
                {
                    new MonsterRuntime { Hp = 0, MaxHp = 30 },
                });
                model.SetPhase(BattlePhase.PlayerTurn);

                int victoryCount = 0;
                bus.GetChannel<BattleEndedEvent>().Subscribe(e =>
                {
                    if (e.IsVictory) victoryCount++;
                });

                bus.GetChannel<CardPlayedEvent>().Publish(new CardPlayedEvent(1));
                Assert.AreEqual(BattlePhase.Check, model.Phase);
                Assert.AreEqual(1, victoryCount);

                // 再次发布 CardPlayedEvent（模拟 AOE 多目标在同一次结算中触发的情况，或额外的事件流）
                // BattleSystem 的 PlayerTurn 守卫应该阻止重复进入 Check。
                bus.GetChannel<CardPlayedEvent>().Publish(new CardPlayedEvent(1));
                bus.GetChannel<CardPlayedEvent>().Publish(new CardPlayedEvent(1));

                Assert.AreEqual(BattlePhase.Check, model.Phase);
                Assert.AreEqual(1, victoryCount, "重复 CardPlayedEvent 不应重复触发胜利结算");
                Assert.AreEqual(0, monsterSystem.ExecuteTurnCallCount);
            }
            finally
            {
                battle.Dispose();
                bus.Dispose();
            }
        }

        [Test]
        public void CardPlayed_无怪物列表_不切换相位()
        {
            var (battle, model, bus, _, _) = CreateBattleWithSystems();
            try
            {
                model.SetMonsters(new List<MonsterRuntime>());
                model.SetPhase(BattlePhase.PlayerTurn);

                bus.GetChannel<CardPlayedEvent>().Publish(new CardPlayedEvent(1));

                Assert.AreEqual(BattlePhase.PlayerTurn, model.Phase,
                    "空怪物列表时不视为'全清'，避免战斗未初始化阶段误触发");
            }
            finally
            {
                battle.Dispose();
                bus.Dispose();
            }
        }

        [Test]
        public void CardPlayed_非PlayerTurn_不切换相位()
        {
            var (battle, model, bus, _, monsterSystem) = CreateBattleWithSystems();
            try
            {
                model.SetMonsters(new List<MonsterRuntime>
                {
                    new MonsterRuntime { Hp = 0, MaxHp = 30 },
                });
                model.SetPhase(BattlePhase.MonsterTurn);

                bus.GetChannel<CardPlayedEvent>().Publish(new CardPlayedEvent(1));

                Assert.AreEqual(BattlePhase.MonsterTurn, model.Phase,
                    "非 PlayerTurn 阶段应被守卫拦下");
                Assert.AreEqual(0, monsterSystem.ExecuteTurnCallCount);
            }
            finally
            {
                battle.Dispose();
                bus.Dispose();
            }
        }

        [Test]
        public void Dispose_后CardPlayed_不再回调()
        {
            var (battle, model, bus, _, _) = CreateBattleWithSystems();
            model.SetMonsters(new List<MonsterRuntime>
            {
                new MonsterRuntime { Hp = 0, MaxHp = 30 },
            });
            model.SetPhase(BattlePhase.PlayerTurn);

            battle.Dispose();
            bus.GetChannel<CardPlayedEvent>().Publish(new CardPlayedEvent(1));

            Assert.AreEqual(BattlePhase.PlayerTurn, model.Phase,
                "Dispose 后 BattleSystem 不应再响应 CardPlayedEvent");

            // 再次 Dispose 应安全
            Assert.DoesNotThrow(() => battle.Dispose());

            bus.Dispose();
        }

        // ── 测试基础设施（与 BattleSystemBuffTickTests 保持一致，避免引入跨文件依赖）──

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
