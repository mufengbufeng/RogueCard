using EF.Common;
using UnityEngine;

namespace EF.Sound
{
    /// <summary>
    /// 定义音频管理器需要实现的核心能力接口。
    /// </summary>
    public interface ISoundManager : IEFManager
    {
        /// <summary>
        /// 当前活跃的音频数量。
        /// </summary>
        int ActiveSoundCount { get; }

        /// <summary>
        /// 全局主音量（0.0 ~ 1.0）。
        /// </summary>
        float MasterVolume { get; set; }

        /// <summary>
        /// 背景音乐音量（0.0 ~ 1.0）。
        /// </summary>
        float MusicVolume { get; set; }

        /// <summary>
        /// 音效音量（0.0 ~ 1.0）。
        /// </summary>
        float SoundEffectVolume { get; set; }

        /// <summary>
        /// 语音音量（0.0 ~ 1.0）。
        /// </summary>
        float VoiceVolume { get; set; }

        /// <summary>
        /// 环境音音量（0.0 ~ 1.0）。
        /// </summary>
        float AmbientVolume { get; set; }

        /// <summary>
        /// 播放音频。
        /// </summary>
        /// <param name="args">播放参数。</param>
        /// <returns>音频实例唯一标识，用于后续控制。</returns>
        int Play(SoundPlayArgs args);

        /// <summary>
        /// 停止指定音频。
        /// </summary>
        /// <param name="soundId">音频实例标识。</param>
        /// <param name="fadeOutDuration">淡出时长（秒）。</param>
        void Stop(int soundId, float fadeOutDuration = 0f);

        /// <summary>
        /// 暂停指定音频。
        /// </summary>
        /// <param name="soundId">音频实例标识。</param>
        void Pause(int soundId);

        /// <summary>
        /// 恢复指定音频。
        /// </summary>
        /// <param name="soundId">音频实例标识。</param>
        void Resume(int soundId);

        /// <summary>
        /// 停止指定类型的所有音频。
        /// </summary>
        /// <param name="soundType">音频类型。</param>
        /// <param name="fadeOutDuration">淡出时长（秒）。</param>
        void StopAll(SoundType soundType, float fadeOutDuration = 0f);

        /// <summary>
        /// 停止所有音频。
        /// </summary>
        /// <param name="fadeOutDuration">淡出时长（秒）。</param>
        void StopAll(float fadeOutDuration = 0f);

        /// <summary>
        /// 暂停指定类型的所有音频。
        /// </summary>
        /// <param name="soundType">音频类型。</param>
        void PauseAll(SoundType soundType);

        /// <summary>
        /// 暂停所有音频。
        /// </summary>
        void PauseAll();

        /// <summary>
        /// 恢复指定类型的所有音频。
        /// </summary>
        /// <param name="soundType">音频类型。</param>
        void ResumeAll(SoundType soundType);

        /// <summary>
        /// 恢复所有音频。
        /// </summary>
        void ResumeAll();

        /// <summary>
        /// 设置指定音频的音量。
        /// </summary>
        /// <param name="soundId">音频实例标识。</param>
        /// <param name="volume">音量（0.0 ~ 1.0）。</param>
        void SetVolume(int soundId, float volume);

        /// <summary>
        /// 检查指定音频是否正在播放。
        /// </summary>
        /// <param name="soundId">音频实例标识。</param>
        bool IsPlaying(int soundId);

        /// <summary>
        /// 获取指定音频的播放进度（0.0 ~ 1.0）。
        /// </summary>
        /// <param name="soundId">音频实例标识。</param>
        float GetProgress(int soundId);
    }
}
