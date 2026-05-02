// ExampleEntityUsage.cs
// 此文件为 Entity 模块的使用示例，展示如何使用 EntityManager

using UnityEngine;
using Cysharp.Threading.Tasks;
using EF.Common;
using EF.Entity;

namespace EF.Entity.Examples
{
    /// <summary>
    /// Entity 模块使用示例类
    /// 展示如何使用 EntityManager 进行实体管理
    /// </summary>
    public class ExampleEntityUsage : MonoBehaviour
    {
        private IEntityManager _entityManager;
        private int _currentEntityId = 1000;

        private void Start()
        {
            // 获取 EntityManager
            _entityManager = ModuleSystem.Get<IEntityManager>();

            // 初始化实体组
            InitializeEntityGroups();

            // 示例：创建实体
            // CreateEntityExample().Forget();
        }

        /// <summary>
        /// 初始化实体组
        /// </summary>
        private void InitializeEntityGroups()
        {
            // 添加敌人实体组
            var enemyOptions = new EntityGroupOptions
            {
                Capacity = 100,
                AutoRelease = true,
                ExpireTime = 60f,
                PoolCapacity = 20
            };
            _entityManager.AddEntityGroup("Enemy", enemyOptions);

            // 添加道具实体组
            var itemOptions = new EntityGroupOptions
            {
                Capacity = 200,
                AutoRelease = true,
                ExpireTime = 120f,
                PoolCapacity = 50
            };
            _entityManager.AddEntityGroup("Item", itemOptions);

            // 添加特效实体组
            var effectOptions = new EntityGroupOptions
            {
                Capacity = 50,
                AutoRelease = true,
                ExpireTime = 10f,
                PoolCapacity = 10
            };
            _entityManager.AddEntityGroup("Effect", effectOptions);
        }

        /// <summary>
        /// 示例：创建并显示实体
        /// </summary>
        private async UniTask CreateEntityExample()
        {
            int entityId = GenerateEntityId();
            string assetName = "Prefabs/Enemy";
            string groupName = "Enemy";

            // 创建显示数据
            var showData = new EntityShowData
            {
                Position = new Vector3(0f, 0f, 5f),
                Rotation = Quaternion.identity,
                Scale = Vector3.one
            };

            // 异步显示实体
            IEntity entity = await _entityManager.ShowEntityAsync(entityId, assetName, groupName, showData);

            Debug.Log($"实体已创建：ID={entity.Id}, Asset={entity.EntityAssetName}");
        }

        /// <summary>
        /// 示例：隐藏实体
        /// </summary>
        private void HideEntityExample(int entityId)
        {
            if (_entityManager.HasEntity(entityId))
            {
                _entityManager.HideEntity(entityId);
                Debug.Log($"实体已隐藏：ID={entityId}");
            }
        }

        /// <summary>
        /// 示例：隐藏所有实体
        /// </summary>
        private void HideAllEntitiesExample()
        {
            _entityManager.HideAllLoadedEntities();
            Debug.Log("所有实体已隐藏");
        }

        /// <summary>
        /// 示例：实体层级关系
        /// </summary>
        private async UniTask EntityHierarchyExample()
        {
            // 创建父实体（如角色）
            int parentId = GenerateEntityId();
            IEntity parent = await _entityManager.ShowEntityAsync(parentId, "Prefabs/Character", "Player");

            // 创建子实体（如武器）
            int childId = GenerateEntityId();
            IEntity child = await _entityManager.ShowEntityAsync(childId, "Prefabs/Weapon", "Item");

            // 将武器附加到角色
            _entityManager.AttachEntity(childId, parentId);
            Debug.Log($"武器 {childId} 已附加到角色 {parentId}");

            // 分离武器
            _entityManager.DetachEntity(childId);
            Debug.Log($"武器 {childId} 已从角色 {parentId} 分离");
        }

        /// <summary>
        /// 示例：获取所有已加载实体
        /// </summary>
        private void GetAllEntitiesExample()
        {
            IEntity[] entities = _entityManager.GetAllLoadedEntities();
            Debug.Log($"当前已加载实体数量：{entities.Length}");

            foreach (var entity in entities)
            {
                Debug.Log($"实体 ID={entity.Id}, Asset={entity.EntityAssetName}, Group={entity.EntityGroup.Name}");
            }
        }

        /// <summary>
        /// 示例：获取实体组信息
        /// </summary>
        private void GetEntityGroupInfoExample()
        {
            if (_entityManager.HasEntityGroup("Enemy"))
            {
                var group = _entityManager.GetEntityGroup("Enemy");
                Debug.Log($"实体组 'Enemy' - 实体数量：{group.EntityCount}");
            }
        }

        /// <summary>
        /// 示例：移除实体组
        /// </summary>
        private void RemoveEntityGroupExample(string groupName)
        {
            if (_entityManager.HasEntityGroup(groupName))
            {
                _entityManager.RemoveEntityGroup(groupName);
                Debug.Log($"实体组 '{groupName}' 已移除");
            }
        }

        /// <summary>
        /// 生成唯一实体 ID
        /// </summary>
        private int GenerateEntityId()
        {
            return ++_currentEntityId;
        }

        // ============================================================
        // Unity 编辑器测试方法（仅在 Editor 模式下可用）
        // ============================================================

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Entity Examples/Create Enemy")]
        private static void EditorCreateEnemy()
        {
            var usage = FindObjectOfType<ExampleEntityUsage>();
            if (usage == null)
            {
                var go = new GameObject("ExampleEntityUsage");
                usage = go.AddComponent<ExampleEntityUsage>();
            }
            usage.CreateEntityExample().Forget();
        }

        [UnityEditor.MenuItem("Entity Examples/Hide All Entities")]
        private static void EditorHideAll()
        {
            var usage = FindObjectOfType<ExampleEntityUsage>();
            if (usage != null)
            {
                usage.HideAllEntitiesExample();
            }
        }
#endif
    }
}
