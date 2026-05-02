using System.Collections.Generic;
using EF.Common;
using EF.Debugger;

namespace EF.Save
{
    /// <summary>
    /// 本地保存管理器实现。
    /// </summary>
    public class SaveManager : AEFManager, ISaveManager
    {
        private readonly Dictionary<SaveStrategyType, ISaveStrategy> _strategies;
        private ISaveStrategy _currentStrategy;
        private SaveStrategyType _currentStrategyType;

        public SaveStrategyType CurrentStrategyType => _currentStrategyType;

        public SaveManager()
        {
            _strategies = new Dictionary<SaveStrategyType, ISaveStrategy>
            {
                { SaveStrategyType.Json, new JsonSaveStrategy() },
                { SaveStrategyType.PlayerPrefs, new PlayerPrefsSaveStrategy() }
            };

            // 默认使用 Json 策略
            SetSaveStrategy(SaveStrategyType.Json);
            Log.Info("[SaveManager] 初始化完成，默认使用 Json 保存策略");
        }

        public void SetSaveStrategy(SaveStrategyType strategyType)
        {
            if (_strategies.TryGetValue(strategyType, out var strategy))
            {
                _currentStrategy = strategy;
                _currentStrategyType = strategyType;
                Log.Info($"[SaveManager] 切换保存策略: {strategyType}");
            }
            else
            {
                Log.Error($"[SaveManager] 不支持的保存策略: {strategyType}");
            }
        }

        public bool Save<T>(string key, T data)
        {
            if (_currentStrategy == null)
            {
                Log.Error("[SaveManager] 当前保存策略未设置");
                return false;
            }

            return _currentStrategy.Save(key, data);
        }

        public T Load<T>(string key, T defaultValue = default)
        {
            if (_currentStrategy == null)
            {
                Log.Error("[SaveManager] 当前保存策略未设置");
                return defaultValue;
            }

            return _currentStrategy.Load(key, defaultValue);
        }

        public bool HasKey(string key)
        {
            if (_currentStrategy == null)
            {
                Log.Error("[SaveManager] 当前保存策略未设置");
                return false;
            }

            return _currentStrategy.HasKey(key);
        }

        public bool Delete(string key)
        {
            if (_currentStrategy == null)
            {
                Log.Error("[SaveManager] 当前保存策略未设置");
                return false;
            }

            return _currentStrategy.Delete(key);
        }

        public void DeleteAll()
        {
            if (_currentStrategy == null)
            {
                Log.Error("[SaveManager] 当前保存策略未设置");
                return;
            }

            _currentStrategy.DeleteAll();
        }

        public override void Shutdown()
        {
            _strategies.Clear();
            _currentStrategy = null;
            Log.Info("[SaveManager] 已关闭");
        }
    }
}
