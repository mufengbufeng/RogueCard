using UnityEngine;
using EF.Feature;

namespace EF.Entity
{
    /// <summary>
    /// 定义实体的生命周期和层级关系回调。
    /// </summary>
    public interface IEntity
    {
        /// <summary>
        /// 获取实体的特性容器。
        /// </summary>
        IFeatureContainer Features { get; }
        /// <summary>
        /// 实体的唯一标识符。
        /// </summary>
        int Id { get; }

        /// <summary>
        /// 实体资源名称。
        /// </summary>
        string EntityAssetName { get; }

        /// <summary>
        /// 实体关联的 GameObject。
        /// </summary>
        GameObject Handle { get; }

        /// <summary>
        /// 实体所属的实体组。
        /// </summary>
        IEntityGroup EntityGroup { get; }

        /// <summary>
        /// 初始化实体。
        /// </summary>
        /// <param name="entityId">实体唯一标识符。</param>
        /// <param name="entityAssetName">实体资源名称。</param>
        /// <param name="entityGroup">实体所属组。</param>
        /// <param name="isNewInstance">是否为新创建的实例。</param>
        /// <param name="userData">用户自定义数据。</param>
        void OnInit(int entityId, string entityAssetName, IEntityGroup entityGroup, bool isNewInstance, object userData);

        /// <summary>
        /// 实体被回收时调用。
        /// </summary>
        void OnRecycle();

        /// <summary>
        /// 实体显示时调用。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        void OnShow(object userData);

        /// <summary>
        /// 实体隐藏时调用。
        /// </summary>
        /// <param name="isShutdown">是否为关闭操作。</param>
        /// <param name="userData">用户自定义数据。</param>
        void OnHide(bool isShutdown, object userData);

        /// <summary>
        /// 子实体附加到本实体时调用。
        /// </summary>
        /// <param name="childEntity">被附加的子实体。</param>
        /// <param name="userData">用户自定义数据。</param>
        void OnAttached(IEntity childEntity, object userData);

        /// <summary>
        /// 子实体从本实体分离时调用。
        /// </summary>
        /// <param name="childEntity">被分离的子实体。</param>
        /// <param name="userData">用户自定义数据。</param>
        void OnDetached(IEntity childEntity, object userData);

        /// <summary>
        /// 本实体附加到父实体时调用。
        /// </summary>
        /// <param name="parentEntity">被附加的父实体。</param>
        /// <param name="userData">用户自定义数据。</param>
        void OnAttachTo(IEntity parentEntity, object userData);

        /// <summary>
        /// 本实体从父实体分离时调用。
        /// </summary>
        /// <param name="parentEntity">被分离的父实体。</param>
        /// <param name="userData">用户自定义数据。</param>
        void OnDetachFrom(IEntity parentEntity, object userData);

        /// <summary>
        /// 实体更新。
        /// </summary>
        /// <param name="elapseSeconds">逻辑流逝时间（秒）。</param>
        /// <param name="realElapseSeconds">真实流逝时间（秒）。</param>
        void OnUpdate(float elapseSeconds, float realElapseSeconds);
    }
}
