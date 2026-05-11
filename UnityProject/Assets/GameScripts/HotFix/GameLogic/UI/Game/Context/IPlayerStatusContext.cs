using System.Collections.Generic;
using EF.UI;

namespace GameLogic
{
    /// <summary>
    /// 玩家状态面板所需的 ViewModel 切片接口。
    /// 仅暴露 PlayerStatusView 所需的 9 个 ReactiveProperty，避免子模块越界访问 Hand / Monsters 等其它字段。
    /// 由 GameViewModel 显式实现；测试时可由 Fake 实现替代。
    /// </summary>
    public interface IPlayerStatusContext
    {
        /// <summary>当前战斗阶段。</summary>
        ReactiveProperty<BattlePhase> Phase { get; }

        /// <summary>玩家当前血量。</summary>
        ReactiveProperty<int> PlayerHp { get; }

        /// <summary>玩家最大血量。</summary>
        ReactiveProperty<int> PlayerMaxHp { get; }

        /// <summary>玩家当前护甲。</summary>
        ReactiveProperty<int> PlayerArmor { get; }

        /// <summary>玩家当前能量。</summary>
        ReactiveProperty<int> Energy { get; }

        /// <summary>玩家最大能量。</summary>
        ReactiveProperty<int> MaxEnergy { get; }

        /// <summary>关卡是否已完成（用于覆盖 Phase 文本）。</summary>
        ReactiveProperty<bool> IsLevelComplete { get; }

        /// <summary>玩家是否已死亡（用于覆盖 Phase 文本）。</summary>
        ReactiveProperty<bool> IsPlayerDead { get; }

        /// <summary>玩家身上的 Buff 列表（DoT 等持续效果）。</summary>
        ReactiveProperty<IReadOnlyList<BuffRuntime>> PlayerBuffs { get; }
    }
}
