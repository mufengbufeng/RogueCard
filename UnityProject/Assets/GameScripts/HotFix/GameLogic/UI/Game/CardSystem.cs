using System;
using System.Collections.Generic;
using EF.Event;

namespace GameLogic
{
    /// <summary>
    /// 卡牌系统，管理出牌、抽牌、洗牌等卡牌操作。
    /// </summary>
    public class CardSystem : IDisposable
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
        /// 初始化牌库，从配置表加载基础卡牌并洗牌。
        /// </summary>
        public void InitDeck()
        {
            var tables = GameLogicEntry.Config?.Tables;
            if (tables == null) return;

            _drawPile.Clear();
            foreach (var card in tables.TbCard.DataList)
            {
                if (card.IsBasic)
                {
                    _drawPile.Add(new CardRuntime { Config = card });
                }
            }

            ShuffleDrawPile();
        }

        /// <summary>
        /// 打出指定手牌索引的卡牌。
        /// </summary>
        public bool Play(int handIndex)
        {
            if (_model.Phase != BattlePhase.PlayerTurn) return false;

            var hand = _model.Hand;
            if (handIndex < 0 || handIndex >= hand.Count) return false;

            var card = hand[handIndex];
            if (card.Config.Cost > _model.CurrentEnergy) return false;

            _model.ModifyEnergy(-card.Config.Cost);

            var newHand = new List<CardRuntime>(hand);
            newHand.RemoveAt(handIndex);
            _model.SetHand(newHand);

            _model.AddToDiscardPile(card);

            ApplyEffect(card);

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
        /// 应用卡牌效果。
        /// </summary>
        private void ApplyEffect(CardRuntime card)
        {
            switch (card.Config.EffectType)
            {
                case "Attack":
                    var monsters = _model.Monsters;
                    MonsterRuntime target = null;
                    int targetIndex = -1;
                    for (int i = 0; i < monsters.Count; i++)
                    {
                        if (monsters[i].Hp > 0)
                        {
                            target = monsters[i];
                            targetIndex = i;
                            break;
                        }
                    }

                    if (target == null) break;

                    int damage = card.Config.Value;
                    if (target.Armor > 0)
                    {
                        int absorbed = Math.Min(target.Armor, damage);
                        target.Armor -= absorbed;
                        damage -= absorbed;
                    }

                    target.Hp -= damage;

                    if (target.Hp <= 0)
                    {
                        _events.GetChannel<MonsterDeathEvent>().Publish(new MonsterDeathEvent(targetIndex));
                    }

                    break;

                case "Defend":
                    _model.ModifyPlayerArmor(card.Config.Value);
                    break;
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
