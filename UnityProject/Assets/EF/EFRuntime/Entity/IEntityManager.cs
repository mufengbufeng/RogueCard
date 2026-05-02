using Cysharp.Threading.Tasks;
using EF.Common;
using EF.ObjectPool;
using EF.Resource;

namespace EF.Entity
{
    /// <summary>
    /// 定义实体管理器提供的能力。
    /// </summary>
    public interface IEntityManager : IEFManager
    {
        /// <summary>
        /// 当前已加载的实体数量。
        /// </summary>
        int EntityCount { get; }

        /// <summary>
        /// 当前实体组数量。
        /// </summary>
        int EntityGroupCount { get; }

        /// <summary>
        /// 设置对象池管理器。
        /// </summary>
        /// <param name="objectPoolManager">对象池管理器。</param>
        void SetObjectPoolManager(IObjectPoolManager objectPoolManager);

        /// <summary>
        /// 设置资源管理器。
        /// </summary>
        /// <param name="resourceManager">资源管理器。</param>
        void SetResourceManager(IResourceManager resourceManager);

        /// <summary>
        /// 设置实体实例化辅助器。
        /// </summary>
        /// <param name="entityHelper">实体实例化辅助器。</param>
        void SetEntityHelper(IEntityHelper entityHelper);

        /// <summary>
        /// 判断指定实体组是否存在。
        /// </summary>
        /// <param name="name">实体组名称。</param>
        /// <returns>是否存在。</returns>
        bool HasEntityGroup(string name);

        /// <summary>
        /// 获取实体组。
        /// </summary>
        /// <param name="name">实体组名称。</param>
        /// <returns>实体组实例。</returns>
        IEntityGroup GetEntityGroup(string name);

        /// <summary>
        /// 添加实体组。
        /// </summary>
        /// <param name="name">实体组名称。</param>
        /// <param name="options">实体组配置。</param>
        /// <returns>是否添加成功。</returns>
        bool AddEntityGroup(string name, EntityGroupOptions options);

        /// <summary>
        /// 移除实体组。
        /// </summary>
        /// <param name="name">实体组名称。</param>
        void RemoveEntityGroup(string name);

        /// <summary>
        /// 判断指定实体是否存在。
        /// </summary>
        /// <param name="entityId">实体标识符。</param>
        /// <returns>是否存在。</returns>
        bool HasEntity(int entityId);

        /// <summary>
        /// 获取实体。
        /// </summary>
        /// <param name="entityId">实体标识符。</param>
        /// <returns>实体实例。</returns>
        IEntity GetEntity(int entityId);

        /// <summary>
        /// 获取所有已加载的实体。
        /// </summary>
        /// <returns>所有已加载的实体。</returns>
        IEntity[] GetAllLoadedEntities();

        /// <summary>
        /// 异步显示实体。
        /// </summary>
        /// <param name="entityId">实体标识符。</param>
        /// <param name="entityAssetName">实体资源名称。</param>
        /// <param name="groupName">所属实体组名称。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>显示的实体实例。</returns>
        UniTask<IEntity> ShowEntityAsync(int entityId, string entityAssetName, string groupName, object userData = null);

        /// <summary>
        /// 隐藏实体。
        /// </summary>
        /// <param name="entityId">实体标识符。</param>
        void HideEntity(int entityId);

        /// <summary>
        /// 隐藏所有已加载的实体。
        /// </summary>
        void HideAllLoadedEntities();

        /// <summary>
        /// 附加子实体到父实体。
        /// </summary>
        /// <param name="childEntityId">子实体标识符。</param>
        /// <param name="parentEntityId">父实体标识符。</param>
        /// <param name="userData">用户自定义数据。</param>
        void AttachEntity(int childEntityId, int parentEntityId, object userData = null);

        /// <summary>
        /// 从父实体分离子实体。
        /// </summary>
        /// <param name="childEntityId">子实体标识符。</param>
        /// <param name="userData">用户自定义数据。</param>
        void DetachEntity(int childEntityId, object userData = null);

        /// <summary>
        /// 生成唯一实体 ID。
        /// 用于外部模块在调用 ShowEntityAsync 之前获取唯一标识符。
        /// </summary>
        /// <returns>唯一的实体 ID。</returns>
        int GenerateEntityId();
    }
}
