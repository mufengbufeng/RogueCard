using System;
using System.Collections.Generic;
using EF.UI;

namespace GameLogic.Tests
{
    /// <summary>
    /// 测试用 IPlayerStatusContext 实现。所有字段是 ReactiveProperty，
    /// 测试中可直接修改 .Value 触发 PlayerStatusView 的订阅刷新。
    /// </summary>
    public class FakePlayerStatusContext : IPlayerStatusContext
    {
        public ReactiveProperty<BattlePhase> Phase { get; } = new ReactiveProperty<BattlePhase>(BattlePhase.Idle);
        public ReactiveProperty<int> PlayerHp { get; } = new ReactiveProperty<int>(0);
        public ReactiveProperty<int> PlayerMaxHp { get; } = new ReactiveProperty<int>(0);
        public ReactiveProperty<int> PlayerArmor { get; } = new ReactiveProperty<int>(0);
        public ReactiveProperty<int> Energy { get; } = new ReactiveProperty<int>(0);
        public ReactiveProperty<int> MaxEnergy { get; } = new ReactiveProperty<int>(0);
        public ReactiveProperty<bool> IsLevelComplete { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<bool> IsPlayerDead { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<IReadOnlyList<BuffRuntime>> PlayerBuffs { get; } =
            new ReactiveProperty<IReadOnlyList<BuffRuntime>>(Array.Empty<BuffRuntime>());
    }
}
