using System;
using System.Collections.Generic;
using System.Linq;
using EF.Event;
using GameConfig.monster;

namespace GameLogic
{
    /// <summary>
    /// 怪物系统，管理怪物意图刷新和回合行为执行。
    /// </summary>
    public class MonsterSystem : IDisposable
    {
        private readonly Random _random = new();
        private GameModel _model;
        private IEventPublisher _events;
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
        /// 刷新所有存活怪物的意图。
        /// </summary>
        public void RefreshIntents()
        {
            var tables = GameLogicEntry.Config?.Tables;
            if (tables == null) return;

            foreach (var monster in _model.Monsters)
            {
                if (monster.Hp <= 0) continue;

                var allIntents = new List<GameConfig.monster.MonsterIntent>();
                foreach (var intent in tables.TbMonsterIntent.DataList)
                {
                    if (intent.MonsterId == monster.Config.Id)
                        allIntents.Add(intent);
                }

                if (allIntents.Count == 0) continue;

                var sequence = allIntents
                    .Where(i => i.Order > 0 && i.Weight == 0)
                    .OrderBy(i => i.Order)
                    .ToList();
                var weighted = allIntents
                    .Where(i => i.Weight > 0 && i.Order == 0)
                    .ToList();

                if (sequence.Count > 0)
                {
                    monster.CurrentIntent = sequence[monster.IntentSequenceIndex % sequence.Count];
                    monster.IntentSequenceIndex++;
                }
                else if (weighted.Count > 0)
                {
                    int totalWeight = weighted.Sum(i => i.Weight);
                    int roll = _random.Next(totalWeight);
                    int cumulative = 0;
                    foreach (var intent in weighted)
                    {
                        cumulative += intent.Weight;
                        if (roll < cumulative)
                        {
                            monster.CurrentIntent = intent;
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 执行所有存活怪物的回合行动。
        /// </summary>
        public bool ExecuteTurn()
        {
            bool anyDeath = false;

            for (int i = 0; i < _model.Monsters.Count; i++)
            {
                var monster = _model.Monsters[i];
                if (monster.Hp <= 0 || monster.CurrentIntent == null) continue;

                switch (monster.CurrentIntent.IntentType)
                {
                    case MonsterIntentType.Attack:
                        int damage = monster.CurrentIntent.Value;
                        if (_model.PlayerArmor > 0)
                        {
                            int absorbed = Math.Min(_model.PlayerArmor, damage);
                            _model.ModifyPlayerArmor(-absorbed);
                            damage -= absorbed;
                        }

                        if (damage > 0)
                        {
                            _model.ModifyPlayerHp(-damage);
                        }

                        break;

                    case MonsterIntentType.Defend:
                        monster.Armor += monster.CurrentIntent.Value;
                        break;
                }
            }

            return anyDeath;
        }

        /// <summary>
        /// 生成指定批次的怪物运行时实例。
        /// </summary>
        public void SpawnBatch(GameConfig.battle.BattleWaveSpawnBatch batch)
        {
            var tables = GameLogicEntry.Config.Tables;
            var monsters = new List<MonsterRuntime>();

            for (int i = 0; i < batch.Count; i++)
            {
                var monsterConfig = tables.TbMonster.GetOrDefault(batch.MonsterId);
                if (monsterConfig != null)
                {
                    monsters.Add(new MonsterRuntime
                    {
                        Config = monsterConfig,
                        Hp = GameModel.DefaultMonsterHp,
                        MaxHp = GameModel.DefaultMonsterHp,
                        Armor = 0,
                        CurrentIntent = null,
                        IntentSequenceIndex = 0
                    });
                }
            }

            _model.SetMonsters(monsters);
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
        }
    }
}
