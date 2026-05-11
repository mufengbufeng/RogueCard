namespace GameLogic
{
    /// <summary>
    /// 战斗子界面顶层协调器（BattlePanelView）所需的 union 切片接口。
    /// 继承所有战斗相关切片，方便 BattlePanelView 一次拿全。
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
