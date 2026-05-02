using UnityEngine;

namespace EF.Sound
{
    /// <summary>
    /// 音频管理器扩展方法，提供更便捷的调用方式。
    /// </summary>
    public static class SoundManagerExtensions
    {
        /// <summary>
        /// 快速播放 2D 音效（不循环）。
        /// </summary>
        public static int PlaySoundEffect(this ISoundManager manager, string assetName, float volume = 1f)
        {
            return manager.Play(new SoundPlayArgs
            {
                AssetName = assetName,
                SoundType = SoundType.SoundEffect,
                Volume = volume,
                Loop = false,
                SpatialBlend = SoundConstant.SpatialBlend2D
            });
        }

        /// <summary>
        /// 快速播放 3D 音效。
        /// </summary>
        public static int PlaySoundEffect3D(this ISoundManager manager, string assetName, Vector3 position, float volume = 1f)
        {
            return manager.Play(new SoundPlayArgs
            {
                AssetName = assetName,
                SoundType = SoundType.SoundEffect,
                Volume = volume,
                Loop = false,
                SpatialBlend = SoundConstant.SpatialBlend3D,
                Position = position
            });
        }

        /// <summary>
        /// 快速播放背景音乐（循环，带淡入）。
        /// </summary>
        public static int PlayMusic(this ISoundManager manager, string assetName, float volume = 1f, float fadeInDuration = 0.5f, bool loop = true)
        {
            return manager.Play(new SoundPlayArgs
            {
                AssetName = assetName,
                SoundType = SoundType.Music,
                Volume = volume,
                Loop = loop,
                FadeInDuration = fadeInDuration,
                Priority = SoundConstant.HighestPriority,
                SpatialBlend = SoundConstant.SpatialBlend2D
            });
        }

        /// <summary>
        /// 快速播放语音。
        /// </summary>
        public static int PlayVoice(this ISoundManager manager, string assetName, float volume = 1f, System.Action<int> onComplete = null)
        {
            return manager.Play(new SoundPlayArgs
            {
                AssetName = assetName,
                SoundType = SoundType.Voice,
                Volume = volume,
                Loop = false,
                Priority = SoundConstant.HighestPriority,
                SpatialBlend = SoundConstant.SpatialBlend2D,
                OnComplete = onComplete
            });
        }

        /// <summary>
        /// 快速播放环境音（循环）。
        /// </summary>
        public static int PlayAmbient(this ISoundManager manager, string assetName, float volume = 0.6f, bool loop = true)
        {
            return manager.Play(new SoundPlayArgs
            {
                AssetName = assetName,
                SoundType = SoundType.Ambient,
                Volume = volume,
                Loop = loop,
                SpatialBlend = SoundConstant.SpatialBlend2D
            });
        }

        /// <summary>
        /// 停止所有背景音乐（带淡出）。
        /// </summary>
        public static void StopAllMusic(this ISoundManager manager, float fadeOutDuration = 0.5f)
        {
            manager.StopAll(SoundType.Music, fadeOutDuration);
        }

        /// <summary>
        /// 停止所有音效。
        /// </summary>
        public static void StopAllSoundEffects(this ISoundManager manager)
        {
            manager.StopAll(SoundType.SoundEffect, 0f);
        }

        /// <summary>
        /// 停止所有语音。
        /// </summary>
        public static void StopAllVoices(this ISoundManager manager, float fadeOutDuration = 0.2f)
        {
            manager.StopAll(SoundType.Voice, fadeOutDuration);
        }

        /// <summary>
        /// 静音所有音频。
        /// </summary>
        public static void Mute(this ISoundManager manager)
        {
            manager.MasterVolume = 0f;
        }

        /// <summary>
        /// 取消静音。
        /// </summary>
        public static void Unmute(this ISoundManager manager, float volume = 1f)
        {
            manager.MasterVolume = Mathf.Clamp01(volume);
        }
    }
}
