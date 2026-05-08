using GameConfig.card;

namespace GameLogic
{
    /// <summary>
    /// 卡牌运行时实例，维护一张卡牌的局内状态。
    /// </summary>
    public class CardRuntime
    {
        /// <summary>
        /// 卡牌配置引用。
        /// </summary>
        public Card Config { get; set; }
    }
}
