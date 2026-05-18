namespace GameLogic
{
    /// <summary>
    /// 局内 UGUI 窗口所需的战斗上下文切片接口。
    /// 继承所有战斗相关切片，方便视图或控制器一次拿全。
    /// 由 GameViewModel 显式实现。
    /// </summary>
    public interface IBattleContext :
        IPlayerStatusContext,
        IMonsterListContext,
        IHandContext,
        ITurnContext,
        ITargetContext
    {
    }
}
