using System;
using EF.UI;

namespace GameLogic
{
    /// <summary>
    /// 回合控制视图所需的 ViewModel 切片接口。
    /// 仅暴露 TurnControlView 所需的 Phase 字段、EndTurn 命令、CardPlayFailed 事件，
    /// 避免子模块越界访问 Hand / Monsters 等其它字段。
    /// 由 GameViewModel 显式实现；测试时可由 FakeTurnContext 实现替代。
    /// </summary>
    public interface ITurnContext
    {
        /// <summary>当前战斗阶段（用于结束回合按钮的启用控制）。</summary>
        ReactiveProperty<BattlePhase> Phase { get; }

        /// <summary>结束回合命令意图。</summary>
        void EndTurn();

        /// <summary>出牌失败事件，参数为失败原因字符串（NotPlayerTurn / InsufficientEnergy / 等）。</summary>
        event Action<string> CardPlayFailed;
    }
}
