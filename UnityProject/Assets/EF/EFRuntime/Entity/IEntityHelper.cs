using UnityEngine;
using Cysharp.Threading.Tasks;

namespace EF.Entity
{
    /// <summary>
    /// 定义实体实例化辅助接口，用于创建实体关联的 GameObject。
    /// </summary>
    public interface IEntityHelper
    {
        /// <summary>
        /// 异步实例化实体 GameObject。
        /// </summary>
        /// <param name="entityAsset">实体资源 GameObject。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>实例化后的 GameObject。</returns>
        UniTask<GameObject> InstantiateEntityAsync(GameObject entityAsset, object userData);
    }
}
