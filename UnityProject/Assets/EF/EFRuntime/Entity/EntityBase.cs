using UnityEngine;
using EF.Feature;

namespace EF.Entity
{
    /// <summary>
    /// 实体抽象基类，提供 IEntity 接口的默认实现。
    /// </summary>
    public abstract class EntityBase : IEntity
    {
        private int _id;
        private string _entityAssetName;
        private IEntityGroup _entityGroup;
        private readonly FeatureContainer _features;

        /// <summary>
        /// 实体的唯一标识符。
        /// </summary>
        public int Id => _id;

        /// <summary>
        /// 实体资源名称。
        /// </summary>
        public string EntityAssetName => _entityAssetName;

        /// <summary>
        /// 实体关联的 GameObject。
        /// </summary>
        public abstract GameObject Handle { get;  set; }

        /// <summary>
        /// 实体所属的实体组。
        /// </summary>
        public IEntityGroup EntityGroup => _entityGroup;

        /// <summary>
        /// 获取实体的特性容器。
        /// </summary>
        public IFeatureContainer Features => _features;

        /// <summary>
        /// 初始化 EntityBase 的新实例。
        /// </summary>
        protected EntityBase()
        {
            _features = new FeatureContainer(this);
        }

        /// <summary>
        /// 初始化实体。
        /// </summary>
        /// <param name="entityId">实体唯一标识符。</param>
        /// <param name="entityAssetName">实体资源名称。</param>
        /// <param name="entityGroup">实体所属组。</param>
        /// <param name="isNewInstance">是否为新创建的实例。</param>
        /// <param name="userData">用户自定义数据。</param>
        public virtual void OnInit(int entityId, string entityAssetName, IEntityGroup entityGroup, bool isNewInstance, object userData)
        {
            _id = entityId;
            _entityAssetName = entityAssetName;
            _entityGroup = entityGroup;
        }

        /// <summary>
        /// 实体被回收时调用。
        /// </summary>
        public virtual void OnRecycle()
        {
        }

        /// <summary>
        /// 实体显示时调用。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        public virtual void OnShow(object userData)
        {
        }

        /// <summary>
        /// 实体隐藏时调用。
        /// </summary>
        /// <param name="isShutdown">是否为关闭操作。</param>
        /// <param name="userData">用户自定义数据。</param>
        public virtual void OnHide(bool isShutdown, object userData)
        {
        }

        /// <summary>
        /// 子实体附加到本实体时调用。
        /// </summary>
        /// <param name="childEntity">被附加的子实体。</param>
        /// <param name="userData">用户自定义数据。</param>
        public virtual void OnAttached(IEntity childEntity, object userData)
        {
        }

        /// <summary>
        /// 子实体从本实体分离时调用。
        /// </summary>
        /// <param name="childEntity">被分离的子实体。</param>
        /// <param name="userData">用户自定义数据。</param>
        public virtual void OnDetached(IEntity childEntity, object userData)
        {
        }

        /// <summary>
        /// 本实体附加到父实体时调用。
        /// </summary>
        /// <param name="parentEntity">被附加的父实体。</param>
        /// <param name="userData">用户自定义数据。</param>
        public virtual void OnAttachTo(IEntity parentEntity, object userData)
        {
        }

        /// <summary>
        /// 本实体从父实体分离时调用。
        /// </summary>
        /// <param name="parentEntity">被分离的父实体。</param>
        /// <param name="userData">用户自定义数据。</param>
        public virtual void OnDetachFrom(IEntity parentEntity, object userData)
        {
        }

        /// <summary>
        /// 实体更新。
        /// </summary>
        /// <param name="elapseSeconds">逻辑流逝时间（秒）。</param>
        /// <param name="realElapseSeconds">真实流逝时间（秒）。</param>
        public virtual void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
            // 更新所有已启用的特性
            _features.Update(elapseSeconds, realElapseSeconds);
        }
    }
}
