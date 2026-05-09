namespace GameLogic
{
    /// <summary>
    /// CardEffectExecutor 与战斗事件发布的解耦接口。
    /// </summary>
    public interface IBattleEventSink
    {
        /// <summary>
        /// 通知某个战斗参与者刚刚死亡（Hp 跨越 0）。
        /// 实现方负责把它转换为对应的事件总线事件。
        /// </summary>
        void OnActorDied(IBattleActor actor);
    }
}
