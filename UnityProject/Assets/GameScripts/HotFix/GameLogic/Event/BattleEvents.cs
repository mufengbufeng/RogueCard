namespace GameLogic
{
    /// <summary>
    /// 卡牌打出事件，在 CardSystem.Play 成功执行后发布。
    /// </summary>
    public readonly struct CardPlayedEvent
    {
        /// <summary>
        /// 打出的卡牌配置标识。
        /// </summary>
        public readonly int CardId;

        /// <summary>
        /// 创建卡牌打出事件。
        /// </summary>
        public CardPlayedEvent(int cardId)
        {
            CardId = cardId;
        }
    }

    /// <summary>
    /// 回合结束事件，在 BattleSystem.EndTurn 执行完毕后发布。
    /// </summary>
    public readonly struct TurnEndedEvent
    {
    }

    /// <summary>
    /// 怪物死亡事件，在怪物血量降至 0 或以下时发布。
    /// </summary>
    public readonly struct MonsterDeathEvent
    {
        /// <summary>
        /// 死亡怪物的运行时索引。
        /// </summary>
        public readonly int MonsterIndex;

        /// <summary>
        /// 创建怪物死亡事件。
        /// </summary>
        public MonsterDeathEvent(int monsterIndex)
        {
            MonsterIndex = monsterIndex;
        }
    }

    /// <summary>
    /// 战斗结束事件，在当前批次怪物全灭或玩家死亡时发布。
    /// </summary>
    public readonly struct BattleEndedEvent
    {
        /// <summary>
        /// 是否胜利。
        /// </summary>
        public readonly bool IsVictory;

        /// <summary>
        /// 创建战斗结束事件。
        /// </summary>
        public BattleEndedEvent(bool isVictory)
        {
            IsVictory = isVictory;
        }
    }

    /// <summary>
    /// 关卡完成事件，在所有波次完成时发布。
    /// </summary>
    public readonly struct LevelCompleteEvent
    {
        /// <summary>
        /// 完成的关卡标识。
        /// </summary>
        public readonly int LevelId;

        /// <summary>
        /// 创建关卡完成事件。
        /// </summary>
        public LevelCompleteEvent(int levelId)
        {
            LevelId = levelId;
        }
    }
}
