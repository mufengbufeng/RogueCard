using System;
using System.Collections.Generic;
using EF.UI;

namespace GameLogic
{
    /// <summary>
    /// 手牌子系统所需的 ViewModel 切片接口。
    /// 仅暴露 HandFanView / CardDragController / CardPreviewController 所需的 Hand / Phase 字段、UseCard 命令、CardPlayFailed 事件，
    /// 避免子模块越界访问 Monsters / PlayerHp 等其它字段。
    /// 由 GameViewModel 显式实现；测试时可由 Fake 实现替代。
    /// </summary>
    public interface IHandContext
    {
        /// <summary>当前手牌列表。</summary>
        ReactiveProperty<IReadOnlyList<CardRuntime>> Hand { get; }

        /// <summary>当前战斗阶段（拖拽逻辑只在 PlayerTurn 接受 PointerDown）。</summary>
        ReactiveProperty<BattlePhase> Phase { get; }

        /// <summary>
        /// 出牌命令意图。targetIdx = -1 表示由后端按 TargetMode 自动决策；
        /// &gt;= 0 表示玩家手选的具体怪物索引（仅 SingleManual 模式使用）。
        /// </summary>
        void UseCard(int handIdx, int targetIdx = -1);

        /// <summary>出牌失败事件，参数为失败原因字符串（NotPlayerTurn / InsufficientEnergy / 等）。</summary>
        event Action<string> CardPlayFailed;
    }
}
