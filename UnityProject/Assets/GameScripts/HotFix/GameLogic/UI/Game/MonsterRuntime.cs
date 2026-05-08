using GameConfig.monster;

namespace GameLogic
{
    /// <summary>
    /// 怪物运行时实例，维护单只怪物的战斗状态。
    /// </summary>
    public class MonsterRuntime
    {
        /// <summary>
        /// 怪物配置引用。
        /// </summary>
        public Monster Config { get; set; }

        /// <summary>
        /// 当前血量。
        /// </summary>
        public int Hp { get; set; }

        /// <summary>
        /// 最大血量。
        /// </summary>
        public int MaxHp { get; set; }

        /// <summary>
        /// 当前护甲。
        /// </summary>
        public int Armor { get; set; }

        /// <summary>
        /// 当前意图。
        /// </summary>
        public MonsterIntent CurrentIntent { get; set; }

        /// <summary>
        /// 序列循环当前索引（Boss 模式使用）。
        /// </summary>
        public int IntentSequenceIndex { get; set; }
    }
}
