using GameConfig.card;

namespace GameLogic
{
    /// <summary>
    /// Buff 运行时实例，描述 DoT、增益、减益等持续效果。
    /// </summary>
    public class BuffRuntime
    {
        /// <summary>
        /// Buff 类型（复用 EffectKind 枚举）。
        /// </summary>
        public EffectKind Kind { get; set; }

        /// <summary>
        /// 数值（DoT 时为每回合伤害值）。
        /// </summary>
        public int Value { get; set; }

        /// <summary>
        /// 剩余持续回合数；每次 Tick 减 1，归零时被移除。
        /// </summary>
        public int RemainingTurns { get; set; }

        /// <summary>
        /// 来源施法者（用于伤害归因或反馈）。
        /// </summary>
        public IBattleActor SourceActor { get; set; }
    }
}
