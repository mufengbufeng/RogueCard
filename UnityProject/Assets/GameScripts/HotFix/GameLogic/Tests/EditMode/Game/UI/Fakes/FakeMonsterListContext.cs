using System;
using System.Collections.Generic;
using EF.UI;

namespace GameLogic.Tests
{
    /// <summary>
    /// 测试用 IMonsterListContext 实现。Monsters 默认为空数组。
    /// </summary>
    public class FakeMonsterListContext : IMonsterListContext
    {
        public ReactiveProperty<IReadOnlyList<MonsterRuntime>> Monsters { get; } =
            new ReactiveProperty<IReadOnlyList<MonsterRuntime>>(Array.Empty<MonsterRuntime>());
    }
}
