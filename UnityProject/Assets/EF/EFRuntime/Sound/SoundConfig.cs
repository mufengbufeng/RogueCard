namespace EF.Sound
{
    /// <summary>
    /// 音频系统配置，用于初始化时的参数设置。
    /// </summary>
    public sealed class SoundConfig
    {
        /// <summary>
        /// 音频代理池初始大小。
        /// </summary>
        public int InitialPoolSize { get; set; } = 10;

        /// <summary>
        /// 音频代理池最大大小。
        /// </summary>
        public int MaxPoolSize { get; set; } = 50;

        /// <summary>
        /// 全局主音量（0.0 ~ 1.0）。
        /// </summary>
        public float MasterVolume { get; set; } = 1f;

        /// <summary>
        /// 背景音乐音量（0.0 ~ 1.0）。
        /// </summary>
        public float MusicVolume { get; set; } = 0.8f;

        /// <summary>
        /// 音效音量（0.0 ~ 1.0）。
        /// </summary>
        public float SoundEffectVolume { get; set; } = 1f;

        /// <summary>
        /// 语音音量（0.0 ~ 1.0）。
        /// </summary>
        public float VoiceVolume { get; set; } = 1f;

        /// <summary>
        /// 环境音音量（0.0 ~ 1.0）。
        /// </summary>
        public float AmbientVolume { get; set; } = 0.6f;

        /// <summary>
        /// 是否启用短音效缓存。
        /// </summary>
        public bool EnableSoundEffectCache { get; set; } = true;

        /// <summary>
        /// 是否启用长音频缓存（可能占用大量内存）。
        /// </summary>
        public bool EnableLongAudioCache { get; set; } = false;

        /// <summary>
        /// 短音效最大并发播放数量。
        /// </summary>
        public int MaxConcurrentSoundEffects { get; set; } = 32;

        /// <summary>
        /// 创建默认配置。
        /// </summary>
        public static SoundConfig Default => new();
    }
}
