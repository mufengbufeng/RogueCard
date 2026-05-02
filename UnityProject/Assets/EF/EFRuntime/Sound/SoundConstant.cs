namespace EF.Sound
{
    /// <summary>
    /// 音频常量定义。
    /// </summary>
    public static class SoundConstant
    {
        /// <summary>
        /// 默认淡入时长（秒）。
        /// </summary>
        public const float DefaultFadeInDuration = 0.5f;

        /// <summary>
        /// 默认淡出时长（秒）。
        /// </summary>
        public const float DefaultFadeOutDuration = 0.5f;

        /// <summary>
        /// 最小音量值。
        /// </summary>
        public const float MinVolume = 0f;

        /// <summary>
        /// 最大音量值。
        /// </summary>
        public const float MaxVolume = 1f;

        /// <summary>
        /// Unity AudioSource 最高优先级。
        /// </summary>
        public const int HighestPriority = 0;

        /// <summary>
        /// Unity AudioSource 默认优先级。
        /// </summary>
        public const int DefaultPriority = 128;

        /// <summary>
        /// Unity AudioSource 最低优先级。
        /// </summary>
        public const int LowestPriority = 256;

        /// <summary>
        /// 2D 音频的空间混合值。
        /// </summary>
        public const float SpatialBlend2D = 0f;

        /// <summary>
        /// 3D 音频的空间混合值。
        /// </summary>
        public const float SpatialBlend3D = 1f;
    }
}
