using System;

namespace EF.Entity
{
    /// <summary>
    /// 实体组的运行配置。
    /// </summary>
    public sealed class EntityGroupOptions
    {
        /// <summary>
        /// 自定义实体工厂方法。
        /// 若为 null，则使用默认的 DefaultEntity。
        /// </summary>
        public Func<IEntity> EntityFactory { get; set; }

        /// <summary>
        /// 实体组中实体实例的优先级。
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// 是否自动释放长时间未使用的实体。
        /// </summary>
        public bool AutoRelease { get; set; } = true;

        /// <summary>
        /// 实体池容量上限，超过后将优先释放空闲实体。
        /// </summary>
        public int Capacity { get; set; } = int.MaxValue;

        /// <summary>
        /// 实体空闲多久后视为过期（秒）。
        /// </summary>
        public float ExpireTime { get; set; } = 60f;

        /// <summary>
        /// 自动释放的检测间隔（秒）。
        /// </summary>
        public float AutoReleaseInterval { get; set; } = 5f;

        /// <summary>
        /// 是否允许同一个实体被多次取出（引用计数方式）。
        /// </summary>
        public bool AllowMultiSpawn { get; set; }

        /// <summary>
        /// 实体对象池的容量上限。
        /// </summary>
        public int PoolCapacity { get; set; } = int.MaxValue;

        /// <summary>
        /// 克隆配置，避免外部修改内部状态。
        /// </summary>
        public EntityGroupOptions Clone()
        {
            return (EntityGroupOptions)MemberwiseClone();
        }
    }
}
