using System;
using UnityEngine;

namespace EF.Sound
{
    /// <summary>
    /// 音频代理，封装 AudioSource 的生命周期管理与状态控制。
    /// </summary>
    internal sealed class SoundAgent
    {
        private AudioSource _audioSource;
        private SoundPlayArgs _playArgs;
        private float _fadeTimer;
        private float _fadeDuration;
        private float _fadeStartVolume;
        private float _fadeTargetVolume;
        private bool _isFading;
        private bool _stopAfterFade;

        /// <summary>
        /// 音频实例唯一标识。
        /// </summary>
        public int SoundId { get; private set; }

        /// <summary>
        /// 是否正在使用中。
        /// </summary>
        public bool IsActive { get; private set; }

        /// <summary>
        /// 音频类型。
        /// </summary>
        public SoundType SoundType => _playArgs?.SoundType ?? SoundType.SoundEffect;

        /// <summary>
        /// 是否正在播放。
        /// </summary>
        public bool IsPlaying => _audioSource != null && _audioSource.isPlaying;

        /// <summary>
        /// 初始化音频代理。
        /// </summary>
        public void Initialize(AudioSource audioSource)
        {
            _audioSource = audioSource ?? throw new ArgumentNullException(nameof(audioSource));
            IsActive = false;
        }

        /// <summary>
        /// 播放音频。
        /// </summary>
        public void Play(int soundId, AudioClip clip, SoundPlayArgs args, float typeVolume)
        {
            if (_audioSource == null)
            {
                throw new InvalidOperationException("音频代理未初始化");
            }

            SoundId = soundId;
            _playArgs = args;
            IsActive = true;

            _audioSource.clip = clip;
            _audioSource.loop = args.Loop;
            _audioSource.priority = args.Priority;
            _audioSource.spatialBlend = args.SpatialBlend;

            // 设置位置
            if (args.AttachedTransform != null)
            {
                _audioSource.transform.SetParent(args.AttachedTransform, false);
                _audioSource.transform.localPosition = Vector3.zero;
            }
            else if (args.Position.HasValue)
            {
                _audioSource.transform.position = args.Position.Value;
            }

            // 设置音量
            float targetVolume = args.Volume * typeVolume;
            if (args.FadeInDuration > 0f)
            {
                _audioSource.volume = 0f;
                StartFade(targetVolume, args.FadeInDuration, false);
            }
            else
            {
                _audioSource.volume = targetVolume;
            }

            _audioSource.Play();
        }

        /// <summary>
        /// 停止播放。
        /// </summary>
        public void Stop(float fadeOutDuration = 0f)
        {
            if (!IsActive || _audioSource == null)
            {
                return;
            }

            if (fadeOutDuration > 0f && _audioSource.isPlaying)
            {
                StartFade(0f, fadeOutDuration, true);
            }
            else
            {
                ForceStop();
            }
        }

        /// <summary>
        /// 暂停播放。
        /// </summary>
        public void Pause()
        {
            if (!IsActive || _audioSource == null)
            {
                return;
            }

            _audioSource.Pause();
        }

        /// <summary>
        /// 恢复播放。
        /// </summary>
        public void Resume()
        {
            if (!IsActive || _audioSource == null)
            {
                return;
            }

            _audioSource.UnPause();
        }

        /// <summary>
        /// 设置音量。
        /// </summary>
        public void SetVolume(float volume)
        {
            if (_audioSource != null && !_isFading)
            {
                _audioSource.volume = Mathf.Clamp01(volume);
            }
        }

        /// <summary>
        /// 更新类型音量（全局音量变化时调用）。
        /// </summary>
        public void UpdateTypeVolume(float typeVolume)
        {
            if (_audioSource != null && _playArgs != null && !_isFading)
            {
                _audioSource.volume = _playArgs.Volume * typeVolume;
            }
        }

        /// <summary>
        /// 获取播放进度（0.0 ~ 1.0）。
        /// </summary>
        public float GetProgress()
        {
            if (_audioSource == null || _audioSource.clip == null)
            {
                return 0f;
            }

            return _audioSource.time / _audioSource.clip.length;
        }

        /// <summary>
        /// 每帧更新。
        /// </summary>
        public bool Update(float deltaTime)
        {
            if (!IsActive)
            {
                return false;
            }

            // 更新淡入淡出
            if (_isFading)
            {
                _fadeTimer += deltaTime;
                float t = Mathf.Clamp01(_fadeTimer / _fadeDuration);
                _audioSource.volume = Mathf.Lerp(_fadeStartVolume, _fadeTargetVolume, t);

                if (t >= 1f)
                {
                    _isFading = false;
                    if (_stopAfterFade)
                    {
                        ForceStop();
                        return false;
                    }
                }
            }

            // 检查是否播放完成
            if (!_audioSource.isPlaying && !_playArgs.Loop)
            {
                _playArgs?.OnComplete?.Invoke(SoundId);
                Release();
                return false;
            }

            // 更新位置跟随
            if (_playArgs?.AttachedTransform != null && _audioSource.transform.parent != _playArgs.AttachedTransform)
            {
                _audioSource.transform.SetParent(_playArgs.AttachedTransform, false);
                _audioSource.transform.localPosition = Vector3.zero;
            }

            return true;
        }

        /// <summary>
        /// 释放资源。
        /// </summary>
        public void Release()
        {
            if (_audioSource != null)
            {
                _audioSource.Stop();
                _audioSource.clip = null;
                _audioSource.transform.SetParent(null);
                _audioSource.transform.position = Vector3.zero;
            }

            _playArgs = null;
            _isFading = false;
            _stopAfterFade = false;
            IsActive = false;
        }

        private void StartFade(float targetVolume, float duration, bool stopAfterFade)
        {
            _fadeStartVolume = _audioSource.volume;
            _fadeTargetVolume = Mathf.Clamp01(targetVolume);
            _fadeDuration = duration;
            _fadeTimer = 0f;
            _isFading = true;
            _stopAfterFade = stopAfterFade;
        }

        private void ForceStop()
        {
            if (_audioSource != null)
            {
                _audioSource.Stop();
            }

            _playArgs?.OnComplete?.Invoke(SoundId);
            Release();
        }
    }
}
