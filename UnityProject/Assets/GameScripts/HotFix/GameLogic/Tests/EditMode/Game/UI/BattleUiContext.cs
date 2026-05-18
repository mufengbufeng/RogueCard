using System;
using System.Collections.Generic;
using EF.UI;

namespace GameLogic.Tests
{
    /// <summary>
    /// BattlePanel / TargetSelector 测试用上下文。
    /// </summary>
    public sealed class BattleUiContext : IBattleContext
    {
        public ReactiveProperty<BattlePhase> Phase { get; } = new ReactiveProperty<BattlePhase>(BattlePhase.PlayerTurn);
        public ReactiveProperty<int> PlayerHp { get; } = new ReactiveProperty<int>(20);
        public ReactiveProperty<int> PlayerMaxHp { get; } = new ReactiveProperty<int>(20);
        public ReactiveProperty<int> PlayerArmor { get; } = new ReactiveProperty<int>(0);
        public ReactiveProperty<int> Energy { get; } = new ReactiveProperty<int>(3);
        public ReactiveProperty<int> MaxEnergy { get; } = new ReactiveProperty<int>(3);
        public ReactiveProperty<bool> IsLevelComplete { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<bool> IsPlayerDead { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<IReadOnlyList<BuffRuntime>> PlayerBuffs { get; } =
            new ReactiveProperty<IReadOnlyList<BuffRuntime>>(Array.Empty<BuffRuntime>());
        public ReactiveProperty<IReadOnlyList<MonsterRuntime>> Monsters { get; } =
            new ReactiveProperty<IReadOnlyList<MonsterRuntime>>(Array.Empty<MonsterRuntime>());
        public ReactiveProperty<IReadOnlyList<CardRuntime>> Hand { get; } =
            new ReactiveProperty<IReadOnlyList<CardRuntime>>(Array.Empty<CardRuntime>());

        public readonly List<(int handIdx, int targetIdx)> UseCardLog = new();
        public readonly List<(int handIdx, int monsterIdx)> UseCardOnMonsterLog = new();
        public int EndTurnCallCount;

        public event Action<string> CardPlayFailed;

        public void UseCard(int handIdx, int targetIdx = -1) => UseCardLog.Add((handIdx, targetIdx));

        public void UseCardOnMonster(int handIdx, int monsterIdx)
        {
            UseCardOnMonsterLog.Add((handIdx, monsterIdx));
            UseCard(handIdx, monsterIdx);
        }

        public void EndTurn() => EndTurnCallCount++;

        public void NotifyCardPlayFailed(string reason) => CardPlayFailed?.Invoke(reason);

        public void SetHand(IEnumerable<GameConfig.card.Card> cards)
        {
            var list = new List<CardRuntime>();
            foreach (GameConfig.card.Card card in cards)
            {
                list.Add(new CardRuntime { Config = card });
            }

            Hand.Value = list;
        }
    }
}
