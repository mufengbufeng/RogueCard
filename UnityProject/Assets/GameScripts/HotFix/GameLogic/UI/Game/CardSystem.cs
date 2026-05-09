using System;
using System.Collections.Generic;
using EF.Event;
using GameConfig.card;

namespace GameLogic
{
    /// <summary>
    /// 卡牌系统，管理出牌、抽牌、洗牌等卡牌操作。
    /// 出牌效果统一委托给 <see cref="CardEffectExecutor"/>，自身不再实现具体效果分支。
    /// </summary>
    public class CardSystem : IDisposable, IBattleEventSink
    {
        private readonly List<CardRuntime> _drawPile = new();
        private readonly Random _random = new();
        private GameModel _model;
        private IEventPublisher _events;
        private Action<TurnEndedEvent> _turnEndedHandler;
        private bool _isDisposed;

        /// <summary>
        /// 当前抽牌堆（只读，供外部查询）。
        /// </summary>
        public IReadOnlyList<CardRuntime> DrawPile => _drawPile;

        /// <summary>
        /// 初始化卡牌系统。
        /// </summary>
        public void Init(GameModel model, IEventPublisher events)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _turnEndedHandler = OnTurnEnded;
            _events.GetChannel<TurnEndedEvent>().Subscribe(_turnEndedHandler);
        }

        /// <summary>
        /// 初始化牌库，从配置表加载基础卡牌（OwnerKind 属于玩家）并洗牌。
        /// </summary>
        public void InitDeck()
        {
            var tables = GameLogicEntry.Config?.Tables;
            if (tables == null) return;

            _drawPile.Clear();
            foreach (var card in tables.TbCard.DataList)
            {
                if (!card.IsBasic) continue;
                if (card.OwnerKind != OwnerKind.Player && card.OwnerKind != OwnerKind.Both) continue;
                _drawPile.Add(new CardRuntime { Config = card });
            }

            ShuffleDrawPile();
        }

        /// <summary>
        /// 打出指定手牌索引的卡牌。SingleManual 卡牌可通过 monsterIndex 指定目标；
        /// 其它 TargetMode 忽略该参数。monsterIndex 为 -1 时由 Executor 决策（SingleAuto 取首个存活）。
        /// 失败时通过事件总线发布 <see cref="CardPlayFailedEvent"/>，Reason 取自 4 种枚举字符串。
        /// </summary>
        public bool Play(int handIndex, int monsterIndex = -1)
        {
            if (_model.Phase != BattlePhase.PlayerTurn)
            {
                PublishFailed("NotPlayerTurn");
                return false;
            }

            var hand = _model.Hand;
            if (handIndex < 0 || handIndex >= hand.Count)
            {
                PublishFailed("InvalidHandIndex");
                return false;
            }

            var card = hand[handIndex];
            if (card.Config.Cost > _model.CurrentEnergy)
            {
                PublishFailed("InsufficientEnergy");
                return false;
            }

            // SingleManual 模式下若调用方未指定有效目标且当前无任何存活敌方，视为 InvalidTarget
            if (card.Config.TargetMode == TargetMode.SingleManual && monsterIndex < 0)
            {
                bool anyAlive = false;
                foreach (var m in _model.Monsters)
                {
                    if (m != null && !m.IsDead) { anyAlive = true; break; }
                }
                if (!anyAlive)
                {
                    PublishFailed("InvalidTarget");
                    return false;
                }
            }

            _model.ModifyEnergy(-card.Config.Cost);

            var newHand = new List<CardRuntime>(hand);
            newHand.RemoveAt(handIndex);
            _model.SetHand(newHand);

            _model.AddToDiscardPile(card);

            var caster = new PlayerActor(_model);
            var targets = ResolveTargetCandidates(card.Config, monsterIndex);
            CardEffectExecutor.Execute(card.Config, caster, targets, this);

            _events.GetChannel<CardPlayedEvent>().Publish(new CardPlayedEvent(card.Config.Id));
            return true;
        }

        /// <summary>
        /// 根据 TargetMode 准备 Executor 的候选目标列表。
        /// </summary>
        private IList<IBattleActor> ResolveTargetCandidates(Card config, int monsterIndex)
        {
            switch (config.TargetMode)
            {
                case TargetMode.Self:
                    return Array.Empty<IBattleActor>();

                case TargetMode.SingleManual:
                {
                    var monsters = _model.Monsters;
                    if (monsterIndex >= 0 && monsterIndex < monsters.Count)
                    {
                        return new List<IBattleActor> { monsters[monsterIndex] };
                    }
                    return CollectAliveMonsters();
                }

                default:
                    return CollectAliveMonsters();
            }
        }

        /// <summary>
        /// 收集当前所有存活怪物。
        /// </summary>
        private IList<IBattleActor> CollectAliveMonsters()
        {
            var list = new List<IBattleActor>();
            foreach (var m in _model.Monsters)
            {
                if (m != null && !m.IsDead) list.Add(m);
            }
            return list;
        }

        /// <summary>
        /// 从牌库抽指定数量的牌到手牌。
        /// </summary>
        public void Draw(int count)
        {
            var hand = new List<CardRuntime>(_model.Hand);

            for (int i = 0; i < count; i++)
            {
                if (hand.Count >= _model.HandLimit) break;

                if (_drawPile.Count == 0)
                {
                    if (_model.DiscardPile.Count == 0) break;

                    _drawPile.AddRange(_model.DiscardPile);
                    _model.SetDiscardPile(new List<CardRuntime>());
                    ShuffleDrawPile();
                }

                if (_drawPile.Count > 0)
                {
                    hand.Add(_drawPile[0]);
                    _drawPile.RemoveAt(0);
                }
            }

            _model.SetHand(hand);
        }

        /// <summary>
        /// Fisher-Yates 洗牌算法。
        /// </summary>
        public void ShuffleDrawPile()
        {
            int n = _drawPile.Count;
            while (n > 1)
            {
                n--;
                int k = _random.Next(n + 1);
                (_drawPile[k], _drawPile[n]) = (_drawPile[n], _drawPile[k]);
            }
        }

        /// <summary>
        /// 通过事件总线发布出牌失败事件。
        /// </summary>
        private void PublishFailed(string reason)
        {
            _events?.GetChannel<CardPlayFailedEvent>().Publish(new CardPlayFailedEvent(reason));
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
        }

        private void OnTurnEnded(TurnEndedEvent e)
        {
            _model.DiscardHand();
            Draw(_model.HandLimit);
        }

        /// <summary>
        /// 释放卡牌系统资源。
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            if (_events != null)
            {
                _events.GetChannel<TurnEndedEvent>().Unsubscribe(_turnEndedHandler);
            }

            _drawPile.Clear();
            _model = null;
            _events = null;
        }
    }
}
