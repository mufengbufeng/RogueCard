using System.Collections.Generic;
using EF.Entity;

namespace EF.Feature
{
    /// <summary>
    /// 特性容器接口，管理实体上的所有特性。
    /// </summary>
    public interface IFeatureContainer
    {
        /// <summary>
        /// 获取特性数量。
        /// </summary>
        int FeatureCount { get; }

        /// <summary>
        /// 获取特性容器所属的实体。
        /// </summary>
        IEntity Owner { get; }

        /// <summary>
        /// 添加指定类型的特性。
        /// </summary>
        /// <typeparam name="T">特性类型，必须实现 IFeature 接口且有无参构造函数。</typeparam>
        /// <returns>添加的特性实例。</returns>
        T AddFeature<T>() where T : IFeature, new();

        /// <summary>
        /// 添加指定类型的特性并传入配置数据。
        /// </summary>
        /// <typeparam name="T">特性类型，必须实现 IFeature 接口且有无参构造函数。</typeparam>
        /// <param name="data">配置数据，在 OnInit 之前传递给 OnSetup。</param>
        /// <returns>添加的特性实例。</returns>
        T AddFeature<T>(object data) where T : IFeature, new();

        /// <summary>
        /// 添加指定类型的特性。
        /// </summary>
        /// <param name="featureType">特性类型，必须实现 IFeature 接口。</param>
        /// <returns>添加的特性实例。</returns>
        IFeature AddFeature(System.Type featureType);

        /// <summary>
        /// 获取指定类型的特性。
        /// </summary>
        /// <typeparam name="T">特性类型。</typeparam>
        /// <returns>特性实例，如果不存在则返回 default(T)。</returns>
        T GetFeature<T>() where T : IFeature;

        /// <summary>
        /// 获取指定类型的特性。
        /// </summary>
        /// <param name="featureType">特性类型。</param>
        /// <returns>特性实例，如果不存在则返回 null。</returns>
        IFeature GetFeature(System.Type featureType);

        /// <summary>
        /// 获取指定类型的所有特性（用于允许多实例的特性）。
        /// </summary>
        /// <typeparam name="T">特性类型。</typeparam>
        /// <returns>特性数组，如果不存在则返回空数组。</returns>
        T[] GetFeatures<T>() where T : IFeature;

        /// <summary>
        /// 判断是否包含指定类型的特性。
        /// </summary>
        /// <typeparam name="T">特性类型。</typeparam>
        /// <returns>如果包含该类型的特性则返回 true，否则返回 false。</returns>
        bool HasFeature<T>() where T : IFeature;

        /// <summary>
        /// 判断是否包含指定类型的特性。
        /// </summary>
        /// <param name="featureType">特性类型。</param>
        /// <returns>如果包含该类型的特性则返回 true，否则返回 false。</returns>
        bool HasFeature(System.Type featureType);

        /// <summary>
        /// 移除指定类型的特性。
        /// </summary>
        /// <typeparam name="T">特性类型。</typeparam>
        /// <returns>如果成功移除则返回 true，如果不存在该特性则返回 false。</returns>
        bool RemoveFeature<T>() where T : IFeature;

        /// <summary>
        /// 移除指定的特性实例。
        /// </summary>
        /// <param name="feature">要移除的特性实例。</param>
        /// <returns>如果成功移除则返回 true，如果该特性不属于此容器则返回 false。</returns>
        bool RemoveFeature(IFeature feature);

        /// <summary>
        /// 设置指定类型特性的启用状态。
        /// </summary>
        /// <typeparam name="T">特性类型。</typeparam>
        /// <param name="enabled">是否启用。</param>
        void SetFeatureEnabled<T>(bool enabled) where T : IFeature;

        /// <summary>
        /// 向已注册的特性传递配置数据。
        /// </summary>
        /// <typeparam name="T">特性类型。</typeparam>
        /// <param name="data">配置数据。</param>
        void SetupFeature<T>(object data) where T : IFeature;

        /// <summary>
        /// 获取所有特性的只读列表。
        /// </summary>
        /// <returns>所有特性的只读列表。</returns>
        IReadOnlyList<IFeature> GetAllFeatures();
    }
}
