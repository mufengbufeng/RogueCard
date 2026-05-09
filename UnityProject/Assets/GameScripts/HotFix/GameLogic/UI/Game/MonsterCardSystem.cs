using System;
using System.Collections.Generic;
using GameConfig.card;
using GameConfig.monster;

namespace GameLogic
{
    /// <summary>
    /// 怪物卡牌系统，与玩家版 <see cref="CardSystem"/> 对称：管理每只怪物的牌堆初始化、抽牌、洗牌、弃牌。
    /// 不维护事件订阅 / 全局状态：所有牌堆数据放在 <see cref="MonsterRuntime"/> 自身字段上。
    /// </summary>
    public class MonsterCardSystem : IDisposable
    {
        private readonly Random _random = new();
        private bool _isDisposed;

        /// <summary>
        /// 初始化一只怪物的牌堆：从 <c>TbMonsterDeck</c> 读所有 <c>MonsterId</c> 命中本怪物的行，
        /// 按 <c>Order &gt; 0</c> 写入 <see cref="MonsterRuntime.ScriptedCards"/>，
        /// 按 <c>Order = 0</c> 按 <c>Count</c> 展开后写入抽牌堆并洗牌。
        /// </summary>
        public void InitDeck(MonsterRuntime monster)
        {
            if (monster == null || monster.Config == null) return;

            monster.ScriptedCards.Clear();
            monster.DrawPile.Clear();
            monster.DiscardPile.Clear();
            monster.Hand.Clear();
            monster.PendingCards.Clear();

            var tables = GameLogicEntry.Config?.Tables;
            if (tables == null) return;

            int monsterId = monster.Config.Id;
            foreach (var row in tables.TbMonsterDeck.DataList)
            {
                if (row.MonsterId != monsterId) continue;
                var cardConfig = row.CardId_Ref ?? tables.TbCard.GetOrDefault(row.CardId);
                if (cardConfig == null) continue;

                if (row.Order > 0)
                {
                    monster.ScriptedCards[row.Order] = cardConfig;
                }
                else
                {
                    int count = Math.Max(1, row.Count);
                    for (int i = 0; i < count; i++)
                    {
                        monster.DrawPile.Add(new CardRuntime { Config = cardConfig });
                    }
                }
            }

            ShuffleDrawPile(monster);
        }

        /// <summary>
        /// 给指定怪物抽牌；不超过 <see cref="MonsterRuntime.HandLimit"/>。
        /// 抽牌堆为空时把弃牌堆洗回抽牌堆继续抽。
        /// </summary>
        public void Draw(MonsterRuntime monster, int count)
        {
            if (monster == null) return;
            int limit = monster.HandLimit > 0 ? monster.HandLimit : int.MaxValue;

            for (int i = 0; i < count; i++)
            {
                if (monster.Hand.Count >= limit) break;

                if (monster.DrawPile.Count == 0)
                {
                    if (monster.DiscardPile.Count == 0) break;
                    monster.DrawPile.AddRange(monster.DiscardPile);
                    monster.DiscardPile.Clear();
                    ShuffleDrawPile(monster);
                }

                if (monster.DrawPile.Count > 0)
                {
                    var card = monster.DrawPile[0];
                    monster.DrawPile.RemoveAt(0);
                    monster.Hand.Add(card);
                }
            }
        }

        /// <summary>
        /// MonsterTurn 结束后将怪物当前手牌全部移入弃牌堆。
        /// </summary>
        public void DiscardAllHand(MonsterRuntime monster)
        {
            if (monster == null) return;
            if (monster.Hand.Count == 0) return;

            monster.DiscardPile.AddRange(monster.Hand);
            monster.Hand.Clear();
        }

        /// <summary>
        /// Fisher-Yates 洗牌一只怪物的抽牌堆。
        /// </summary>
        public void ShuffleDrawPile(MonsterRuntime monster)
        {
            if (monster == null) return;
            var pile = monster.DrawPile;
            int n = pile.Count;
            while (n > 1)
            {
                n--;
                int k = _random.Next(n + 1);
                (pile[k], pile[n]) = (pile[n], pile[k]);
            }
        }

        /// <summary>
        /// 释放怪物卡牌系统。
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
        }
    }
}
