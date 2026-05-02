using EF.ObjectPool;

namespace EF.Entity
{
    /// <summary>
    /// 定义实体组的能力，管理同类型实体的对象池。
    /// </summary>
    public interface IEntityGroup
    {
        /// <summary>
        /// 实体组名称。
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 当前实体组中的实体数量（包含使用中和空闲）。
        /// </summary>
        int EntityCount { get; }

        /// <summary>
        /// 实体组使用的对象池。
        /// </summary>
        IObjectPool<IEntity> EntityPool { get; }

        /// <summary>
        /// 从对象池中生成一个实体。
        /// </summary>
        /// <returns>生成的实体实例。</returns>
        IEntity SpawnEntity();

        /// <summary>
        /// 回收实体到对象池。
        /// </summary>
        /// <param name="entity">需要回收的实体。</param>
        void RecycleEntity(IEntity entity);

        /// <summary>
        /// 释放实体组及其所有实体。
        /// </summary>
        void Release();
    }
}
