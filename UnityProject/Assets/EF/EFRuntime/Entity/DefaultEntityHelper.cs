using UnityEngine;
using Cysharp.Threading.Tasks;

namespace EF.Entity
{
    /// <summary>
    /// 默认的实体实例化辅助器实现。
    /// </summary>
    public sealed class DefaultEntityHelper : IEntityHelper
    {
        /// <summary>
        /// 异步实例化实体 GameObject。
        /// </summary>
        /// <param name="entityAsset">实体资源 GameObject。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>实例化后的 GameObject。</returns>
        public UniTask<GameObject> InstantiateEntityAsync(GameObject entityAsset, object userData)
        {
            if (entityAsset == null)
            {
                throw new System.ArgumentNullException(nameof(entityAsset));
            }

            var instance = UnityEngine.Object.Instantiate(entityAsset);
            return UniTask.FromResult(instance);
        }
    }
}
