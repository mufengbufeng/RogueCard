using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using EF.Common;
using EF.ObjectPool;
using EF.Resource;
using UnityEngine;
using YooAsset;

namespace EF.Entity
{
    /// <summary>
    /// 实体管理器实现类。
    /// </summary>
    public sealed class EntityManager : AEFManager, IEntityManager
    {
        private static readonly int[] EmptyIntArray = Array.Empty<int>();

        private IObjectPoolManager _objectPoolManager;
        private IResourceManager _resourceManager;
        private IEntityHelper _entityHelper;
        private readonly Dictionary<string, IEntityGroup> _entityGroups;
        private readonly Dictionary<int, IEntity> _entities;
        private readonly Dictionary<int, int> _childEntityIndices;
        private readonly Dictionary<int, List<int>> _parentEntityIndices;
        private readonly Dictionary<string, AssetHandle> _assetHandleCache;
        private int _serialId;
        private Queue<IEntity> _entityUpdateQueue;

        /// <summary>
        /// 当前已加载的实体数量。
        /// </summary>
        public int EntityCount => _entities.Count;

        /// <summary>
        /// 当前实体组数量。
        /// </summary>
        public int EntityGroupCount => _entityGroups.Count;

        /// <summary>
        /// 初始化实体管理器。
        /// </summary>
        public EntityManager()
        {
            _entityGroups = new Dictionary<string, IEntityGroup>(StringComparer.Ordinal);
            _entities = new Dictionary<int, IEntity>();
            _childEntityIndices = new Dictionary<int, int>();
            _parentEntityIndices = new Dictionary<int, List<int>>();
            _assetHandleCache = new Dictionary<string, AssetHandle>(StringComparer.Ordinal);
            _entityUpdateQueue = new Queue<IEntity>();
        }

        /// <summary>
        /// 设置对象池管理器。
        /// </summary>
        /// <param name="objectPoolManager">对象池管理器。</param>
        public void SetObjectPoolManager(IObjectPoolManager objectPoolManager)
        {
            _objectPoolManager = objectPoolManager ?? throw new ArgumentNullException(nameof(objectPoolManager));
        }

        /// <summary>
        /// 设置资源管理器。
        /// </summary>
        /// <param name="resourceManager">资源管理器。</param>
        public void SetResourceManager(IResourceManager resourceManager)
        {
            _resourceManager = resourceManager ?? throw new ArgumentNullException(nameof(resourceManager));
        }

        /// <summary>
        /// 设置实体实例化辅助器。
        /// </summary>
        /// <param name="entityHelper">实体实例化辅助器。</param>
        public void SetEntityHelper(IEntityHelper entityHelper)
        {
            _entityHelper = entityHelper ?? throw new ArgumentNullException(nameof(entityHelper));
        }

        /// <summary>
        /// 每帧更新。
        /// </summary>
        /// <param name="elapseSeconds">逻辑流逝时间（秒）。</param>
        /// <param name="realElapseSeconds">真实流逝时间（秒）。</param>
        public override void Update(float elapseSeconds, float realElapseSeconds)
        {
            // 更新所有实体组
            foreach (var entityGroup in _entityGroups.Values)
            {
                entityGroup.EntityPool.Update(elapseSeconds, realElapseSeconds);
            }

            // 将需要更新的实体加入队列，避免迭代中修改集合
            _entityUpdateQueue.Clear();
            foreach (var entity in _entities.Values)
            {
                if (entity.Handle != null && entity.Handle.activeInHierarchy)
                {
                    _entityUpdateQueue.Enqueue(entity);
                }
            }

            while (_entityUpdateQueue.Count > 0)
            {
                var entity = _entityUpdateQueue.Dequeue();
                // 实体可能在前一个实体的 OnUpdate 中被隐藏，需要二次检查
                if (_entities.ContainsKey(entity.Id))
                {
                    entity.OnUpdate(elapseSeconds, realElapseSeconds);
                }
            }
        }

        /// <summary>
        /// 关闭实体管理器。
        /// </summary>
        public override void Shutdown()
        {
            HideAllLoadedEntities();

            foreach (var kvp in _entityGroups)
            {
                _objectPoolManager.DestroyPool<IEntity>(kvp.Key);
            }

            _entityGroups.Clear();
            _entities.Clear();
            _childEntityIndices.Clear();
            _parentEntityIndices.Clear();
            _entityUpdateQueue?.Clear();

            // 释放所有缓存的资源句柄
            foreach (var handle in _assetHandleCache.Values)
            {
                if (handle != null)
                {
                    handle.Release();
                }
            }
            _assetHandleCache.Clear();
        }

        /// <summary>
        /// 判断指定实体组是否存在。
        /// </summary>
        /// <param name="name">实体组名称。</param>
        /// <returns>是否存在。</returns>
        public bool HasEntityGroup(string name)
        {
            return !string.IsNullOrEmpty(name) && _entityGroups.ContainsKey(name);
        }

        /// <summary>
        /// 获取实体组。
        /// </summary>
        /// <param name="name">实体组名称。</param>
        /// <returns>实体组实例。</returns>
        public IEntityGroup GetEntityGroup(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("实体组名称不能为空", nameof(name));
            }

            if (!_entityGroups.TryGetValue(name, out var entityGroup))
            {
                throw new ArgumentException($"实体组 '{name}' 不存在");
            }

            return entityGroup;
        }

        /// <summary>
        /// 添加实体组。
        /// </summary>
        /// <param name="name">实体组名称。</param>
        /// <param name="options">实体组配置。</param>
        /// <returns>是否添加成功。</returns>
        public bool AddEntityGroup(string name, EntityGroupOptions options)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("实体组名称不能为空", nameof(name));
            }

            if (_entityGroups.ContainsKey(name))
            {
                return false;
            }

            options = options ?? new EntityGroupOptions();

            var poolOptions = new ObjectPoolOptions
            {
                AllowMultiSpawn = options.AllowMultiSpawn,
                AutoRelease = options.AutoRelease,
                Capacity = options.PoolCapacity,
                ExpireTime = options.ExpireTime,
                AutoReleaseInterval = options.AutoReleaseInterval
            };

            var entityPool = _objectPoolManager.CreatePool(
                name,
                () => CreateEntityInstance(name, options),
                poolOptions,
                null,
                OnEntityRecycle,
                OnDestroyEntity
            );

            var entityGroup = new EntityGroup(name, entityPool);
            _entityGroups[name] = entityGroup;
            return true;
        }

        /// <summary>
        /// 移除实体组。
        /// </summary>
        /// <param name="name">实体组名称。</param>
        public void RemoveEntityGroup(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("实体组名称不能为空", nameof(name));
            }

            if (!_entityGroups.TryGetValue(name, out var entityGroup))
            {
                return;
            }

            // 隐藏该组所有实体
            var entitiesToHide = _entities.Values
                .Where(e => e.EntityGroup == entityGroup)
                .ToList();

            foreach (var entity in entitiesToHide)
            {
                HideEntity(entity.Id);
            }

            _objectPoolManager.DestroyPool<IEntity>(name);
            _entityGroups.Remove(name);
        }

        /// <summary>
        /// 判断指定实体是否存在。
        /// </summary>
        /// <param name="entityId">实体标识符。</param>
        /// <returns>是否存在。</returns>
        public bool HasEntity(int entityId)
        {
            return _entities.ContainsKey(entityId);
        }

        /// <summary>
        /// 获取实体。
        /// </summary>
        /// <param name="entityId">实体标识符。</param>
        /// <returns>实体实例。</returns>
        public IEntity GetEntity(int entityId)
        {
            if (!_entities.TryGetValue(entityId, out var entity))
            {
                throw new ArgumentException($"实体 ID '{entityId}' 不存在");
            }

            return entity;
        }

        /// <summary>
        /// 获取所有已加载的实体。
        /// </summary>
        /// <returns>所有已加载的实体。</returns>
        public IEntity[] GetAllLoadedEntities()
        {
            var index = 0;
            var entities = new IEntity[_entities.Count];
            foreach (var entity in _entities.Values)
            {
                entities[index++] = entity;
            }

            return entities;
        }

        /// <summary>
        /// 异步显示实体。
        /// </summary>
        /// <param name="entityId">实体标识符。</param>
        /// <param name="entityAssetName">实体资源名称。</param>
        /// <param name="groupName">所属实体组名称。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>显示的实体实例。</returns>
        public async UniTask<IEntity> ShowEntityAsync(int entityId, string entityAssetName, string groupName, object userData = null)
        {
            if (string.IsNullOrEmpty(entityAssetName))
            {
                throw new ArgumentException("实体资源名称不能为空", nameof(entityAssetName));
            }

            if (string.IsNullOrEmpty(groupName))
            {
                throw new ArgumentException("实体组名称不能为空", nameof(groupName));
            }

            var entityGroup = GetEntityGroup(groupName);

            // 检查实体是否已存在
            if (_entities.TryGetValue(entityId, out var existingEntity))
            {
                return existingEntity;
            }

            // 从对象池生成实体
            var entity = entityGroup.SpawnEntity();

            // 判断对象池返回的实体是否已有 Handle（复用回收的实体）
            if (entity.Handle != null)
            {
                var entityView = entity.Handle.GetComponent<EntityView>();
                if (entityView != null && entity is EntityBase entityBase)
                {
                    entityView.SetEntity(entityBase);
                }
            }
            else
            {
                // 加载实体资源（使用缓存）
                AssetHandle assetHandle = await LoadEntityAssetAsync(entityAssetName);
                GameObject entityAsset = assetHandle.AssetObject as GameObject;

                // 实例化实体
                GameObject instance = await _entityHelper.InstantiateEntityAsync(entityAsset, userData);

                // 设置实体 Handle（必须在 OnInit 之前设置）
                if (entity is EntityBase entityBase)
                {
                    SetEntityHandle(entityBase, instance);
                }
            }

            // 初始化实体
            var isNewInstance = entity.EntityAssetName == null;
            entity.OnInit(entityId, entityAssetName, entityGroup, isNewInstance, userData);

            // 注册实体
            _entities[entityId] = entity;

            // 调用显示回调
            entity.OnShow(userData);

            return entity;
        }

        /// <summary>
        /// 隐藏实体。
        /// </summary>
        /// <param name="entityId">实体标识符。</param>
        public void HideEntity(int entityId)
        {
            if (!_entities.TryGetValue(entityId, out var entity))
            {
                return;
            }

            // 分离所有子实体
            if (_parentEntityIndices.TryGetValue(entityId, out var childIndices))
            {
                foreach (var childId in childIndices.ToArray())
                {
                    DetachEntity(childId);
                }
            }

            // 从父实体分离
            if (_childEntityIndices.TryGetValue(entityId, out var parentId))
            {
                var parentEntity = GetEntity(parentId);
                parentEntity.OnDetached(entity, null);
                entity.OnDetachFrom(parentEntity, null);
                _childEntityIndices.Remove(entityId);
            }

            // 调用隐藏回调
            entity.OnHide(false, null);

            // 回收实体
            entity.EntityGroup.RecycleEntity(entity);

            // 从已加载实体中移除
            _entities.Remove(entityId);
        }

        /// <summary>
        /// 隐藏所有已加载的实体。
        /// </summary>
        public void HideAllLoadedEntities()
        {
            var entityIds = _entities.Keys.ToArray();
            foreach (var entityId in entityIds)
            {
                HideEntity(entityId);
            }
        }

        /// <summary>
        /// 附加子实体到父实体。
        /// </summary>
        /// <param name="childEntityId">子实体标识符。</param>
        /// <param name="parentEntityId">父实体标识符。</param>
        /// <param name="userData">用户自定义数据。</param>
        public void AttachEntity(int childEntityId, int parentEntityId, object userData = null)
        {
            if (childEntityId == parentEntityId)
            {
                throw new ArgumentException("不能将实体附加到自己");
            }

            var childEntity = GetEntity(childEntityId);
            var parentEntity = GetEntity(parentEntityId);

            // 检查循环引用
            if (CheckCircularReference(childEntityId, parentEntityId))
            {
                throw new InvalidOperationException("检测到循环引用，无法附加实体");
            }

            // 如果已有父实体，先分离
            if (_childEntityIndices.ContainsKey(childEntityId))
            {
                DetachEntity(childEntityId);
            }

            // 附加到父实体
            _childEntityIndices[childEntityId] = parentEntityId;

            if (!_parentEntityIndices.TryGetValue(parentEntityId, out var children))
            {
                children = new List<int>();
                _parentEntityIndices[parentEntityId] = children;
            }

            children.Add(childEntityId);

            // 设置 GameObject 层级关系
            if (childEntity.Handle != null && parentEntity.Handle != null)
            {
                childEntity.Handle.transform.SetParent(parentEntity.Handle.transform);
            }

            // 调用回调
            parentEntity.OnAttached(childEntity, userData);
            childEntity.OnAttachTo(parentEntity, userData);
        }

        /// <summary>
        /// 从父实体分离子实体。
        /// </summary>
        /// <param name="childEntityId">子实体标识符。</param>
        /// <param name="userData">用户自定义数据。</param>
        public void DetachEntity(int childEntityId, object userData = null)
        {
            if (!_childEntityIndices.TryGetValue(childEntityId, out var parentId))
            {
                return;
            }

            var childEntity = GetEntity(childEntityId);
            var parentEntity = GetEntity(parentId);

            // 从父实体的子实体列表中移除
            if (_parentEntityIndices.TryGetValue(parentId, out var children))
            {
                children.Remove(childEntityId);
                if (children.Count == 0)
                {
                    _parentEntityIndices.Remove(parentId);
                }
            }

            _childEntityIndices.Remove(childEntityId);

            // 取消 GameObject 层级关系
            if (childEntity.Handle != null)
            {
                childEntity.Handle.transform.SetParent(null);
            }

            // 调用回调
            parentEntity.OnDetached(childEntity, userData);
            childEntity.OnDetachFrom(parentEntity, userData);
        }

        /// <summary>
        /// 异步加载实体资源，使用缓存避免重复加载。
        /// </summary>
        /// <param name="entityAssetName">实体资源名称。</param>
        /// <returns>资源句柄（包含 AssetObject）。</returns>
        private async UniTask<AssetHandle> LoadEntityAssetAsync(string entityAssetName)
        {
            // 优先从缓存中获取
            if (_assetHandleCache.TryGetValue(entityAssetName, out var cachedHandle))
            {
                return cachedHandle;
            }

            var handle = await _resourceManager.LoadAssetAsync<GameObject>(entityAssetName);
            var asset = handle.AssetObject as GameObject;

            if (asset == null)
            {
                handle.Release();
                throw new InvalidOperationException($"实体资源 '{entityAssetName}' 加载失败或不是 GameObject");
            }

            // 缓存资源句柄
            _assetHandleCache[entityAssetName] = handle;
            return handle;
        }

        /// <summary>
        /// 创建实体实例。
        /// </summary>
        /// <param name="groupName">实体组名称。</param>
        /// <param name="options">实体组配置。</param>
        /// <returns>新创建的实体实例。</returns>
        private IEntity CreateEntityInstance(string groupName, EntityGroupOptions options)
        {
            // 若配置了自定义实体工厂，则使用工厂创建；否则使用默认实体。
            if (options.EntityFactory != null)
            {
                return options.EntityFactory();
            }

            var entity = new DefaultEntity();
            return entity;
        }

        /// <summary>
        /// 实体回收回调。
        /// </summary>
        /// <param name="entity">被回收的实体。</param>
        private void OnEntityRecycle(IEntity entity)
        {
            if (entity.Handle != null)
            {
                var entityView = entity.Handle.GetComponent<EntityView>();
                if (entityView != null)
                {
                    entityView.ClearEntity();
                }
            }
            entity.OnRecycle();
        }

        /// <summary>
        /// 实体销毁回调。
        /// </summary>
        /// <param name="entity">被销毁的实体。</param>
        private void OnDestroyEntity(IEntity entity)
        {
            if (entity?.Handle != null)
            {
                UnityEngine.Object.Destroy(entity.Handle);
            }
        }

        /// <summary>
        /// 设置实体 Handle。
        /// </summary>
        /// <param name="entity">实体实例。</param>
        /// <param name="handle">GameObject Handle。</param>
        private void SetEntityHandle(EntityBase entity, GameObject handle)
        {
            entity.Handle = handle;
            var entityView = handle.GetComponent<EntityView>() ?? handle.AddComponent<EntityView>();
            entityView.SetEntity(entity);
        }

        /// <summary>
        /// 检查是否存在循环引用。
        /// </summary>
        /// <param name="childEntityId">子实体 ID。</param>
        /// <param name="parentEntityId">父实体 ID。</param>
        /// <returns>是否存在循环引用。</returns>
        private bool CheckCircularReference(int childEntityId, int parentEntityId)
        {
            var currentId = parentEntityId;

            while (_childEntityIndices.ContainsKey(currentId))
            {
                currentId = _childEntityIndices[currentId];

                if (currentId == childEntityId)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 生成唯一实体 ID。
        /// </summary>
        /// <returns>实体 ID。</returns>
        public int GenerateEntityId()
        {
            return ++_serialId;
        }
    }

    /// <summary>
    /// 默认实体实现，用于对象池创建时的占位。
    /// </summary>
    internal sealed class DefaultEntity : EntityBase
    {
        private GameObject _handle;

        public override GameObject Handle
        {
            get => _handle;
            set => _handle = value;
        }
    }
}
