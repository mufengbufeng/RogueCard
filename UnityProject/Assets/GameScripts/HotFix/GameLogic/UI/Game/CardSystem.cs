using System;
using System.Collections.Generic;
using EF.Event;
using GameConfig.card;

namespace GameLogic
{
    /// <summary>
    /// 卡牌系统，管理出牌、抽牌、洗牌等卡牌操作。
    /// 出牌释放统一委托给 <see cref="CardReleaseResolver"/>，自身不实现目标优先级或具体效果分支。
    /// </summary>
    public class CardSystem : IDisposable, IBattleEventSink
    {
        private readonly List<CardRuntime> _drawPile = new();
        private readonly Random _random = new();
        private GameModel _model;
        private IEventPublisher _events;
        private CardReleaseResolver _releaseResolver;
        private Action<TurnEndedEvent> _turnEndedHandler;
        private bool _isDisposed;

        /// <summary>
        /// 当前抽牌堆（只读，供外部查询）。
        /// </summary>
        public IReadOnlyList<CardRuntime> DrawPile => _drawPile;

        /// <summary>
        /// 卡牌释放调度服务，供 BattleSystem 在敌人回合结算延迟效果。
        /// </summary>
        public CardReleaseResolver ReleaseResolver => _releaseResolver ??= new CardReleaseResolver();

        /// <summary>
        /// 创建卡牌系统。
        /// </summary>
        public CardSystem(CardReleaseResolver releaseResolver = null)
        {
            _releaseResolver = releaseResolver;
        }

        /// <summary>
        /// 初始化卡牌系统。
        /// </summary>
        public void Init(GameModel model, IEventPublisher events)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _releaseResolver ??= new CardReleaseResolver();
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
            if (card.Config.TargetMode == TargetMode.SingleManual
                && card.Config.CardReleaseKind != CardReleaseKind.Spell
                && monsterIndex < 0)
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
            ReleaseResolver.Release(card.Config, caster, _model.Monsters, monsterIndex, this);
            _model.NotifyMonstersChanged();

            _events.GetChannel<CardPlayedEvent>().Publish(new CardPlayedEvent(card.Config.Id));
            return true;
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
            _releaseResolver?.Clear();
            _releaseResolver = null;
            _model = null;
            _events = null;
        }
    }
}
