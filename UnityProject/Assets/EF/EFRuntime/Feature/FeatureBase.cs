using EF.Entity;

namespace EF.Feature
{
    /// <summary>
    /// 特性抽象基类，提供 IFeature 接口的默认实现。
    /// 特性可以包含数据和逻辑，支持生命周期回调。
    /// </summary>
    public abstract class FeatureBase : IFeature
    {
        /// <summary>
        /// 获取特性所属的实体。
        /// </summary>
        public IEntity Entity { get; private set; }

        /// <summary>
        /// 获取或设置特性是否启用。
        /// 禁用的特性不会触发 OnEnable、OnDisable 和 OnUpdate 回调。
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 获取特性是否已初始化。
        /// </summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// 特性初始化时调用，仅在特性首次添加到实体时调用一次。
        /// </summary>
        public virtual void OnInit()
        {
            IsInitialized = true;
        }

        /// <summary>
        /// 特性启用时调用，当实体显示或特性被启用时触发。
        /// </summary>
        public virtual void OnEnable()
        {
        }

        /// <summary>
        /// 特性禁用时调用，当实体隐藏或特性被禁用时触发。
        /// </summary>
        public virtual void OnDisable()
        {
        }

        /// <summary>
        /// 特性销毁时调用，当实体回收或特性被移除时触发。
        /// </summary>
        public virtual void OnDestroy()
        {
            IsInitialized = false;
        }

        /// <summary>
        /// 特性更新时调用，每帧更新仅在特性启用时触发。
        /// </summary>
        /// <param name="elapseSeconds">逻辑流逝时间（秒）。</param>
        /// <param name="realElapseSeconds">真实流逝时间（秒）。</param>
        public virtual void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
        }

        /// <summary>
        /// 接收外部传入的配置数据，在 OnInit 之前调用。默认空实现。
        /// </summary>
        /// <param name="data">配置数据对象。</param>
        public virtual void OnSetup(object data)
        {
        }

        /// <summary>
        /// 设置特性所属的实体（由特性容器内部调用）。
        /// </summary>
        /// <param name="entity">所属的实体。</param>
        internal void SetEntity(IEntity entity)
        {
            Entity = entity;
        }
    }
}
