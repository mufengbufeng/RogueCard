using System;
using System.Collections.Generic;
using EF.UI;

namespace GameLogic.Tests
{
    /// <summary>
    /// 测试用 IMonsterListContext 实现。Monsters 默认为空数组，
    /// 测试中可通过 Monsters.Value = new[] { ... } 触发 MonsterListView 的 Refresh。
    /// </summary>
    public class FakeMonsterListContext : IMonsterListContext
    {
        public ReactiveProperty<IReadOnlyList<MonsterRuntime>> Monsters { get; } =
            new ReactiveProperty<IReadOnlyList<MonsterRuntime>>(Array.Empty<MonsterRuntime>());
    }
}
