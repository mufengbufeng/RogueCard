// ExampleEntity.cs
// 此文件为 Entity 模块的使用示例，展示如何创建自定义实体类

using UnityEngine;
using Cysharp.Threading.Tasks;
using EF.Common;
using EF.Entity;

namespace EF.Entity.Examples
{
    /// <summary>
    /// 示例实体类 - 展示如何继承 EntityBase 实现自定义实体
    /// </summary>
    public class ExampleEntity : EntityBase
    {
        private GameObject _handle;
        private float _lifeTime;

        /// <summary>
        /// 实体关联的 GameObject
        /// </summary>
        public override GameObject Handle
        {
            get => _handle;
             set => _handle = value;
        }

        /// <summary>
        /// 实体初始化 - 设置 Handle 引用
        /// </summary>
        public override void OnInit(int entityId, string entityAssetName, IEntityGroup entityGroup, bool isNewInstance, object userData)
        {
            base.OnInit(entityId, entityAssetName, entityGroup, isNewInstance, userData);

            // 从用户数据中获取预制体实例（如果有）
            if (userData is GameObject prefab)
            {
                _handle = prefab;
            }

            _lifeTime = 0f;
        }

        /// <summary>
        /// 实体显示时调用
        /// </summary>
        public override void OnShow(object userData)
        {
            base.OnShow(userData);

            if (_handle != null)
            {
                _handle.SetActive(true);
                Debug.Log($"[ExampleEntity] 实体 {Id} 已显示");
            }

            // 可以从 userData 中获取显示时的额外参数
            if (userData is EntityShowData showData)
            {
                _handle.transform.position = showData.Position;
                _handle.transform.rotation = showData.Rotation;
            }
        }

        /// <summary>
        /// 实体隐藏时调用
        /// </summary>
        public override void OnHide(bool isShutdown, object userData)
        {
            base.OnHide(isShutdown, userData);

            if (_handle != null)
            {
                _handle.SetActive(false);
                Debug.Log($"[ExampleEntity] 实体 {Id} 已隐藏");
            }
        }

        /// <summary>
        /// 实体回收时调用
        /// </summary>
        public override void OnRecycle()
        {
            base.OnRecycle();
            _lifeTime = 0f;
            Debug.Log($"[ExampleEntity] 实体 {Id} 已回收");
        }

        /// <summary>
        /// 子实体附加到本实体时调用
        /// </summary>
        public override void OnAttached(IEntity childEntity, object userData)
        {
            base.OnAttached(childEntity, userData);
            Debug.Log($"[ExampleEntity] 子实体 {childEntity.Id} 附加到实体 {Id}");
        }

        /// <summary>
        /// 子实体从本实体分离时调用
        /// </summary>
        public override void OnDetached(IEntity childEntity, object userData)
        {
            base.OnDetached(childEntity, userData);
            Debug.Log($"[ExampleEntity] 子实体 {childEntity.Id} 从实体 {Id} 分离");
        }

        /// <summary>
        /// 本实体附加到父实体时调用
        /// </summary>
        public override void OnAttachTo(IEntity parentEntity, object userData)
        {
            base.OnAttachTo(parentEntity, userData);
            Debug.Log($"[ExampleEntity] 实体 {Id} 附加到父实体 {parentEntity.Id}");
        }

        /// <summary>
        /// 本实体从父实体分离时调用
        /// </summary>
        public override void OnDetachFrom(IEntity parentEntity, object userData)
        {
            base.OnDetachFrom(parentEntity, userData);
            Debug.Log($"[ExampleEntity] 实体 {Id} 从父实体 {parentEntity.Id} 分离");
        }

        /// <summary>
        /// 每帧更新
        /// </summary>
        public override void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
            base.OnUpdate(elapseSeconds, realElapseSeconds);

            if (_handle != null && _handle.activeSelf)
            {
                _lifeTime += elapseSeconds;

                // 示例：让实体简单的旋转
                _handle.transform.Rotate(0f, elapseSeconds * 90f, 0f);
            }
        }
    }

    /// <summary>
    /// 实体显示数据
    /// </summary>
    public class EntityShowData
    {
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; } = Quaternion.identity;
        public Vector3 Scale { get; set; } = Vector3.one;
    }
}
