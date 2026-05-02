using System;
using System.Collections.Generic;
using UnityEngine;

namespace EF.Sound
{
    /// <summary>
    /// 音频代理对象池，管理 AudioSource 的复用与扩展。
    /// </summary>
    internal sealed class SoundAgentPool
    {
        private readonly GameObject _poolRoot;
        private readonly Queue<SoundAgent> _availableAgents = new();
        private readonly List<SoundAgent> _activeAgents = new();
        private readonly Dictionary<int, SoundAgent> _soundIdMap = new();

        private int _nextSoundId = 1;
        private int _initialPoolSize = 10;
        private int _maxPoolSize = 50;

        /// <summary>
        /// 当前可用代理数量。
        /// </summary>
        public int AvailableCount => _availableAgents.Count;

        /// <summary>
        /// 当前活跃代理数量。
        /// </summary>
        public int ActiveCount => _activeAgents.Count;

        /// <summary>
        /// 初始化对象池。
        /// </summary>
        public SoundAgentPool(int initialSize = 10, int maxSize = 50)
        {
            _initialPoolSize = Math.Max(1, initialSize);
            _maxPoolSize = Math.Max(_initialPoolSize, maxSize);

            _poolRoot = new GameObject("[SoundAgentPool]");
            GameObject.DontDestroyOnLoad(_poolRoot);

            // 预创建初始数量的代理
            for (int i = 0; i < _initialPoolSize; i++)
            {
                SoundAgent agent = CreateAgent();
                _availableAgents.Enqueue(agent);
            }
        }

        /// <summary>
        /// 获取一个可用的音频代理。
        /// </summary>
        public SoundAgent Acquire()
        {
            SoundAgent agent;

            if (_availableAgents.Count > 0)
            {
                agent = _availableAgents.Dequeue();
            }
            else if (_activeAgents.Count + _availableAgents.Count < _maxPoolSize)
            {
                agent = CreateAgent();
            }
            else
            {
                // 达到最大限制，复用优先级最低的音频代理
                agent = FindLowestPriorityAgent();
                if (agent != null)
                {
                    agent.Stop(0f);
                    _activeAgents.Remove(agent);
                    _soundIdMap.Remove(agent.SoundId);
                }
                else
                {
                    throw new InvalidOperationException($"音频代理池已达到最大限制 ({_maxPoolSize})，且无法找到可回收的代理");
                }
            }

            int soundId = _nextSoundId++;
            _activeAgents.Add(agent);
            _soundIdMap[soundId] = agent;

            return agent;
        }

        /// <summary>
        /// 归还音频代理到池中。
        /// </summary>
        public void Release(SoundAgent agent)
        {
            if (agent == null)
            {
                return;
            }

            if (_activeAgents.Remove(agent))
            {
                _soundIdMap.Remove(agent.SoundId);
                agent.Release();
                _availableAgents.Enqueue(agent);
            }
        }

        /// <summary>
        /// 根据 SoundId 获取音频代理。
        /// </summary>
        public SoundAgent GetAgent(int soundId)
        {
            return _soundIdMap.TryGetValue(soundId, out SoundAgent agent) ? agent : null;
        }

        /// <summary>
        /// 获取指定类型的所有活跃代理。
        /// </summary>
        public List<SoundAgent> GetActiveAgents(SoundType? soundType = null)
        {
            if (!soundType.HasValue)
            {
                return new List<SoundAgent>(_activeAgents);
            }

            List<SoundAgent> result = new();
            foreach (SoundAgent agent in _activeAgents)
            {
                if (agent.SoundType == soundType.Value)
                {
                    result.Add(agent);
                }
            }

            return result;
        }

        /// <summary>
        /// 更新所有活跃代理。
        /// </summary>
        public void UpdateAgents(float deltaTime)
        {
            // 使用倒序遍历，因为可能在更新过程中释放代理
            for (int i = _activeAgents.Count - 1; i >= 0; i--)
            {
                SoundAgent agent = _activeAgents[i];
                if (!agent.Update(deltaTime))
                {
                    Release(agent);
                }
            }
        }

        /// <summary>
        /// 清空对象池。
        /// </summary>
        public void Clear()
        {
            foreach (SoundAgent agent in _activeAgents)
            {
                agent.Release();
            }

            _activeAgents.Clear();
            _soundIdMap.Clear();
            _availableAgents.Clear();

            if (_poolRoot != null)
            {
                GameObject.Destroy(_poolRoot);
            }
        }

        private SoundAgent CreateAgent()
        {
            GameObject agentObj = new GameObject($"SoundAgent_{_nextSoundId}");
            agentObj.transform.SetParent(_poolRoot.transform);

            AudioSource audioSource = agentObj.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;

            SoundAgent agent = new SoundAgent();
            agent.Initialize(audioSource);

            return agent;
        }

        private SoundAgent FindLowestPriorityAgent()
        {
            SoundAgent lowestPriorityAgent = null;
            // int lowestPriority = int.MinValue;

            foreach (SoundAgent agent in _activeAgents)
            {
                // 优先级数值越大，优先级越低（Unity AudioSource 的优先级规则）
                // 注意：SoundEffect 类型可以被抢占，而 Music/Voice/Ambient 不应该被抢占
                if (agent.SoundType == SoundType.SoundEffect)
                {
                    // 这里可以扩展更复杂的优先级逻辑
                    if (lowestPriorityAgent == null)
                    {
                        lowestPriorityAgent = agent;
                        // lowestPriority = 0; // 简化处理
                    }
                }
            }

            return lowestPriorityAgent;
        }
    }
}
