using System.Collections.Generic;
using EF.UI;

namespace GameLogic.Tests
{
    /// <summary>
    /// 测试用 ITargetContext 实现：UseCardOnMonsterLog 记录调用。
    /// </summary>
    public class FakeTargetContext : ITargetContext
    {
        public ReactiveProperty<BattlePhase> Phase { get; } = new ReactiveProperty<BattlePhase>(BattlePhase.PlayerTurn);
        public readonly List<(int handIdx, int monsterIdx)> UseCardOnMonsterLog = new();

        public void UseCardOnMonster(int handIdx, int monsterIdx) => UseCardOnMonsterLog.Add((handIdx, monsterIdx));
    }

    /// <summary>测试用 IMonsterTargetSurface：记录 Enter/Exit 调用 + 暴露已捕获的 onMonsterClick 供测试触发。</summary>
    public class FakeMonsterTargetSurface : IMonsterTargetSurface
    {
        public IReadOnlyList<MonsterItemView> Items { get; set; } = System.Array.Empty<MonsterItemView>();
        public int EnterTargetModeCallCount;
        public int ExitTargetModeCallCount;
        public System.Action<int> CapturedClickHandler;

        public void EnterTargetMode(System.Action<int> onMonsterClick)
        {
            EnterTargetModeCallCount++;
            CapturedClickHandler = onMonsterClick;
        }

        public void ExitTargetMode()
        {
            ExitTargetModeCallCount++;
            CapturedClickHandler = null;
        }
    }

    /// <summary>测试用 IHandGhostSurface：记录 RequestGhostCleanup 与 RequestGhostRebound 调用。</summary>
    public class FakeHandGhostSurface : IHandGhostSurface
    {
        public int RequestGhostCleanupCallCount;
        public readonly List<int> RequestGhostReboundLog = new();

        public void RequestGhostCleanup() => RequestGhostCleanupCallCount++;
        public void RequestGhostRebound(int handIdx) => RequestGhostReboundLog.Add(handIdx);
    }
}
