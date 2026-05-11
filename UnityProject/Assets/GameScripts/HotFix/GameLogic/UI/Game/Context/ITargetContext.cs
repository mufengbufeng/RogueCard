using EF.UI;

namespace GameLogic
{
    /// <summary>
    /// 目标选择编排器所需的 ViewModel 切片接口。
    /// UseCardOnMonster 是 IHandContext.UseCard(handIdx, monsterIdx) 的语义命名（避免误用单参数 UseCard）。
    /// 由 GameViewModel 显式实现；测试时可由 FakeTargetContext 实现替代。
    /// </summary>
    public interface ITargetContext
    {
        /// <summary>当前战斗阶段（用于 Phase 中途变化强制取消选目标）。</summary>
        ReactiveProperty<BattlePhase> Phase { get; }

        /// <summary>使用卡牌指定怪物目标。等价于 IHandContext.UseCard(handIdx, monsterIdx)。</summary>
        void UseCardOnMonster(int handIdx, int monsterIdx);
    }
}
