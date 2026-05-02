using EF.Sound;
using UnityEngine;

namespace EF.Examples
{
    /// <summary>
    /// Sound 模块使用示例。
    /// 演示如何使用 Sound 管理器播放音频、控制音量、以及处理不同类型的音频。
    /// </summary>
    public class SoundExample : MonoBehaviour
    {
        private ISoundManager _soundManager;
        private int _currentMusicId = -1;
        private int _currentVoiceId = -1;

        private void Start()
        {
            // 示例：从全局管理器获取 Sound 管理器
            // _soundManager = EFCore.GetManager<SoundManager>();

            // 或者直接创建（示例用途）
            // _soundManager = new SoundManager(resourceManager);
        }

        /// <summary>
        /// 示例：播放背景音乐。
        /// </summary>
        public void PlayBackgroundMusic()
        {
            // 方式1: 使用扩展方法（推荐）
            _currentMusicId = _soundManager.PlayMusic("Audio/BGM/MainTheme", volume: 0.8f, fadeInDuration: 1.5f);

            // 方式2: 使用完整参数
            _currentMusicId = _soundManager.Play(new SoundPlayArgs
            {
                AssetName = "Audio/BGM/MainTheme",
                SoundType = SoundType.Music,
                Volume = 0.8f,
                Loop = true,
                FadeInDuration = 1.5f,
                Priority = SoundConstant.HighestPriority
            });
        }

        /// <summary>
        /// 示例：播放 2D 音效。
        /// </summary>
        public void PlayButtonClick()
        {
            // 使用扩展方法，简单快捷
            _soundManager.PlaySoundEffect("Audio/SFX/ButtonClick");
        }

        /// <summary>
        /// 示例：播放 3D 音效（位置音频）。
        /// </summary>
        public void PlayExplosion(Vector3 position)
        {
            _soundManager.PlaySoundEffect3D("Audio/SFX/Explosion", position, volume: 1f);
        }

        /// <summary>
        /// 示例：播放语音（带完成回调）。
        /// </summary>
        public void PlayVoiceLine()
        {
            _currentVoiceId = _soundManager.PlayVoice("Audio/Voice/Intro", volume: 1f, onComplete: (soundId) =>
            {
                Debug.Log($"语音播放完成: {soundId}");
                // 可以在这里触发字幕隐藏、继续剧情等逻辑
            });
        }

        /// <summary>
        /// 示例：播放环境音。
        /// </summary>
        public void PlayAmbientSound()
        {
            _soundManager.PlayAmbient("Audio/Ambient/Forest", volume: 0.4f, loop: true);
        }

        /// <summary>
        /// 示例：停止背景音乐（带淡出）。
        /// </summary>
        public void StopBackgroundMusic()
        {
            if (_currentMusicId >= 0)
            {
                _soundManager.Stop(_currentMusicId, fadeOutDuration: 2f);
            }

            // 或者停止所有音乐
            _soundManager.StopAllMusic(fadeOutDuration: 2f);
        }

        /// <summary>
        /// 示例：暂停和恢复音乐。
        /// </summary>
        public void PauseAndResumeMusic()
        {
            if (_currentMusicId >= 0)
            {
                if (_soundManager.IsPlaying(_currentMusicId))
                {
                    _soundManager.Pause(_currentMusicId);
                }
                else
                {
                    _soundManager.Resume(_currentMusicId);
                }
            }
        }

        /// <summary>
        /// 示例：调整音量。
        /// </summary>
        public void AdjustVolume()
        {
            // 调整主音量
            _soundManager.MasterVolume = 0.8f;

            // 调整背景音乐音量
            _soundManager.MusicVolume = 0.6f;

            // 调整音效音量
            _soundManager.SoundEffectVolume = 1f;

            // 调整语音音量
            _soundManager.VoiceVolume = 1f;

            // 调整环境音音量
            _soundManager.AmbientVolume = 0.3f;
        }

        /// <summary>
        /// 示例：静音和取消静音。
        /// </summary>
        public void MuteAndUnmute()
        {
            // 静音
            _soundManager.Mute();

            // 取消静音
            _soundManager.Unmute(volume: 0.8f);
        }

        /// <summary>
        /// 示例：获取播放进度。
        /// </summary>
        public void CheckProgress()
        {
            if (_currentVoiceId >= 0)
            {
                float progress = _soundManager.GetProgress(_currentVoiceId);
                Debug.Log($"当前语音播放进度: {progress * 100}%");
            }
        }

        /// <summary>
        /// 示例：高级用法 - 跟随物体的 3D 音频。
        /// </summary>
        public void PlayFollowingSound(Transform target)
        {
            _soundManager.Play(new SoundPlayArgs
            {
                AssetName = "Audio/SFX/Engine",
                SoundType = SoundType.SoundEffect,
                Volume = 0.8f,
                Loop = true,
                SpatialBlend = SoundConstant.SpatialBlend3D,
                AttachedTransform = target, // 音频将跟随这个 Transform 移动
                Priority = 100
            });
        }

        /// <summary>
        /// 示例：暂停游戏时暂停所有音频。
        /// </summary>
        public void OnGamePause()
        {
            _soundManager.PauseAll();
        }

        /// <summary>
        /// 示例：恢复游戏时恢复所有音频。
        /// </summary>
        public void OnGameResume()
        {
            _soundManager.ResumeAll();
        }

        /// <summary>
        /// 示例：切换场景时停止所有音频。
        /// </summary>
        public void OnSceneChange()
        {
            _soundManager.StopAll(fadeOutDuration: 0.5f);
        }
    }
}
