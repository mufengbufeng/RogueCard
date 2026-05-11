using System;
using EF.UI;

namespace GameLogic.Tests
{
    /// <summary>
    /// 测试用 ITurnContext 实现：通过 Phase.Value 设置阶段、调 NotifyCardPlayFailed 触发事件，
    /// EndTurnCallCount 记录 EndTurn 调用次数。
    /// </summary>
    public class FakeTurnContext : ITurnContext
    {
        public ReactiveProperty<BattlePhase> Phase { get; } = new ReactiveProperty<BattlePhase>(BattlePhase.Idle);
        public int EndTurnCallCount;

        public event Action<string> CardPlayFailed;

        public void EndTurn() => EndTurnCallCount++;

        public void NotifyCardPlayFailed(string reason) => CardPlayFailed?.Invoke(reason);
    }
}
