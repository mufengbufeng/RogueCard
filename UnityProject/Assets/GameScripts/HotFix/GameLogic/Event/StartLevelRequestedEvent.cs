namespace GameLogic
{
    /// <summary>
    /// 请求进入关卡事件。
    /// </summary>
    public readonly struct StartLevelRequestedEvent
    {
        /// <summary>
        /// 关卡标识。
        /// </summary>
        public readonly string LevelId;

        /// <summary>
        /// 关卡展示名称。
        /// </summary>
        public readonly string LevelName;

        /// <summary>
        /// 创建请求进入关卡事件。
        /// </summary>
        public StartLevelRequestedEvent(string levelId, string levelName)
        {
            LevelId = levelId;
            LevelName = levelName;
        }
    }
}
