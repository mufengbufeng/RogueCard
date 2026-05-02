using EF.Entity;

namespace EF.Feature
{
    /// <summary>
    /// 特性接口，定义特性的基础能力。
    /// </summary>
    public interface IFeature
    {
        /// <summary>
        /// 获取或设置特性是否启用。
        /// </summary>
        bool Enabled { get; set; }

        /// <summary>
        /// 获取特性所属的实体。
        /// </summary>
        IEntity Entity { get; }

        /// <summary>
        /// 获取特性是否已初始化。
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// 特性初始化时调用，仅在特性首次添加到实体时调用一次。
        /// </summary>
        void OnInit();

        /// <summary>
        /// 特性启用时调用，当实体显示或特性被启用时触发。
        /// </summary>
        void OnEnable();

        /// <summary>
        /// 特性禁用时调用，当实体隐藏或特性被禁用时触发。
        /// </summary>
        void OnDisable();

        /// <summary>
        /// 特性销毁时调用，当实体回收或特性被移除时触发。
        /// </summary>
        void OnDestroy();

        /// <summary>
        /// 特性更新时调用，每帧更新仅在特性启用时触发。
        /// </summary>
        /// <param name="elapseSeconds">逻辑流逝时间（秒）。</param>
        /// <param name="realElapseSeconds">真实流逝时间（秒）。</param>
        void OnUpdate(float elapseSeconds, float realElapseSeconds);

        /// <summary>
        /// 接收外部传入的配置数据，在 OnInit 之前调用。
        /// </summary>
        /// <param name="data">配置数据对象。</param>
        void OnSetup(object data);
    }
}
