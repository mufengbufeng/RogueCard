using System;
using System.Collections.Generic;
using EF.Event;

namespace GameLogic
{
    /// <summary>
    /// 怪物系统：Prepare 阶段为每只存活怪物准备 PendingCards；MonsterTurn 阶段执行 PendingCards。
    /// 实际行动通过 <see cref="CardEffectExecutor"/> 与 <see cref="MonsterCardSystem"/> 完成，自身不实现具体效果。
    /// </summary>
    public class MonsterSystem : IDisposable, IBattleEventSink
    {
        private GameModel _model;
        private IEventPublisher _events;
        private MonsterCardSystem _cardSystem;
        private bool _isDisposed;

        /// <summary>
        /// 初始化怪物系统。
        /// </summary>
        public void Init(GameModel model, IEventPublisher events)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _events = events ?? throw new ArgumentNullException(nameof(events));
        }

        /// <summary>
        /// 注入怪物卡牌系统依赖。
        /// </summary>
        public void Initialize(MonsterCardSystem cardSystem)
        {
            _cardSystem = cardSystem ?? throw new ArgumentNullException(nameof(cardSystem));
        }

        /// <summary>
        /// Prepare 阶段：恢复每只存活怪物能量、按剧本/抽牌生成 PendingCards。
        /// </summary>
        public void BeginMonsterPrepare()
        {
            if (_cardSystem == null)
            {
                throw new InvalidOperationException("MonsterSystem.BeginMonsterPrepare 调用前必须 Initialize 注入 MonsterCardSystem");
            }

            foreach (var monster in _model.Monsters)
            {
                if (monster == null || monster.IsDead) continue;

                // 恢复能量到上限（与玩家 Prepare 阶段对称）
                monster.CurrentEnergy = monster.MaxEnergy;

                int turnNumber = monster.TurnsAlive + 1;
                bool isScripted = monster.ScriptedCards != null
                    && monster.ScriptedCards.ContainsKey(turnNumber);

                if (!isScripted)
                {
                    _cardSystem.Draw(monster, monster.HandLimit);
                }

                var pending = MonsterAiBrain.SelectIntent(monster, turnNumber);
                monster.PendingCards.Clear();
                foreach (var card in pending)
                {
                    if (card != null) monster.PendingCards.Add(card);
                }
            }
        }

        /// <summary>
        /// MonsterTurn 阶段：对每只存活怪物按 PendingCards 依次调用 Executor，
        /// 之后弃光手牌、清空 PendingCards、TurnsAlive++。
        /// </summary>
        public virtual void ExecuteTurn()
        {
            if (_cardSystem == null) return;

            var playerActor = new PlayerActor(_model);

            for (int i = 0; i < _model.Monsters.Count; i++)
            {
                var monster = _model.Monsters[i];
                if (monster == null || monster.IsDead) continue;

                var targets = new List<IBattleActor> { playerActor };
                foreach (var card in monster.PendingCards)
                {
                    if (card == null) continue;
                    if (playerActor.IsDead) break; // 玩家已死，停止后续行动
                    CardEffectExecutor.Execute(card, monster, targets, this);
                }

                _cardSystem.DiscardAllHand(monster);
                monster.PendingCards.Clear();
                monster.TurnsAlive++;
            }
        }

        /// <summary>
        /// 生成指定批次的怪物运行时实例：读取 TbMonster 字段、初始化牌堆、设置初始能量。
        /// </summary>
        public void SpawnBatch(GameConfig.battle.BattleWaveSpawnBatch batch)
        {
            if (_cardSystem == null)
            {
                throw new InvalidOperationException("MonsterSystem.SpawnBatch 调用前必须 Initialize 注入 MonsterCardSystem");
            }

            var tables = GameLogicEntry.Config?.Tables;
            if (tables == null) return;

            var monsters = new List<MonsterRuntime>();
            for (int i = 0; i < batch.Count; i++)
            {
                var monsterConfig = tables.TbMonster.GetOrDefault(batch.MonsterId);
                if (monsterConfig == null) continue;

                var monster = new MonsterRuntime
                {
                    Config = monsterConfig,
                    MaxHp = monsterConfig.MaxHp,
                    Hp = monsterConfig.MaxHp,
                    Armor = 0,
                    MaxEnergy = monsterConfig.MaxEnergy,
                    CurrentEnergy = monsterConfig.MaxEnergy,
                    HandLimit = monsterConfig.HandLimit,
                    TurnsAlive = 0,
                };

                _cardSystem.InitDeck(monster);
                monsters.Add(monster);
            }

            _model.SetMonsters(monsters);
        }

        /// <inheritdoc />
        public void OnActorDied(IBattleActor actor)
        {
            if (actor is MonsterRuntime monster)
            {
                int index = -1;
                var list = _model.Monsters;
                for (int i = 0; i < list.Count; i++)
                {
                    if (ReferenceEquals(list[i], monster))
                    {
                        index = i;
                        break;
                    }
                }
                if (index >= 0)
                {
                    _events.GetChannel<MonsterDeathEvent>().Publish(new MonsterDeathEvent(index));
                }
            }
            // 玩家死亡由 BattleSystem.Check 阶段处理，此处不发布
        }

        /// <summary>
        /// 释放怪物系统资源。
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _model = null;
            _events = null;
            _cardSystem = null;
        }
    }
}
