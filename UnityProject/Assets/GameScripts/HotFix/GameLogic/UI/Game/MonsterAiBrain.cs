using System.Collections.Generic;
using GameConfig.card;

namespace GameLogic
{
    /// <summary>
    /// 怪物 AI 决策器。无内部状态，纯函数式服务。
    /// 路线 2：剧本前置 + AI 兜底——剧本回合直接产出指定卡，否则按手牌 Cost 降序贪心选牌。
    /// </summary>
    public static class MonsterAiBrain
    {
        /// <summary>
        /// 为指定怪物在第 <paramref name="turnNumber"/> 回合生成 PendingCards。
        /// </summary>
        /// <param name="monster">目标怪物。</param>
        /// <param name="turnNumber">本回合序号（从 1 开始）。一般传 <c>monster.TurnsAlive + 1</c>。</param>
        /// <returns>本回合应当依次打出的卡牌列表。剧本卡只返回 1 张；兜底回合返回 0~N 张。</returns>
        public static IList<Card> SelectIntent(MonsterRuntime monster, int turnNumber)
        {
            var result = new List<Card>();
            if (monster == null) return result;

            // 剧本回合：直接返回该回合指定卡，跳过 Cost 校验
            if (monster.ScriptedCards != null
                && monster.ScriptedCards.TryGetValue(turnNumber, out var scripted)
                && scripted != null)
            {
                result.Add(scripted);
                return result;
            }

            // 兜底回合：按手牌 Cost 降序贪心
            if (monster.Hand == null || monster.Hand.Count == 0) return result;

            // 复制并按 Cost 降序排序，避免破坏原手牌顺序
            var sorted = new List<CardRuntime>(monster.Hand);
            sorted.Sort((a, b) => b.Config.Cost.CompareTo(a.Config.Cost));

            int remainingEnergy = monster.CurrentEnergy;
            foreach (var card in sorted)
            {
                if (card?.Config == null) continue;
                if (card.Config.Cost > remainingEnergy) continue;

                result.Add(card.Config);
                remainingEnergy -= card.Config.Cost;
            }

            return result;
        }
    }
}
