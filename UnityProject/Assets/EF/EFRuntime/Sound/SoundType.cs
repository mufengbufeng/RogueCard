namespace EF.Sound
{
    /// <summary>
    /// 音频类型枚举，用于区分不同音频的播放策略。
    /// </summary>
    public enum SoundType
    {
        /// <summary>
        /// 背景音乐：长音频，流式加载，支持循环、淡入淡出。
        /// </summary>
        Music = 0,

        /// <summary>
        /// 音效：短音频，完全加载到内存，支持多实例同时播放。
        /// </summary>
        SoundEffect = 1,

        /// <summary>
        /// 对话/旁白：中长音频，可能需要字幕同步，不支持循环。
        /// </summary>
        Voice = 2,

        /// <summary>
        /// 环境音：长音频，支持循环，通常音量较低。
        /// </summary>
        Ambient = 3
    }
}
