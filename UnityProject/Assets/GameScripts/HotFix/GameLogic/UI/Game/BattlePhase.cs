namespace GameLogic
{
    /// <summary>
    /// 战斗阶段枚举。
    /// </summary>
    public enum BattlePhase
    {
        /// <summary>
        /// 空闲状态。
        /// </summary>
        Idle = 0,

        /// <summary>
        /// 准备阶段：刷新怪物意图、恢复能量、抽牌。
        /// </summary>
        Prepare = 1,

        /// <summary>
        /// 玩家回合：出牌或结束回合。
        /// </summary>
        PlayerTurn = 2,

        /// <summary>
        /// 怪物回合：按意图执行行为。
        /// </summary>
        MonsterTurn = 3,

        /// <summary>
        /// 检查阶段：判断胜负和推进。
        /// </summary>
        Check = 4,

        /// <summary>
        /// 奖励阶段：关卡完成后展示奖励选择界面。
        /// </summary>
        Reward = 5,
    }
}
