using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using EF.Common;
using EF.Resource;
using UnityEngine;
using YooAsset;

namespace EF.Sound
{
    /// <summary>
    /// 音频管理器，负责音频的播放、控制与资源管理。
    /// 针对长音频（BGM、对话）和短音效进行了优化处理。
    /// </summary>
    public sealed class SoundManager : AEFManager, ISoundManager
    {
        private SoundAgentPool _agentPool;
        private readonly Dictionary<string, AssetHandle> _cachedClips = new();
        private readonly Dictionary<SoundType, float> _typeVolumes = new();
        private const int DefaultInitialPoolSize = 10;
        private const int DefaultMaxPoolSize = 50;
        private IResourceManager _resourceManager;
        private int _initialPoolSize = DefaultInitialPoolSize;
        private int _maxPoolSize = DefaultMaxPoolSize;

        private float _masterVolume = 1f;

        /// <summary>
        /// 初始化音频管理器。
        /// </summary>
        public SoundManager(IResourceManager resourceManager)
        {
            _resourceManager = resourceManager ?? throw new ArgumentNullException(nameof(resourceManager));

            // 初始化各类型音量
            _typeVolumes[SoundType.Music] = 1f;
            _typeVolumes[SoundType.SoundEffect] = 1f;
            _typeVolumes[SoundType.Voice] = 1f;
            _typeVolumes[SoundType.Ambient] = 1f;
        }

        /// <inheritdoc />
        public int ActiveSoundCount => _agentPool?.ActiveCount ?? 0;

        /// <inheritdoc />
        public float MasterVolume
        {
            get => _masterVolume;
            set
            {
                _masterVolume = Mathf.Clamp01(value);
                UpdateAllVolumes();
            }
        }

        /// <inheritdoc />
        public float MusicVolume
        {
            get => _typeVolumes[SoundType.Music];
            set
            {
                _typeVolumes[SoundType.Music] = Mathf.Clamp01(value);
                UpdateTypeVolume(SoundType.Music);
            }
        }

        /// <inheritdoc />
        public float SoundEffectVolume
        {
            get => _typeVolumes[SoundType.SoundEffect];
            set
            {
                _typeVolumes[SoundType.SoundEffect] = Mathf.Clamp01(value);
                UpdateTypeVolume(SoundType.SoundEffect);
            }
        }

        /// <inheritdoc />
        public float VoiceVolume
        {
            get => _typeVolumes[SoundType.Voice];
            set
            {
                _typeVolumes[SoundType.Voice] = Mathf.Clamp01(value);
                UpdateTypeVolume(SoundType.Voice);
            }
        }

        /// <inheritdoc />
        public float AmbientVolume
        {
            get => _typeVolumes[SoundType.Ambient];
            set
            {
                _typeVolumes[SoundType.Ambient] = Mathf.Clamp01(value);
                UpdateTypeVolume(SoundType.Ambient);
            }
        }

        /// <summary>
        /// 设置资源管理器（用于延迟注入）。
        /// </summary>
        public void SetResourceManager(IResourceManager resourceManager)
        {
            _resourceManager = resourceManager ?? throw new ArgumentNullException(nameof(resourceManager));
        }

        /// <inheritdoc />
        public int Play(SoundPlayArgs args)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args), "播放参数不能为空");
            }

            if (string.IsNullOrEmpty(args.AssetName))
            {
                throw new ArgumentException("音频资源名称不能为空", nameof(args));
            }

            // 异步加载并播放
            PlayAsync(args).Forget();

            // 返回临时 ID（实际 ID 会在加载完成后分配）
            return -1;
        }

        /// <summary>
        /// 异步播放音频（推荐使用）。
        /// </summary>
        public async UniTask<int> PlayAsync(SoundPlayArgs args)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args), "播放参数不能为空");
            }

            if (string.IsNullOrEmpty(args.AssetName))
            {
                throw new ArgumentException("音频资源名称不能为空", nameof(args));
            }

            // 加载音频资源
            AudioClip clip = await LoadClipAsync(args.AssetName, args.SoundType);
            if (clip == null)
            {
                Debug.LogError($"加载音频失败: {args.AssetName}");
                return -1;
            }

            EnsureAgentPoolReady();

            // 获取代理并播放
            SoundAgent agent = _agentPool.Acquire();
            float typeVolume = GetFinalTypeVolume(args.SoundType);
            agent.Play(agent.SoundId, clip, args, typeVolume);

            return agent.SoundId;
        }

        /// <inheritdoc />
        public void Stop(int soundId, float fadeOutDuration = 0f)
        {
            if (_agentPool == null)
            {
                return;
            }

            SoundAgent agent = _agentPool.GetAgent(soundId);
            agent?.Stop(fadeOutDuration);
        }

        /// <inheritdoc />
        public void Pause(int soundId)
        {
            if (_agentPool == null)
            {
                return;
            }

            SoundAgent agent = _agentPool.GetAgent(soundId);
            agent?.Pause();
        }

        /// <inheritdoc />
        public void Resume(int soundId)
        {
            if (_agentPool == null)
            {
                return;
            }

            SoundAgent agent = _agentPool.GetAgent(soundId);
            agent?.Resume();
        }

        /// <inheritdoc />
        public void StopAll(SoundType soundType, float fadeOutDuration = 0f)
        {
            if (_agentPool == null)
            {
                return;
            }

            List<SoundAgent> agents = _agentPool.GetActiveAgents(soundType);
            foreach (SoundAgent agent in agents)
            {
                agent.Stop(fadeOutDuration);
            }
        }

        /// <inheritdoc />
        public void StopAll(float fadeOutDuration = 0f)
        {
            if (_agentPool == null)
            {
                return;
            }

            List<SoundAgent> agents = _agentPool.GetActiveAgents();
            foreach (SoundAgent agent in agents)
            {
                agent.Stop(fadeOutDuration);
            }
        }

        /// <inheritdoc />
        public void PauseAll(SoundType soundType)
        {
            if (_agentPool == null)
            {
                return;
            }

            List<SoundAgent> agents = _agentPool.GetActiveAgents(soundType);
            foreach (SoundAgent agent in agents)
            {
                agent.Pause();
            }
        }

        /// <inheritdoc />
        public void PauseAll()
        {
            if (_agentPool == null)
            {
                return;
            }

            List<SoundAgent> agents = _agentPool.GetActiveAgents();
            foreach (SoundAgent agent in agents)
            {
                agent.Pause();
            }
        }

        /// <inheritdoc />
        public void ResumeAll(SoundType soundType)
        {
            if (_agentPool == null)
            {
                return;
            }

            List<SoundAgent> agents = _agentPool.GetActiveAgents(soundType);
            foreach (SoundAgent agent in agents)
            {
                agent.Resume();
            }
        }

        /// <inheritdoc />
        public void ResumeAll()
        {
            if (_agentPool == null)
            {
                return;
            }

            List<SoundAgent> agents = _agentPool.GetActiveAgents();
            foreach (SoundAgent agent in agents)
            {
                agent.Resume();
            }
        }

        /// <inheritdoc />
        public void SetVolume(int soundId, float volume)
        {
            if (_agentPool == null)
            {
                return;
            }

            SoundAgent agent = _agentPool.GetAgent(soundId);
            agent?.SetVolume(volume);
        }

        /// <inheritdoc />
        public bool IsPlaying(int soundId)
        {
            if (_agentPool == null)
            {
                return false;
            }

            SoundAgent agent = _agentPool.GetAgent(soundId);
            return agent != null && agent.IsPlaying;
        }

        /// <inheritdoc />
        public float GetProgress(int soundId)
        {
            if (_agentPool == null)
            {
                return 0f;
            }

            SoundAgent agent = _agentPool.GetAgent(soundId);
            return agent?.GetProgress() ?? 0f;
        }

        /// <summary>
        /// 每帧更新。
        /// </summary>
        public override void Update(float elapseSeconds, float realElapseSeconds)
        {
            EnsureAgentPoolReady();
            _agentPool?.UpdateAgents(elapseSeconds);
        }

        /// <summary>
        /// 关闭音频管理器并释放资源。
        /// </summary>
        public override void Shutdown()
        {
            StopAll(0f);
            _agentPool?.Clear();
            _agentPool = null;

            // 释放缓存的音频资源
            foreach (AssetHandle handle in _cachedClips.Values)
            {
                handle?.Release();
            }

            _cachedClips.Clear();
        }

        /// <summary>
        /// 预加载音频资源。
        /// </summary>
        public async UniTask PreloadAsync(string assetName)
        {
            if (string.IsNullOrEmpty(assetName))
            {
                throw new ArgumentException("资源名称不能为空", nameof(assetName));
            }

            if (_cachedClips.ContainsKey(assetName))
            {
                return;
            }

            await LoadClipAsync(assetName, SoundType.SoundEffect);
        }

        /// <summary>
        /// 卸载指定音频资源。
        /// </summary>
        public void UnloadClip(string assetName)
        {
            if (string.IsNullOrEmpty(assetName))
            {
                return;
            }

            if (_cachedClips.TryGetValue(assetName, out AssetHandle handle))
            {
                handle?.Release();
                _cachedClips.Remove(assetName);
            }
        }

        private async UniTask<AudioClip> LoadClipAsync(string assetName, SoundType soundType)
        {
            // 检查缓存
            if (_cachedClips.TryGetValue(assetName, out AssetHandle cachedHandle))
            {
                return cachedHandle.AssetObject as AudioClip;
            }

            if (_resourceManager == null)
            {
                Debug.LogError("资源管理器未设置，无法加载音频");
                return null;
            }

            try
            {
                // 短音效完全加载到内存，长音频使用流式加载（通过 Unity 的 AudioClip Load Type 设置）
                AssetHandle handle = await _resourceManager.LoadAssetAsync<AudioClip>(assetName);

                if (handle == null || handle.AssetObject == null)
                {
                    Debug.LogError($"加载音频失败: {assetName}");
                    return null;
                }

                // 短音效缓存起来，长音频根据策略可选择性缓存
                if (soundType == SoundType.SoundEffect)
                {
                    _cachedClips[assetName] = handle;
                }
                else
                {
                    // 长音频也可以缓存，但需要注意内存占用
                    _cachedClips[assetName] = handle;
                }

                return handle.AssetObject as AudioClip;
            }
            catch (Exception ex)
            {
                Debug.LogError($"加载音频异常: {assetName}, {ex.Message}");
                return null;
            }
        }

        private float GetFinalTypeVolume(SoundType soundType)
        {
            return _masterVolume * _typeVolumes[soundType];
        }

        private void UpdateTypeVolume(SoundType soundType)
        {
            float typeVolume = GetFinalTypeVolume(soundType);
            if (_agentPool == null)
            {
                return;
            }

            List<SoundAgent> agents = _agentPool.GetActiveAgents(soundType);

            foreach (SoundAgent agent in agents)
            {
                agent.UpdateTypeVolume(typeVolume);
            }
        }

        private void UpdateAllVolumes()
        {
            if (_agentPool == null)
            {
                return;
            }

            foreach (SoundType soundType in Enum.GetValues(typeof(SoundType)))
            {
                UpdateTypeVolume(soundType);
            }
        }

        private void EnsureAgentPoolReady()
        {
            if (_agentPool != null)
            {
                return;
            }

            // 懒加载对象池，保证在 Unity 主线程创建 GameObject
            _agentPool = new SoundAgentPool(_initialPoolSize, _maxPoolSize);
        }
    }
}
