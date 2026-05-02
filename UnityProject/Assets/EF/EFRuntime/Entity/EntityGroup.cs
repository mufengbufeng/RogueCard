using System;
using EF.ObjectPool;

namespace EF.Entity
{
    /// <summary>
    /// 实体组实现类，管理同类型实体的对象池。
    /// </summary>
    public sealed class EntityGroup : IEntityGroup
    {
        private readonly IObjectPool<IEntity> _entityPool;

        /// <summary>
        /// 实体组名称。
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 当前实体组中的实体数量。
        /// </summary>
        public int EntityCount => _entityPool.TotalCount;

        /// <summary>
        /// 实体组使用的对象池。
        /// </summary>
        public IObjectPool<IEntity> EntityPool => _entityPool;

        /// <summary>
        /// 初始化实体组。
        /// </summary>
        /// <param name="name">实体组名称。</param>
        /// <param name="entityPool">实体对象池。</param>
        public EntityGroup(string name, IObjectPool<IEntity> entityPool)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("实体组名称不能为空", nameof(name));
            }

            Name = name;
            _entityPool = entityPool ?? throw new ArgumentNullException(nameof(entityPool));
        }

        /// <summary>
        /// 从对象池中生成一个实体。
        /// </summary>
        /// <returns>生成的实体实例。</returns>
        public IEntity SpawnEntity()
        {
            return _entityPool.Spawn();
        }

        /// <summary>
        /// 回收实体到对象池。
        /// </summary>
        /// <param name="entity">需要回收的实体。</param>
        public void RecycleEntity(IEntity entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            _entityPool.Recycle(entity);
        }

        /// <summary>
        /// 释放实体组及其所有实体。
        /// </summary>
        public void Release()
        {
            _entityPool.Shutdown();
        }
    }
}
