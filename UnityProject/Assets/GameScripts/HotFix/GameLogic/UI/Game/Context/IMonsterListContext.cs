using System.Collections.Generic;
using EF.UI;

namespace GameLogic
{
    /// <summary>
    /// 怪物列表面板所需的 ViewModel 切片接口。
    /// 仅暴露 Monsters 一个 ReactiveProperty，避免子模块越界访问 Hand / PlayerHp 等字段。
    /// 由 GameViewModel 显式实现；测试时可由 Fake 实现替代。
    /// </summary>
    public interface IMonsterListContext
    {
        /// <summary>当前在场怪物列表。</summary>
        ReactiveProperty<IReadOnlyList<MonsterRuntime>> Monsters { get; }
    }
}
