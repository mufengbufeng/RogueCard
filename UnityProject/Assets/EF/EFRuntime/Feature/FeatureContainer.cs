using System;
using System.Collections.Generic;
using System.Linq;
using EF.Entity;

namespace EF.Feature
{
    /// <summary>
    /// 特性容器实现类，管理实体上的所有特性。
    /// </summary>
    internal sealed class FeatureContainer : IFeatureContainer
    {
        private readonly IEntity _owner;
        private readonly Dictionary<Type, List<IFeature>> _features;
        private readonly List<IFeature> _allFeatures;

        /// <summary>
        /// 初始化 FeatureContainer 的新实例。
        /// </summary>
        /// <param name="owner">特性容器所属的实体。</param>
        public FeatureContainer(IEntity owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _features = new Dictionary<Type, List<IFeature>>();
            _allFeatures = new List<IFeature>();
        }

        /// <summary>
        /// 获取特性数量。
        /// </summary>
        public int FeatureCount => _allFeatures.Count;

        /// <summary>
        /// 获取特性容器所属的实体。
        /// </summary>
        public IEntity Owner => _owner;

        /// <summary>
        /// 添加指定类型的特性。
        /// </summary>
        public T AddFeature<T>() where T : IFeature, new()
        {
            return (T)AddFeature(typeof(T), null);
        }

        /// <summary>
        /// 添加指定类型的特性并传入配置数据。
        /// </summary>
        public T AddFeature<T>(object data) where T : IFeature, new()
        {
            return (T)AddFeature(typeof(T), data);
        }

        /// <summary>
        /// 添加指定类型的特性。
        /// </summary>
        public IFeature AddFeature(Type featureType)
        {
            return AddFeature(featureType, null);
        }

        /// <summary>
        /// 添加指定类型的特性并传入配置数据。
        /// </summary>
        public IFeature AddFeature(Type featureType, object data)
        {
            if (featureType == null)
            {
                throw new ArgumentNullException(nameof(featureType));
            }

            if (!typeof(IFeature).IsAssignableFrom(featureType))
            {
                throw new ArgumentException($"Type '{featureType.Name}' must implement IFeature interface.", nameof(featureType));
            }

            // 检查依赖关系
            CheckRequiredFeatures(featureType);

            // 检查是否允许多实例
            bool allowMultiple = AllowsMultipleInstances(featureType);

            if (!allowMultiple && _features.ContainsKey(featureType))
            {
                throw new InvalidOperationException($"Feature of type '{featureType.Name}' already exists and does not allow multiple instances.");
            }

            // 创建特性实例
            IFeature feature;
            try
            {
                feature = (IFeature)Activator.CreateInstance(featureType);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to create instance of feature type '{featureType.Name}'. Ensure it has a parameterless constructor.", ex);
            }

            // 设置所属实体
            if (feature is FeatureBase featureBase)
            {
                featureBase.SetEntity(_owner);
            }

            // 添加到存储
            if (!_features.ContainsKey(featureType))
            {
                _features[featureType] = new List<IFeature>();
            }
            _features[featureType].Add(feature);
            _allFeatures.Add(feature);

            // 传入配置数据（在 OnInit 之前）
            feature.OnSetup(data);

            // 初始化特性
            feature.OnInit();

            return feature;
        }

        /// <summary>
        /// 获取指定类型的特性。
        /// </summary>
        public T GetFeature<T>() where T : IFeature
        {
            return (T)GetFeature(typeof(T));
        }

        /// <summary>
        /// 获取指定类型的特性。
        /// </summary>
        public IFeature GetFeature(Type featureType)
        {
            if (featureType == null)
            {
                return null;
            }

            if (_features.TryGetValue(featureType, out var featureList) && featureList.Count > 0)
            {
                return featureList[0];
            }

            return null;
        }

        /// <summary>
        /// 获取指定类型的所有特性。
        /// </summary>
        public T[] GetFeatures<T>() where T : IFeature
        {
            if (_features.TryGetValue(typeof(T), out var featureList))
            {
                return featureList.Cast<T>().ToArray();
            }

            return Array.Empty<T>();
        }

        /// <summary>
        /// 判断是否包含指定类型的特性。
        /// </summary>
        public bool HasFeature<T>() where T : IFeature
        {
            return HasFeature(typeof(T));
        }

        /// <summary>
        /// 判断是否包含指定类型的特性。
        /// </summary>
        public bool HasFeature(Type featureType)
        {
            if (featureType == null)
            {
                return false;
            }

            return _features.ContainsKey(featureType) && _features[featureType].Count > 0;
        }

        /// <summary>
        /// 移除指定类型的特性。
        /// </summary>
        public bool RemoveFeature<T>() where T : IFeature
        {
            return RemoveFeature(typeof(T));
        }

        /// <summary>
        /// 移除指定类型的特性（移除第一个）。
        /// </summary>
        private bool RemoveFeature(Type featureType)
        {
            if (featureType == null)
            {
                return false;
            }

            if (!_features.TryGetValue(featureType, out var featureList) || featureList.Count == 0)
            {
                return false;
            }

            IFeature feature = featureList[0];
            return RemoveFeature(feature);
        }

        /// <summary>
        /// 移除指定的特性实例。
        /// </summary>
        public bool RemoveFeature(IFeature feature)
        {
            if (feature == null)
            {
                return false;
            }

            Type featureType = feature.GetType();

            if (!_features.TryGetValue(featureType, out var featureList))
            {
                return false;
            }

            if (!featureList.Remove(feature))
            {
                return false;
            }

            // 如果列表为空，移除该类型的键
            if (featureList.Count == 0)
            {
                _features.Remove(featureType);
            }

            _allFeatures.Remove(feature);

            // 销毁特性
            feature.OnDestroy();

            return true;
        }

        /// <summary>
        /// 设置指定类型特性的启用状态。
        /// </summary>
        public void SetFeatureEnabled<T>(bool enabled) where T : IFeature
        {
            IFeature feature = GetFeature<T>();
            if (feature != null)
            {
                bool wasEnabled = feature.Enabled;
                feature.Enabled = enabled;

                if (feature is FeatureBase featureBase)
                {
                    if (enabled && !wasEnabled)
                    {
                        featureBase.OnEnable();
                    }
                    else if (!enabled && wasEnabled)
                    {
                        featureBase.OnDisable();
                    }
                }
            }
        }

        /// <summary>
        /// 向已注册的特性传递配置数据。
        /// </summary>
        public void SetupFeature<T>(object data) where T : IFeature
        {
            IFeature feature = GetFeature<T>();
            if (feature != null)
            {
                feature.OnSetup(data);
            }
        }

        /// <summary>
        /// 获取所有特性的只读列表。
        /// </summary>
        public IReadOnlyList<IFeature> GetAllFeatures()
        {
            return _allFeatures;
        }

        /// <summary>
        /// 更新所有已启用的特性。
        /// </summary>
        public void Update(float elapseSeconds, float realElapseSeconds)
        {
            foreach (IFeature feature in _allFeatures)
            {
                if (feature.Enabled)
                {
                    feature.OnUpdate(elapseSeconds, realElapseSeconds);
                }
            }
        }

        /// <summary>
        /// 检查特性的依赖特性。
        /// </summary>
        private void CheckRequiredFeatures(Type featureType)
        {
            RequireFeatureAttribute[] attributes = (RequireFeatureAttribute[])featureType.GetCustomAttributes(typeof(RequireFeatureAttribute), false);

            foreach (RequireFeatureAttribute attribute in attributes)
            {
                Type requiredType = attribute.RequiredType;
                if (!HasFeature(requiredType))
                {
                    throw new InvalidOperationException($"Cannot add feature '{featureType.Name}' because required feature '{requiredType.Name}' is not present on entity '{_owner.EntityAssetName}' (Id: {_owner.Id}).");
                }
            }
        }

        /// <summary>
        /// 判断特性类型是否允许多实例。
        /// </summary>
        private bool AllowsMultipleInstances(Type featureType)
        {
            AllowMultipleAttribute attribute = (AllowMultipleAttribute)featureType.GetCustomAttributes(typeof(AllowMultipleAttribute), false).FirstOrDefault();
            return attribute != null && attribute.AllowMultiple;
        }
    }
}
